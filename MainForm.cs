using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using Sybase.DataWindow;
using RadioButton = System.Windows.Forms.RadioButton;
using CheckBox = System.Windows.Forms.CheckBox;
using Timer = System.Windows.Forms.Timer;

namespace TUH_WristbandPrinter
{
    public class MainForm : Form
    {
        private StaffUser _currentUser;
        private AppConfig _config;

        // ToolTips
        private ToolTip toolTips;

        // Header controls
        private Panel topBar;
        private Label lblAppTitle;
        private Label lblAppSub;
        private Label lblUser;
        private Button btnLogout;
        private Button btnToggleSidebar;
        private Button btnTopHistory;
        private Button btnServerBadge;
        private Button btnCheckUpdate;
        private CheckBox chkAlwaysOnTop;

        // Body & layout
        private Panel bodyPanel;
        private TableLayoutPanel midSplit;
        private Panel pnlSearch;
        private Panel pnlQueueContainer;
        private Panel pnlRightContainer;

        // Search controls (Ultra-prominent High-visibility Barcode/HN Scanner)
        private Label lblSearchTitle;
        private Label lblSearchHint;
        private Label lblBarcodeBadge;
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnClear;
        private Button btnHistorySearch;
        private ComboBox cboAdmissions;
        private Label lblAdmissionsPrompt;

        // Queue controls
        private Panel topQueue;
        private DataGridView dgvQueue;
        private Button btnPrintAll;
        private Button btnRemoveSelected;
        private Button btnClearQueue;
        private CheckBox chkSelectAll;
        private Label lblQueueCount;

        // Queue Columns
        private DataGridViewCheckBoxColumn colChk;
        private DataGridViewTextBoxColumn colAn;
        private DataGridViewTextBoxColumn colHn;
        private DataGridViewTextBoxColumn colName;
        private DataGridViewTextBoxColumn colWard;
        private DataGridViewTextBoxColumn colAllergy;
        private DataGridViewTextBoxColumn colCopies;
        private DataGridViewTextBoxColumn colStatus;

        // Active Patient Card controls
        private Panel pnlPatientCard;
        private Label lblCardHeader;
        private Label lblPtName;
        private Label lblPtHn;
        private Label lblPtAn;
        private Label lblPtGenderAge;
        private Label lblPtWard;
        private Label lblPtAdmitDate;
        private Panel pnlAllergy;
        private Label lblAllergyText;
        private Button btnPrintSingle;
        private Button btnPreview;

        // Printer & Central Print Server controls
        private Panel pnlPrinter;
        private Label lblPrnTitle;
        private RadioButton rdoPrintLocal;
        private RadioButton rdoPrintRemote;
        private Panel pnlLocalPrinter;
        private ComboBox cboPrinters;
        private Panel pnlRemotePrinter;
        private TextBox txtRemoteHost;
        private Button btnDiscoverServer;
        private Button btnTestServer;
        private Label lblRemoteStatus;
        private Button btnSavePrinter;
        private RadioButton rdoAdult;
        private RadioButton rdoChild;
        private Label lblCopies;
        private NumericUpDown numCopies;
        private CheckBox chkAutoPrint;
        private Label lblServerNotice;

        // History
        private Panel pnlHist;
        private Label lblHistTitle;
        private Button btnOpenAllHistory;
        private ListView lstHistory;

        // Status
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatusMsg;
        private ToolStripStatusLabel lblCurrentPrinterStatus;
        private ToolStripStatusLabel lblServerStatusStrip;
        private Timer heartbeatTimer;

        // Flash timer for duplicates
        private Timer flashTimer;
        private int flashingRowIndex = -1;
        private Color originalRowColor = Color.White;

        // State
        private List<QueueItem> _queue = new List<QueueItem>();
        private PatientAdmission _currentPatient;
        private DataStore _currentDataStore;
        private List<PatientAdmission> _recentAdmissions = new List<PatientAdmission>();
        private bool _isSidebarMode = false;

        public MainForm(StaffUser user)
        {
            _currentUser = user;
            _config = AppConfig.Load();

            toolTips = new ToolTip();
            toolTips.AutoPopDelay = 5000;
            toolTips.InitialDelay = 400;
            toolTips.ReshowDelay = 200;

            InitializeComponent();
            LoadPrinters();
            LoadPrintModeConfig();
            ResetPatientView();
            LoadRecentHistoryList();

            // Start Central Print Server on host machine
            StartPrintServerIfEnabled();
            UpdateServerBadge();

            // Set app icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(iconPath))
                    this.Icon = new Icon(iconPath);
            }
            catch { }

            // Restore saved mode
            if (_config.WindowMode == "Sidebar")
            {
                SetSidebarMode(true);
            }
            else
            {
                SetSidebarMode(false);
            }

            this.TopMost = _config.AlwaysOnTop;
            chkAlwaysOnTop.Checked = _config.AlwaysOnTop;

            // Heartbeat timer to keep remoting session alive
            heartbeatTimer = new Timer();
            heartbeatTimer.Interval = 10 * 60 * 1000;
            heartbeatTimer.Tick += (s, e) =>
            {
                try { EphisService.Instance.SearchAdmissions("0"); } catch { }
            };
            heartbeatTimer.Start();

            // Auto Update background check on startup
            Timer startupUpdateTimer = new Timer();
            startupUpdateTimer.Interval = 3000;
            startupUpdateTimer.Tick += (s, e) =>
            {
                startupUpdateTimer.Stop();
                startupUpdateTimer.Dispose();
                if (_config.AutoCheckUpdate)
                {
                    CheckUpdateInBackground(false);
                }
            };
            startupUpdateTimer.Start();

            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            this.Resize += MainForm_Resize;
            this.FormClosing += (s, e) =>
            {
                try { PrintServerService.Instance.Stop(); } catch { }
            };
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            UpdateLayoutSizes();
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F9)
            {
                e.Handled = true;
                if (_queue.Count > 0)
                    DoPrintAll();
                else
                    DoPrintSingle();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                ClearSearchAndActivePatient();
            }
            else if (e.KeyCode == Keys.F4)
            {
                e.Handled = true;
                OpenHistoryDialog();
            }
        }

        private void InitializeComponent()
        {
            this.Text = "TUH Wristband Printer - ระบบพิมพ์สายรัดข้อมือผู้ป่วย";
            this.Size = new Size(1100, 760);
            this.MinimumSize = new Size(380, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            // 1. Top Header Bar
            topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 60;
            topBar.BackColor = Color.FromArgb(24, 76, 120);
            this.Controls.Add(topBar);

            lblAppTitle = new Label();
            lblAppTitle.Text = "🏥 ระบบพิมพ์สายรัดข้อมือผู้ป่วย (TUH Wristband)";
            lblAppTitle.ForeColor = Color.White;
            lblAppTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblAppTitle.Location = new Point(12, 10);
            lblAppTitle.AutoSize = true;
            topBar.Controls.Add(lblAppTitle);

            lblAppSub = new Label();
            lblAppSub.Text = "โรงพยาบาลธรรมศาสตร์เฉลิมพระเกียรติ | งานผู้ป่วยใน (IPD)";
            lblAppSub.ForeColor = Color.FromArgb(200, 225, 245);
            lblAppSub.Font = new Font("Segoe UI", 8.5F);
            lblAppSub.Location = new Point(14, 34);
            lblAppSub.AutoSize = true;
            topBar.Controls.Add(lblAppSub);

            btnServerBadge = new Button();
            btnServerBadge.Text = "📡 Print Server";
            btnServerBadge.Size = new Size(160, 32);
            btnServerBadge.BackColor = Color.FromArgb(4, 120, 87); // Emerald Dark Green
            btnServerBadge.ForeColor = Color.White;
            btnServerBadge.FlatStyle = FlatStyle.Flat;
            btnServerBadge.FlatAppearance.BorderSize = 0;
            btnServerBadge.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnServerBadge.Cursor = Cursors.Hand;
            btnServerBadge.Click += (s, e) => ShowServerInfoDialog();
            toolTips.SetToolTip(btnServerBadge, "ดูข้อมูลเชื่อมต่อ Central Print Server สำหรับให้เครื่องอื่นในวอร์ดสั่งพิมพ์มาที่นี่");
            topBar.Controls.Add(btnServerBadge);

            btnCheckUpdate = new Button();
            btnCheckUpdate.Text = "๐” v" + UpdateService.CurrentVersion;
            btnCheckUpdate.Size = new Size(95, 32);
            btnCheckUpdate.BackColor = Color.FromArgb(24, 76, 120);
            btnCheckUpdate.ForeColor = Color.White;
            btnCheckUpdate.FlatStyle = FlatStyle.Flat;
            btnCheckUpdate.FlatAppearance.BorderSize = 0;
            btnCheckUpdate.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnCheckUpdate.Cursor = Cursors.Hand;
            btnCheckUpdate.Click += (s, e) => CheckUpdateInBackground(true);
            toolTips.SetToolTip(btnCheckUpdate, "เธเธฅเธดเธเน€เธเธทเนเธญเธ•เธฃเธงเธเธชเธญเธเธเธฒเธฃเธญเธฑเธเน€เธ”เธ•เน€เธงเธญเธฃเนเธเธฑเธเนเธซเธกเน (Auto Update)");
            topBar.Controls.Add(btnCheckUpdate);

            btnTopHistory = new Button();
            btnTopHistory.Text = "📜 ประวัติพิมพ์";
            btnTopHistory.Size = new Size(100, 32);
            btnTopHistory.BackColor = Color.FromArgb(15, 118, 110); // Medical Teal
            btnTopHistory.ForeColor = Color.White;
            btnTopHistory.FlatStyle = FlatStyle.Flat;
            btnTopHistory.FlatAppearance.BorderSize = 0;
            btnTopHistory.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnTopHistory.Cursor = Cursors.Hand;
            btnTopHistory.Click += (s, e) => OpenHistoryDialog();
            toolTips.SetToolTip(btnTopHistory, "ดูประวัติการพิมพ์ย้อนหลัง และสั่งพิมพ์ซ้ำ (ปุ่มลัด: F4)");
            topBar.Controls.Add(btnTopHistory);

            btnToggleSidebar = new Button();
            btnToggleSidebar.Text = "📌 แถบข้าง";
            btnToggleSidebar.Size = new Size(105, 32);
            btnToggleSidebar.BackColor = Color.FromArgb(41, 128, 185);
            btnToggleSidebar.ForeColor = Color.White;
            btnToggleSidebar.FlatStyle = FlatStyle.Flat;
            btnToggleSidebar.FlatAppearance.BorderSize = 0;
            btnToggleSidebar.Cursor = Cursors.Hand;
            btnToggleSidebar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnToggleSidebar.Click += (s, e) => SetSidebarMode(!_isSidebarMode);
            toolTips.SetToolTip(btnToggleSidebar, "สลับระหว่างโหมดหน้าต่างปกติ กับโหมดแถบข้างติดขอบจอ");
            topBar.Controls.Add(btnToggleSidebar);

            chkAlwaysOnTop = new CheckBox();
            chkAlwaysOnTop.Text = "อยู่บนสุด";
            chkAlwaysOnTop.ForeColor = Color.White;
            chkAlwaysOnTop.Font = new Font("Segoe UI", 8.5F);
            chkAlwaysOnTop.Size = new Size(78, 24);
            chkAlwaysOnTop.CheckedChanged += (s, e) =>
            {
                this.TopMost = chkAlwaysOnTop.Checked;
                _config.AlwaysOnTop = chkAlwaysOnTop.Checked;
                _config.Save();
            };
            topBar.Controls.Add(chkAlwaysOnTop);

            lblUser = new Label();
            lblUser.Text = string.Format("{0}", _currentUser != null ? _currentUser.DisplayName : "");
            lblUser.ForeColor = Color.FromArgb(230, 240, 255);
            lblUser.Font = new Font("Segoe UI", 8.5F);
            lblUser.Size = new Size(110, 22);
            lblUser.TextAlign = ContentAlignment.MiddleRight;
            topBar.Controls.Add(lblUser);

            btnLogout = new Button();
            btnLogout.Text = "ออก";
            btnLogout.Size = new Size(58, 32);
            btnLogout.BackColor = Color.FromArgb(190, 45, 45);
            btnLogout.ForeColor = Color.White;
            btnLogout.FlatStyle = FlatStyle.Flat;
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Cursor = Cursors.Hand;
            btnLogout.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnLogout.Click += (s, e) => Logout();
            toolTips.SetToolTip(btnLogout, "ออกจากระบบและยกเลิกการจำรหัสผ่าน");
            topBar.Controls.Add(btnLogout);

            // 2. Bottom Status Strip
            statusStrip = new StatusStrip();
            lblStatusMsg = new ToolStripStatusLabel();
            lblStatusMsg.Spring = true;
            lblStatusMsg.TextAlign = ContentAlignment.MiddleLeft;
            lblStatusMsg.Text = "พร้อมใช้งาน - กรุณากรอกหรือยิงบาร์โค้ด HN / AN";

            lblCurrentPrinterStatus = new ToolStripStatusLabel();
            lblCurrentPrinterStatus.Text = "เครื่องพิมพ์: -";
            lblCurrentPrinterStatus.BorderSides = ToolStripStatusLabelBorderSides.Left;

            lblServerStatusStrip = new ToolStripStatusLabel();
            lblServerStatusStrip.Text = "Print Server: กำลังเริ่ม";
            lblServerStatusStrip.BorderSides = ToolStripStatusLabelBorderSides.Left;

            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatusMsg, lblCurrentPrinterStatus, lblServerStatusStrip });
            this.Controls.Add(statusStrip);

            // 3. Body Panel
            bodyPanel = new Panel();
            bodyPanel.Dock = DockStyle.Fill;
            bodyPanel.Padding = new Padding(8);
            this.Controls.Add(bodyPanel);
            bodyPanel.BringToFront();

            BuildSearchPanel();
            BuildQueueSection();
            BuildRightDetailsSection();

            // Assemble main layout
            midSplit = new TableLayoutPanel();
            midSplit.Dock = DockStyle.Fill;
            midSplit.Padding = new Padding(0, 6, 0, 0);
            bodyPanel.Controls.Add(midSplit);
            midSplit.BringToFront();
        }

        private void BuildSearchPanel()
        {
            pnlSearch = new Panel();
            pnlSearch.Dock = DockStyle.Top;
            pnlSearch.Height = 84;
            pnlSearch.BackColor = Color.White;
            pnlSearch.BorderStyle = BorderStyle.FixedSingle;
            pnlSearch.Padding = new Padding(10, 8, 10, 8);
            bodyPanel.Controls.Add(pnlSearch);

            lblSearchTitle = new Label();
            lblSearchTitle.Text = "🔍 ระบุหมายเลข HN หรือ AN ผู้ป่วย (ยิงบาร์โค้ดได้ทันที):";
            lblSearchTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblSearchTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblSearchTitle.Location = new Point(12, 6);
            lblSearchTitle.AutoSize = true;
            pnlSearch.Controls.Add(lblSearchTitle);

            lblSearchHint = new Label();
            lblSearchHint.Text = "[ ⚡ ช่องสแกนสีเหลืองเด่นชัด  |  ⌨️ Enter: เพิ่มเข้าคิว  |  Esc: ล้าง ]";
            lblSearchHint.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblSearchHint.ForeColor = Color.FromArgb(180, 83, 9); // Warm Amber
            lblSearchHint.Location = new Point(375, 8);
            lblSearchHint.AutoSize = true;
            pnlSearch.Controls.Add(lblSearchHint);

            // High-contrast Badge in front of textbox
            lblBarcodeBadge = new Label();
            lblBarcodeBadge.Text = "HN / AN";
            lblBarcodeBadge.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblBarcodeBadge.BackColor = Color.FromArgb(24, 76, 120); // Medical Navy
            lblBarcodeBadge.ForeColor = Color.White;
            lblBarcodeBadge.TextAlign = ContentAlignment.MiddleCenter;
            lblBarcodeBadge.Location = new Point(12, 29);
            lblBarcodeBadge.Size = new Size(72, 38);
            pnlSearch.Controls.Add(lblBarcodeBadge);

            // High-visibility Canary Yellow Barcode Scanner Box
            txtSearch = new TextBox();
            txtSearch.Location = new Point(88, 30);
            txtSearch.Size = new Size(250, 36);
            txtSearch.Font = new Font("Segoe UI", 15.5F, FontStyle.Bold);
            txtSearch.ForeColor = Color.FromArgb(15, 23, 42);
            txtSearch.BackColor = Color.FromArgb(254, 249, 195); // High-visibility Soft Canary Yellow (#FEF9C3)
            txtSearch.BorderStyle = BorderStyle.FixedSingle;
            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    DoSearchAndAddQueue();
                }
            };
            txtSearch.Enter += (s, e) =>
            {
                txtSearch.BackColor = Color.FromArgb(254, 240, 138); // Glowing Bright Sun Yellow (#FEF08A)
                lblBarcodeBadge.BackColor = Color.FromArgb(16, 149, 106); // Emerald Green highlight indicating "READY TO SCAN"
            };
            txtSearch.Leave += (s, e) =>
            {
                txtSearch.BackColor = Color.FromArgb(254, 249, 195); // Soft yellow
                lblBarcodeBadge.BackColor = Color.FromArgb(24, 76, 120); // Medical Navy
            };
            toolTips.SetToolTip(txtSearch, "ช่องสแกนบาร์โค้ด / ระบุหมายเลข HN หรือ AN (กด Enter เพื่อค้นหาและเพิ่มเข้าคิว)");
            pnlSearch.Controls.Add(txtSearch);

            btnSearch = new Button();
            btnSearch.Text = "＋ เพิ่มเข้าคิว [Enter]";
            btnSearch.Location = new Point(346, 29);
            btnSearch.Size = new Size(135, 38);
            btnSearch.BackColor = Color.FromArgb(24, 76, 120);
            btnSearch.ForeColor = Color.White;
            btnSearch.FlatStyle = FlatStyle.Flat;
            btnSearch.FlatAppearance.BorderSize = 0;
            btnSearch.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnSearch.Cursor = Cursors.Hand;
            btnSearch.Click += (s, e) => DoSearchAndAddQueue();
            toolTips.SetToolTip(btnSearch, "ค้นหาและเพิ่มผู้ป่วยเข้าคิวพิมพ์ (Enter)");
            pnlSearch.Controls.Add(btnSearch);

            // Clear Button - High-visibility Crisp Red
            btnClear = new Button();
            btnClear.Text = "✕ ล้างข้อมูล [Esc]";
            btnClear.Location = new Point(489, 29);
            btnClear.Size = new Size(115, 38);
            btnClear.BackColor = Color.FromArgb(220, 38, 38); // Crisp Crimson Red
            btnClear.ForeColor = Color.White;
            btnClear.FlatStyle = FlatStyle.Flat;
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnClear.Cursor = Cursors.Hand;
            btnClear.Click += (s, e) => ClearSearchAndActivePatient();
            btnClear.MouseEnter += (s, e) => btnClear.BackColor = Color.FromArgb(185, 28, 28);
            btnClear.MouseLeave += (s, e) => btnClear.BackColor = Color.FromArgb(220, 38, 38);
            toolTips.SetToolTip(btnClear, "ล้างข้อมูลค้นหาและรายละเอียดผู้ป่วย เพื่อเริ่มค้นหาหรือยิงใหม่ (ปุ่มลัด: Esc)");
            pnlSearch.Controls.Add(btnClear);

            btnHistorySearch = new Button();
            btnHistorySearch.Text = "📜 ประวัติพิมพ์ (F4)";
            btnHistorySearch.Location = new Point(612, 29);
            btnHistorySearch.Size = new Size(125, 38);
            btnHistorySearch.BackColor = Color.FromArgb(71, 85, 105);
            btnHistorySearch.ForeColor = Color.White;
            btnHistorySearch.FlatStyle = FlatStyle.Flat;
            btnHistorySearch.FlatAppearance.BorderSize = 0;
            btnHistorySearch.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnHistorySearch.Cursor = Cursors.Hand;
            btnHistorySearch.Click += (s, e) => OpenHistoryDialog();
            toolTips.SetToolTip(btnHistorySearch, "ดูประวัติการพิมพ์ย้อนหลัง และสั่งพิมพ์ซ้ำ (F4)");
            pnlSearch.Controls.Add(btnHistorySearch);

            // Multiple admissions dropdown
            lblAdmissionsPrompt = new Label();
            lblAdmissionsPrompt.Text = "เลือก Admit:";
            lblAdmissionsPrompt.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblAdmissionsPrompt.ForeColor = Color.FromArgb(180, 83, 9);
            lblAdmissionsPrompt.Location = new Point(748, 8);
            lblAdmissionsPrompt.AutoSize = true;
            lblAdmissionsPrompt.Visible = false;
            pnlSearch.Controls.Add(lblAdmissionsPrompt);

            cboAdmissions = new ComboBox();
            cboAdmissions.Location = new Point(748, 30);
            cboAdmissions.Size = new Size(270, 28);
            cboAdmissions.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAdmissions.Font = new Font("Segoe UI", 9.5F);
            cboAdmissions.Visible = false;
            cboAdmissions.SelectedIndexChanged += cboAdmissions_SelectedIndexChanged;
            pnlSearch.Controls.Add(cboAdmissions);
        }

        private void BuildQueueSection()
        {
            pnlQueueContainer = new Panel();
            pnlQueueContainer.Dock = DockStyle.Fill;
            pnlQueueContainer.BackColor = Color.White;
            pnlQueueContainer.BorderStyle = BorderStyle.FixedSingle;

            // Header of queue
            topQueue = new Panel();
            topQueue.Dock = DockStyle.Top;
            topQueue.Height = 42;
            topQueue.BackColor = Color.FromArgb(248, 250, 252);
            topQueue.BorderStyle = BorderStyle.FixedSingle;
            pnlQueueContainer.Controls.Add(topQueue);

            chkSelectAll = new CheckBox();
            chkSelectAll.Text = "เลือกทั้งหมด";
            chkSelectAll.Checked = true;
            chkSelectAll.Location = new Point(10, 10);
            chkSelectAll.AutoSize = true;
            chkSelectAll.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            chkSelectAll.CheckedChanged += (s, e) =>
            {
                foreach (var item in _queue) item.IsSelected = chkSelectAll.Checked;
                RefreshQueueGrid();
            };
            topQueue.Controls.Add(chkSelectAll);

            lblQueueCount = new Label();
            lblQueueCount.Text = "คิวรอพิมพ์: 0";
            lblQueueCount.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblQueueCount.ForeColor = Color.FromArgb(24, 76, 120);
            lblQueueCount.Location = new Point(115, 11);
            lblQueueCount.AutoSize = true;
            topQueue.Controls.Add(lblQueueCount);

            btnRemoveSelected = new Button();
            btnRemoveSelected.Text = "ลบที่เลือก";
            btnRemoveSelected.Size = new Size(72, 26);
            btnRemoveSelected.BackColor = Color.FromArgb(241, 245, 249);
            btnRemoveSelected.FlatStyle = FlatStyle.Flat;
            btnRemoveSelected.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnRemoveSelected.Cursor = Cursors.Hand;
            btnRemoveSelected.Font = new Font("Segoe UI", 8.5F);
            btnRemoveSelected.Click += (s, e) => RemoveSelectedFromQueue();
            toolTips.SetToolTip(btnRemoveSelected, "ลบรายการที่ทำเครื่องหมายถูกออกจากคิว");
            topQueue.Controls.Add(btnRemoveSelected);

            btnClearQueue = new Button();
            btnClearQueue.Text = "🧹 ล้างคิว";
            btnClearQueue.Size = new Size(72, 26);
            btnClearQueue.BackColor = Color.FromArgb(254, 226, 226);
            btnClearQueue.ForeColor = Color.FromArgb(185, 28, 28);
            btnClearQueue.FlatStyle = FlatStyle.Flat;
            btnClearQueue.FlatAppearance.BorderColor = Color.FromArgb(248, 113, 113);
            btnClearQueue.Cursor = Cursors.Hand;
            btnClearQueue.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnClearQueue.Click += (s, e) => ClearQueue();
            toolTips.SetToolTip(btnClearQueue, "ล้างรายการผู้ป่วยทั้งหมดออกจากคิวพิมพ์");
            topQueue.Controls.Add(btnClearQueue);

            // Bottom print all button
            Panel bottomQueue = new Panel();
            bottomQueue.Dock = DockStyle.Bottom;
            bottomQueue.Height = 52;
            bottomQueue.BackColor = Color.FromArgb(248, 250, 252);
            bottomQueue.Padding = new Padding(6);
            pnlQueueContainer.Controls.Add(bottomQueue);

            btnPrintAll = new Button();
            btnPrintAll.Text = "🖨️  พิมพ์ทั้งหมดในคิว (0 รายการ) [F9]";
            btnPrintAll.Dock = DockStyle.Fill;
            btnPrintAll.BackColor = Color.FromArgb(16, 149, 106); // Emerald Green
            btnPrintAll.ForeColor = Color.White;
            btnPrintAll.FlatStyle = FlatStyle.Flat;
            btnPrintAll.FlatAppearance.BorderSize = 0;
            btnPrintAll.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnPrintAll.Cursor = Cursors.Hand;
            btnPrintAll.Click += (s, e) => DoPrintAll();
            toolTips.SetToolTip(btnPrintAll, "สั่งพิมพ์สายรัดข้อมือตามรายการที่เลือกทั้งหมด (F9)");
            bottomQueue.Controls.Add(btnPrintAll);

            // DataGridView setup
            dgvQueue = new DataGridView();
            dgvQueue.Dock = DockStyle.Fill;
            dgvQueue.BackgroundColor = Color.White;
            dgvQueue.BorderStyle = BorderStyle.None;
            dgvQueue.RowHeadersVisible = false;
            dgvQueue.AllowUserToAddRows = false;
            dgvQueue.AllowUserToDeleteRows = false;
            dgvQueue.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvQueue.MultiSelect = false;
            dgvQueue.AutoGenerateColumns = false;
            dgvQueue.RowTemplate.Height = 32;

            colChk = new DataGridViewCheckBoxColumn();
            colChk.HeaderText = "เลือก";
            colChk.Width = 40;
            dgvQueue.Columns.Add(colChk);

            colAn = new DataGridViewTextBoxColumn();
            colAn.HeaderText = "AN";
            colAn.Width = 65;
            colAn.ReadOnly = true;
            dgvQueue.Columns.Add(colAn);

            colHn = new DataGridViewTextBoxColumn();
            colHn.HeaderText = "HN";
            colHn.Width = 65;
            colHn.ReadOnly = true;
            dgvQueue.Columns.Add(colHn);

            colName = new DataGridViewTextBoxColumn();
            colName.HeaderText = "ชื่อ-นามสกุล";
            colName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colName.ReadOnly = true;
            dgvQueue.Columns.Add(colName);

            colWard = new DataGridViewTextBoxColumn();
            colWard.HeaderText = "หอผู้ป่วย";
            colWard.Width = 85;
            colWard.ReadOnly = true;
            dgvQueue.Columns.Add(colWard);

            colAllergy = new DataGridViewTextBoxColumn();
            colAllergy.HeaderText = "แพ้ยา";
            colAllergy.Width = 65;
            colAllergy.ReadOnly = true;
            dgvQueue.Columns.Add(colAllergy);

            colCopies = new DataGridViewTextBoxColumn();
            colCopies.HeaderText = "ใบ";
            colCopies.Width = 32;
            colCopies.ReadOnly = true;
            dgvQueue.Columns.Add(colCopies);

            colStatus = new DataGridViewTextBoxColumn();
            colStatus.HeaderText = "สถานะ";
            colStatus.Width = 65;
            colStatus.ReadOnly = true;
            dgvQueue.Columns.Add(colStatus);

            dgvQueue.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    dgvQueue.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    bool val = Convert.ToBoolean(dgvQueue.Rows[e.RowIndex].Cells[0].Value);
                    if (e.RowIndex < _queue.Count)
                    {
                        _queue[e.RowIndex].IsSelected = val;
                        UpdateQueueCount();
                    }
                }
            };

            dgvQueue.SelectionChanged += (s, e) =>
            {
                if (dgvQueue.SelectedRows.Count > 0)
                {
                    int idx = dgvQueue.SelectedRows[0].Index;
                    if (idx >= 0 && idx < _queue.Count)
                    {
                        ShowPatientDetails(_queue[idx].Patient, _queue[idx].DataStore);
                    }
                }
            };

            pnlQueueContainer.Controls.Add(dgvQueue);
            dgvQueue.BringToFront();
        }

        private void BuildRightDetailsSection()
        {
            pnlRightContainer = new Panel();
            pnlRightContainer.Dock = DockStyle.Fill;
            pnlRightContainer.AutoScroll = true;

            // 1. Patient Card (Compact height 148px)
            pnlPatientCard = new Panel();
            pnlPatientCard.Dock = DockStyle.Top;
            pnlPatientCard.Height = 148;
            pnlPatientCard.BackColor = Color.White;
            pnlPatientCard.BorderStyle = BorderStyle.FixedSingle;
            pnlPatientCard.Padding = new Padding(8);
            pnlRightContainer.Controls.Add(pnlPatientCard);

            lblCardHeader = new Label();
            lblCardHeader.Text = "รายละเอียดผู้ป่วยที่เลือก:";
            lblCardHeader.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblCardHeader.ForeColor = Color.FromArgb(71, 85, 105);
            lblCardHeader.Location = new Point(10, 6);
            lblCardHeader.AutoSize = true;
            pnlPatientCard.Controls.Add(lblCardHeader);

            lblPtName = new Label();
            lblPtName.Text = "-";
            lblPtName.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            lblPtName.ForeColor = Color.FromArgb(15, 23, 42);
            lblPtName.Location = new Point(10, 22);
            lblPtName.AutoSize = true;
            pnlPatientCard.Controls.Add(lblPtName);

            lblPtHn = new Label();
            lblPtHn.Text = "HN: -";
            lblPtHn.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblPtHn.Location = new Point(10, 46);
            lblPtHn.AutoSize = true;
            pnlPatientCard.Controls.Add(lblPtHn);

            lblPtAn = new Label();
            lblPtAn.Text = "AN: -";
            lblPtAn.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblPtAn.ForeColor = Color.FromArgb(24, 76, 120);
            lblPtAn.Location = new Point(120, 46);
            lblPtAn.AutoSize = true;
            pnlPatientCard.Controls.Add(lblPtAn);

            lblPtGenderAge = new Label();
            lblPtGenderAge.Text = "เพศ/อายุ: -";
            lblPtGenderAge.Font = new Font("Segoe UI", 8.5F);
            lblPtGenderAge.Location = new Point(230, 46);
            lblPtGenderAge.AutoSize = true;
            pnlPatientCard.Controls.Add(lblPtGenderAge);

            lblPtWard = new Label();
            lblPtWard.Text = "หอผู้ป่วย: -";
            lblPtWard.Font = new Font("Segoe UI", 8.5F);
            lblPtWard.Location = new Point(10, 66);
            lblPtWard.AutoSize = true;
            pnlPatientCard.Controls.Add(lblPtWard);

            lblPtAdmitDate = new Label();
            lblPtAdmitDate.Text = "Admit: -";
            lblPtAdmitDate.Font = new Font("Segoe UI", 8.5F);
            lblPtAdmitDate.Location = new Point(230, 66);
            lblPtAdmitDate.AutoSize = true;
            pnlPatientCard.Controls.Add(lblPtAdmitDate);

            // Allergy Banner (Compact height 24px)
            pnlAllergy = new Panel();
            pnlAllergy.Location = new Point(10, 88);
            pnlAllergy.Size = new Size(350, 24);
            pnlAllergy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlAllergy.BackColor = Color.FromArgb(240, 242, 245);
            pnlPatientCard.Controls.Add(pnlAllergy);

            lblAllergyText = new Label();
            lblAllergyText.Text = "ประวัติแพ้ยา: -";
            lblAllergyText.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblAllergyText.Location = new Point(6, 3);
            lblAllergyText.AutoSize = true;
            pnlAllergy.Controls.Add(lblAllergyText);

            btnPrintSingle = new Button();
            btnPrintSingle.Text = "🖨️ พิมพ์คนนี้";
            btnPrintSingle.Location = new Point(10, 116);
            btnPrintSingle.Size = new Size(105, 26);
            btnPrintSingle.BackColor = Color.FromArgb(24, 76, 120);
            btnPrintSingle.ForeColor = Color.White;
            btnPrintSingle.FlatStyle = FlatStyle.Flat;
            btnPrintSingle.FlatAppearance.BorderSize = 0;
            btnPrintSingle.Cursor = Cursors.Hand;
            btnPrintSingle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnPrintSingle.Enabled = false;
            btnPrintSingle.Click += (s, e) => DoPrintSingle();
            pnlPatientCard.Controls.Add(btnPrintSingle);

            btnPreview = new Button();
            btnPreview.Text = "👁️ ตัวอย่าง";
            btnPreview.Location = new Point(122, 116);
            btnPreview.Size = new Size(85, 26);
            btnPreview.BackColor = Color.FromArgb(226, 232, 240);
            btnPreview.ForeColor = Color.FromArgb(51, 65, 85);
            btnPreview.FlatStyle = FlatStyle.Flat;
            btnPreview.FlatAppearance.BorderSize = 0;
            btnPreview.Cursor = Cursors.Hand;
            btnPreview.Font = new Font("Segoe UI", 8.5F);
            btnPreview.Enabled = false;
            btnPreview.Click += (s, e) => ShowPreview();
            pnlPatientCard.Controls.Add(btnPreview);

            // 2. Printer & Central Print Server Card (Height 165px)
            pnlPrinter = new Panel();
            pnlPrinter.Dock = DockStyle.Top;
            pnlPrinter.Height = 165;
            pnlPrinter.BackColor = Color.White;
            pnlPrinter.BorderStyle = BorderStyle.FixedSingle;
            pnlPrinter.Padding = new Padding(8);
            pnlRightContainer.Controls.Add(pnlPrinter);
            pnlPrinter.BringToFront();

            lblPrnTitle = new Label();
            lblPrnTitle.Text = "⚙️ การสั่งพิมพ์:";
            lblPrnTitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblPrnTitle.Location = new Point(10, 6);
            lblPrnTitle.AutoSize = true;
            pnlPrinter.Controls.Add(lblPrnTitle);

            rdoPrintLocal = new RadioButton();
            rdoPrintLocal.Text = "เครื่องนี้ (Local)";
            rdoPrintLocal.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            rdoPrintLocal.ForeColor = Color.FromArgb(24, 76, 120);
            rdoPrintLocal.Location = new Point(105, 4);
            rdoPrintLocal.Size = new Size(115, 22);
            rdoPrintLocal.Checked = true;
            rdoPrintLocal.CheckedChanged += (s, e) => TogglePrintModeUI();
            pnlPrinter.Controls.Add(rdoPrintLocal);

            rdoPrintRemote = new RadioButton();
            rdoPrintRemote.Text = "📡 เครื่องแม่ในวอร์ด (Server)";
            rdoPrintRemote.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            rdoPrintRemote.ForeColor = Color.FromArgb(4, 120, 87);
            rdoPrintRemote.Location = new Point(220, 4);
            rdoPrintRemote.Size = new Size(175, 22);
            rdoPrintRemote.CheckedChanged += (s, e) => TogglePrintModeUI();
            pnlPrinter.Controls.Add(rdoPrintRemote);

            // Panel for Local Printer Selection
            pnlLocalPrinter = new Panel();
            pnlLocalPrinter.Location = new Point(10, 26);
            pnlLocalPrinter.Size = new Size(350, 28);
            pnlLocalPrinter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlPrinter.Controls.Add(pnlLocalPrinter);

            cboPrinters = new ComboBox();
            cboPrinters.Dock = DockStyle.Fill;
            cboPrinters.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPrinters.Font = new Font("Segoe UI", 8.5F);
            cboPrinters.SelectedIndexChanged += (s, e) =>
            {
                if (rdoPrintLocal.Checked && cboPrinters.SelectedItem != null)
                {
                    lblCurrentPrinterStatus.Text = "เครื่องพิมพ์: " + cboPrinters.SelectedItem;
                }
            };
            pnlLocalPrinter.Controls.Add(cboPrinters);

            // Panel for Remote Print Server Selection
            pnlRemotePrinter = new Panel();
            pnlRemotePrinter.Location = new Point(10, 26);
            pnlRemotePrinter.Size = new Size(350, 48);
            pnlRemotePrinter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlRemotePrinter.Visible = false;
            pnlPrinter.Controls.Add(pnlRemotePrinter);

            txtRemoteHost = new TextBox();
            txtRemoteHost.Location = new Point(0, 2);
            txtRemoteHost.Size = new Size(155, 22);
            txtRemoteHost.Font = new Font("Segoe UI", 8.5F);
            toolTips.SetToolTip(txtRemoteHost, "ระบุ IP หรือชื่อคอมพิวเตอร์เครื่องแม่ในวอร์ด (เช่น 172.30.249.158 หรือ HP600G6-009)");
            pnlRemotePrinter.Controls.Add(txtRemoteHost);

            btnDiscoverServer = new Button();
            btnDiscoverServer.Text = "🔍 ค้นหาเครื่องแม่";
            btnDiscoverServer.Location = new Point(160, 0);
            btnDiscoverServer.Size = new Size(108, 25);
            btnDiscoverServer.BackColor = Color.FromArgb(241, 245, 249);
            btnDiscoverServer.FlatStyle = FlatStyle.Flat;
            btnDiscoverServer.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnDiscoverServer.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnDiscoverServer.Cursor = Cursors.Hand;
            btnDiscoverServer.Click += (s, e) => DoDiscoverServer();
            toolTips.SetToolTip(btnDiscoverServer, "ค้นหาเครื่องแม่ในวงแลนของวอร์ดอัตโนมัติ (Auto-Discovery)");
            pnlRemotePrinter.Controls.Add(btnDiscoverServer);

            btnTestServer = new Button();
            btnTestServer.Text = "⚡ ทดสอบ";
            btnTestServer.Location = new Point(272, 0);
            btnTestServer.Size = new Size(76, 25);
            btnTestServer.BackColor = Color.FromArgb(241, 245, 249);
            btnTestServer.FlatStyle = FlatStyle.Flat;
            btnTestServer.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnTestServer.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnTestServer.Cursor = Cursors.Hand;
            btnTestServer.Click += (s, e) => DoTestServerConnection();
            toolTips.SetToolTip(btnTestServer, "ทดสอบการเชื่อมต่อไปยังเครื่องแม่");
            pnlRemotePrinter.Controls.Add(btnTestServer);

            lblRemoteStatus = new Label();
            lblRemoteStatus.Text = "พร้อมระบุ IP เครื่องแม่";
            lblRemoteStatus.Font = new Font("Segoe UI", 8F);
            lblRemoteStatus.ForeColor = Color.FromArgb(100, 116, 139);
            lblRemoteStatus.Location = new Point(0, 28);
            lblRemoteStatus.AutoSize = true;
            pnlRemotePrinter.Controls.Add(lblRemoteStatus);

            // Row 3: Print Options (Shared by both modes)
            rdoAdult = new RadioButton();
            rdoAdult.Text = "ผู้ใหญ่";
            rdoAdult.Font = new Font("Segoe UI", 8.5F);
            rdoAdult.Location = new Point(10, 78);
            rdoAdult.Size = new Size(68, 22);
            rdoAdult.Checked = true;
            pnlPrinter.Controls.Add(rdoAdult);

            rdoChild = new RadioButton();
            rdoChild.Text = "เด็ก";
            rdoChild.Font = new Font("Segoe UI", 8.5F);
            rdoChild.Location = new Point(80, 78);
            rdoChild.Size = new Size(55, 22);
            pnlPrinter.Controls.Add(rdoChild);

            lblCopies = new Label();
            lblCopies.Text = "จำนวน:";
            lblCopies.Font = new Font("Segoe UI", 8.5F);
            lblCopies.Location = new Point(145, 80);
            lblCopies.AutoSize = true;
            pnlPrinter.Controls.Add(lblCopies);

            numCopies = new NumericUpDown();
            numCopies.Location = new Point(190, 78);
            numCopies.Size = new Size(45, 22);
            numCopies.Font = new Font("Segoe UI", 8.5F);
            numCopies.Minimum = 1;
            numCopies.Maximum = 10;
            numCopies.Value = 1;
            pnlPrinter.Controls.Add(numCopies);

            btnSavePrinter = new Button();
            btnSavePrinter.Text = "💾 บันทึกเริ่มต้น";
            btnSavePrinter.Location = new Point(245, 76);
            btnSavePrinter.Size = new Size(100, 26);
            btnSavePrinter.BackColor = Color.FromArgb(241, 245, 249);
            btnSavePrinter.FlatStyle = FlatStyle.Flat;
            btnSavePrinter.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnSavePrinter.Cursor = Cursors.Hand;
            btnSavePrinter.Font = new Font("Segoe UI", 8.5F);
            btnSavePrinter.Click += (s, e) => SavePrinterConfig();
            toolTips.SetToolTip(btnSavePrinter, "บันทึกการตั้งค่าการพิมพ์เป็นค่าเริ่มต้นของโปรแกรม");
            pnlPrinter.Controls.Add(btnSavePrinter);

            // Row 4: Checkbox
            chkAutoPrint = new CheckBox();
            chkAutoPrint.Text = "⚡ พิมพ์ทันทีเมื่อยิง HN/AN (Auto-print)";
            chkAutoPrint.Font = new Font("Segoe UI", 8.5F);
            chkAutoPrint.ForeColor = Color.FromArgb(71, 85, 105);
            chkAutoPrint.Location = new Point(10, 108);
            chkAutoPrint.AutoSize = true;
            chkAutoPrint.CheckedChanged += (s, e) =>
            {
                _config.AutoPrintOnScan = chkAutoPrint.Checked;
                _config.Save();
            };
            pnlPrinter.Controls.Add(chkAutoPrint);

            // Row 5: Server Notice
            lblServerNotice = new Label();
            lblServerNotice.Text = "💡 เครื่องนี้เปิด Print Server รับคำสั่งพิมพ์จากเครื่องอื่นในวอร์ดอัตโนมัติ";
            lblServerNotice.Font = new Font("Segoe UI", 7.5F);
            lblServerNotice.ForeColor = Color.FromArgb(100, 116, 139);
            lblServerNotice.Location = new Point(10, 134);
            lblServerNotice.AutoSize = true;
            pnlPrinter.Controls.Add(lblServerNotice);

            // 3. History Panel
            pnlHist = new Panel();
            pnlHist.Dock = DockStyle.Fill;
            pnlHist.BackColor = Color.White;
            pnlHist.BorderStyle = BorderStyle.FixedSingle;
            pnlHist.Padding = new Padding(8);
            pnlRightContainer.Controls.Add(pnlHist);
            pnlHist.BringToFront();

            lblHistTitle = new Label();
            lblHistTitle.Text = "📋 ประวัติพิมพ์ล่าสุด:";
            lblHistTitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblHistTitle.Location = new Point(8, 8);
            lblHistTitle.AutoSize = true;
            pnlHist.Controls.Add(lblHistTitle);

            btnOpenAllHistory = new Button();
            btnOpenAllHistory.Text = "📜 ดูประวัติทั้งหมด / ค้นหา (F4)";
            btnOpenAllHistory.Size = new Size(185, 26);
            btnOpenAllHistory.Location = new Point(pnlHist.Width - 195, 4);
            btnOpenAllHistory.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpenAllHistory.BackColor = Color.FromArgb(241, 245, 249);
            btnOpenAllHistory.FlatStyle = FlatStyle.Flat;
            btnOpenAllHistory.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnOpenAllHistory.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnOpenAllHistory.ForeColor = Color.FromArgb(24, 76, 120);
            btnOpenAllHistory.Cursor = Cursors.Hand;
            btnOpenAllHistory.Click += (s, e) => OpenHistoryDialog();
            toolTips.SetToolTip(btnOpenAllHistory, "เปิดหน้าต่างประวัติการพิมพ์ย้อนหลังทั้งหมด และสั่งพิมพ์ซ้ำ (F4)");
            pnlHist.Controls.Add(btnOpenAllHistory);

            lstHistory = new ListView();
            lstHistory.Location = new Point(8, 34);
            lstHistory.Size = new Size(pnlHist.Width - 16, 120);
            lstHistory.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstHistory.View = View.Details;
            lstHistory.FullRowSelect = true;
            lstHistory.GridLines = true;
            lstHistory.Columns.Add("เวลา", 60);
            lstHistory.Columns.Add("AN", 70);
            lstHistory.Columns.Add("HN", 70);
            lstHistory.Columns.Add("ชื่อผู้ป่วย", 130);
            lstHistory.Columns.Add("หอผู้ป่วย", 90);
            lstHistory.DoubleClick += (s, e) =>
            {
                if (lstHistory.SelectedItems.Count > 0)
                {
                    txtSearch.Text = lstHistory.SelectedItems[0].SubItems[1].Text;
                    DoSearchAndAddQueue();
                }
            };
            pnlHist.Controls.Add(lstHistory);
        }

        private void TogglePrintModeUI()
        {
            if (rdoPrintRemote.Checked)
            {
                pnlLocalPrinter.Visible = false;
                pnlRemotePrinter.Visible = true;
                lblServerNotice.Visible = false;
                lblCurrentPrinterStatus.Text = string.Format("เครื่องพิมพ์: [เครื่องแม่: {0}]", 
                    string.IsNullOrEmpty(txtRemoteHost.Text) ? "ยังไม่ระบุ" : txtRemoteHost.Text);
            }
            else
            {
                pnlLocalPrinter.Visible = true;
                pnlRemotePrinter.Visible = false;
                lblServerNotice.Visible = true;
                if (cboPrinters.SelectedItem != null)
                {
                    lblCurrentPrinterStatus.Text = "เครื่องพิมพ์: " + cboPrinters.SelectedItem;
                }
            }
        }

        private void LoadPrintModeConfig()
        {
            if (_config.PrintMode == "Remote")
            {
                rdoPrintRemote.Checked = true;
                txtRemoteHost.Text = _config.RemoteServerHost ?? "";
            }
            else
            {
                rdoPrintLocal.Checked = true;
                txtRemoteHost.Text = _config.RemoteServerHost ?? "";
            }
            TogglePrintModeUI();
        }

        private void DoDiscoverServer()
        {
            this.Cursor = Cursors.WaitCursor;
            lblRemoteStatus.ForeColor = Color.FromArgb(24, 76, 120);
            lblRemoteStatus.Text = "กำลังค้นหาเครื่องแม่ในวอร์ด...";
            Application.DoEvents();

            try
            {
                var discovered = PrintClientService.Instance.DiscoverServer(2500);
                if (discovered != null)
                {
                    txtRemoteHost.Text = discovered.IP;
                    lblRemoteStatus.ForeColor = Color.FromArgb(16, 149, 106);
                    lblRemoteStatus.Text = string.Format("✓ พบ: {0} ({1}) เครื่องพิมพ์: {2}", 
                        discovered.MachineName, discovered.IP, discovered.PrinterName);
                    lblCurrentPrinterStatus.Text = string.Format("เครื่องพิมพ์: [เครื่องแม่: {0}]", discovered.IP);
                    System.Media.SystemSounds.Asterisk.Play();
                }
                else
                {
                    lblRemoteStatus.ForeColor = Color.Crimson;
                    lblRemoteStatus.Text = "ไม่พบเครื่องแม่ผ่านการค้นหาอัตโนมัติ (สามารถพิมพ์ระบุ IP หรือชื่อเครื่องด้วยตนเองได้)";
                }
            }
            catch (Exception ex)
            {
                lblRemoteStatus.ForeColor = Color.Crimson;
                lblRemoteStatus.Text = "ค้นหาผิดพลาด: " + ex.Message;
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void DoTestServerConnection()
        {
            string host = txtRemoteHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("กรุณาระบุ IP หรือชื่อคอมพิวเตอร์เครื่องแม่ก่อนกดทดสอบ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRemoteHost.Focus();
                return;
            }

            this.Cursor = Cursors.WaitCursor;
            lblRemoteStatus.ForeColor = Color.FromArgb(24, 76, 120);
            lblRemoteStatus.Text = "กำลังทดสอบเชื่อมต่อ...";
            Application.DoEvents();

            try
            {
                int port = _config.RemoteServerPort > 0 ? _config.RemoteServerPort : 8099;
                var stat = PrintClientService.Instance.TestConnection(host, port);
                lblRemoteStatus.ForeColor = Color.FromArgb(16, 149, 106);
                lblRemoteStatus.Text = string.Format("✓ เชื่อมต่อสำเร็จ: {0} (เครื่องพิมพ์: {1})", 
                    stat.MachineName, stat.SelectedPrinter ?? "พร้อม");
                lblCurrentPrinterStatus.Text = string.Format("เครื่องพิมพ์: [เครื่องแม่: {0}]", host);
                MessageBox.Show(string.Format("เชื่อมต่อไปยังเครื่องแม่สำเร็จ!\n\nชื่อเครื่อง: {0}\nIP: {1}\nเครื่องพิมพ์: {2}\nสถานะ: ออนไลน์พร้อมพิมพ์",
                    stat.MachineName, stat.IP, stat.SelectedPrinter), "เชื่อมต่อสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblRemoteStatus.ForeColor = Color.Crimson;
                lblRemoteStatus.Text = "เชื่อมต่อล้มเหลว: " + ex.Message;
                MessageBox.Show("ไม่สามารถเชื่อมต่อกับเครื่องแม่ได้:\n" + ex.Message + "\n\nโปรดตรวจสอบว่า:\n1. เครื่องแม่เปิดโปรแกรม TUH Wristband Printer อยู่หรือไม่\n2. หมายเลข IP ถูกต้องและต่อวงแลนเดียวกันหรือไม่", 
                    "เชื่อมต่อล้มเหลว", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void StartPrintServerIfEnabled()
        {
            if (_config.EnablePrintServer)
            {
                try
                {
                    int port = _config.ServerPort > 0 ? _config.ServerPort : 8099;
                    PrintServerService.Instance.Start(port, HandleRemotePrintRequest, HandleServerStatusRequest);
                }
                catch (Exception ex)
                {
                    lblServerStatusStrip.Text = "Print Server: ผิดพลาด (" + ex.Message + ")";
                }
            }
        }

        private void UpdateServerBadge()
        {
            if (PrintServerService.Instance.IsRunning)
            {
                string ip = PrintServerService.GetLocalIPAddress();
                btnServerBadge.Text = "📡 Server: " + ip;
                btnServerBadge.BackColor = Color.FromArgb(4, 120, 87); // Emerald
                lblServerStatusStrip.Text = string.Format("Print Server: ออนไลน์ (IP: {0}:{1})", 
                    ip, PrintServerService.Instance.Port);
            }
            else
            {
                btnServerBadge.Text = "📡 Print Server: ปิด";
                btnServerBadge.BackColor = Color.FromArgb(100, 116, 139); // Slate
                lblServerStatusStrip.Text = "Print Server: ปิดใช้งาน";
            }
        }

        private PrintResponsePayload HandleRemotePrintRequest(PrintRequestPayload req)
        {
            PrintResponsePayload resp = new PrintResponsePayload { AN = req.AN };
            try
            {
                WristbandType type = (req.Type == "Child" || req.Type == "เด็ก") ? WristbandType.Child : WristbandType.Adult;
                DataStore ds;
                PatientAdmission pt = EphisService.Instance.LoadWristband(req.AN, type, out ds);

                string prn = "";
                this.Invoke((MethodInvoker)delegate
                {
                    prn = cboPrinters.SelectedItem != null ? cboPrinters.SelectedItem.ToString() : "";
                });

                if (string.IsNullOrEmpty(prn)) prn = _config.SelectedPrinter;

                int copies = req.Copies > 0 ? req.Copies : 1;
                EphisService.Instance.PrintWristband(ds, prn, copies);

                // Record to history on server machine
                PrintHistoryRecord hist = new PrintHistoryRecord();
                hist.AN = pt.AN;
                hist.HN = pt.HN;
                hist.PatientName = pt.PatientName;
                hist.Gender = pt.Gender;
                hist.Age = pt.Age;
                hist.WardName = pt.WardName;
                hist.RgtDate = pt.RgtDate;
                hist.Allergy = pt.Allergy;
                hist.WristbandType = (type == WristbandType.Adult) ? "ผู้ใหญ่" : "เด็ก";
                hist.Copies = copies;
                hist.PrinterName = prn;
                hist.PrintedBy = string.Format("{0} [ส่งจาก: {1}]", req.ClientUser ?? "เครื่องลูก", req.ClientMachine ?? "เครือข่าย");
                hist.Status = "พิมพ์จากเครื่องลูก";
                PrintHistoryService.Instance.AddRecord(hist);

                // Update UI on host machine
                this.BeginInvoke((MethodInvoker)delegate
                {
                    LoadRecentHistoryList();
                    lblStatusMsg.Text = string.Format("✓ ได้รับคำสั่งพิมพ์ AN: {0} จากเครื่อง [{1}] สำเร็จแล้ว", req.AN, req.ClientMachine);
                    System.Media.SystemSounds.Asterisk.Play();
                });

                resp.Success = true;
                resp.PatientName = pt.PatientName;
                resp.Message = string.Format("พิมพ์สายรัดข้อมือ AN: {0} ({1}) สำเร็จแล้ว", req.AN, pt.PatientName);
            }
            catch (Exception ex)
            {
                resp.Success = false;
                resp.Message = "พิมพ์ล้มเหลว: " + ex.Message;
            }
            return resp;
        }

        private ServerStatusPayload HandleServerStatusRequest()
        {
            string prn = "";
            this.Invoke((MethodInvoker)delegate
            {
                prn = cboPrinters.SelectedItem != null ? cboPrinters.SelectedItem.ToString() : "";
            });
            return new ServerStatusPayload
            {
                Status = "online",
                MachineName = Environment.MachineName,
                IP = PrintServerService.GetLocalIPAddress(),
                Port = PrintServerService.Instance.Port,
                SelectedPrinter = prn,
                Version = UpdateService.CurrentVersion
            };
        }

        private void ShowServerInfoDialog()
        {
            string ip = PrintServerService.GetLocalIPAddress();
            string machine = Environment.MachineName;
            int port = PrintServerService.Instance.Port;
            string prn = cboPrinters.SelectedItem != null ? cboPrinters.SelectedItem.ToString() : (_config.SelectedPrinter ?? "-");

            string msg = string.Format(
                "🏥 ข้อมูลระบบพิมพ์ส่วนกลาง (Central Print Server)\n\n" +
                "• คอมพิวเตอร์เครื่องนี้ (Host Name):  {0}\n" +
                "• หมายเลข IP เครื่องนี้:  {1}\n" +
                "• พอร์ตบริการ (Port):  {2}\n" +
                "• เครื่องพิมพ์ประจำเครื่อง:  {3}\n" +
                "• สถานะ:  {4}\n\n" +
                "📖 วิธีให้คอมเครื่องอื่นในวอร์ดพิมพ์มาที่เครื่องนี้:\n" +
                "1. เปิดโปรแกรม TUH Wristband Printer ที่เครื่องอื่น\n" +
                "2. ที่กล่องการสั่งพิมพ์ เลือก '📡 เครื่องแม่ในวอร์ด (Server)'\n" +
                "3. กดปุ่ม '🔍 ค้นหาเครื่องแม่' (หรือพิมพ์ IP: {1})\n" +
                "4. สามารถยิงบาร์โค้ดและกดพิมพ์มาที่เครื่องนี้ได้ทันที!\n\n" +
                "ต้องการคัดลอก IP Address ({1}) ลง Clipboard หรือไม่?",
                machine, ip, port, prn, PrintServerService.Instance.IsRunning ? "🟢 ออนไลน์ (พร้อมรับคำสั่งพิมพ์)" : "🔴 ปิด"
            );

            if (MessageBox.Show(msg, "ข้อมูล Central Print Server", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try
                {
                    Clipboard.SetText(ip);
                    MessageBox.Show("คัดลอกหมายเลข IP: " + ip + " เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { }
            }
        }

        private void SetSidebarMode(bool isSidebar)
        {
            _isSidebarMode = isSidebar;
            _config.WindowMode = isSidebar ? "Sidebar" : "Normal";
            _config.Save();

            this.SuspendLayout();
            midSplit.SuspendLayout();
            midSplit.Controls.Clear();

            Rectangle workArea = Screen.FromControl(this).WorkingArea;

            if (isSidebar)
            {
                // SIDEBAR MODE: Docks cleanly on right, adapts to full screen height
                int sidebarWidth = Math.Min(430, Math.Max(380, workArea.Width / 3));
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.MinimumSize = new Size(360, 480);
                this.MaximumSize = new Size(550, workArea.Height);
                this.Size = new Size(sidebarWidth, workArea.Height);
                this.Location = new Point(workArea.Right - sidebarWidth, workArea.Top);

                btnToggleSidebar.Text = "⤢ ขยายเต็ม";
                lblAppTitle.Text = "🏥 TUH Wristband";
                lblAppSub.Visible = false;
                lblUser.Visible = false;
                topBar.Height = 48;

                chkAlwaysOnTop.Checked = true;
                this.TopMost = true;

                // Hide less essential columns in compact sidebar so no horizontal scrolling occurs
                colHn.Visible = false;
                colWard.Visible = false;
                colAllergy.Visible = false;
                colChk.Width = 35;
                colAn.Width = 62;
                colCopies.Width = 32;
                colStatus.Width = 60;
                colName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                // Vertical Single-Column Split: 53% Queue, 47% Details
                midSplit.ColumnCount = 1;
                midSplit.RowCount = 2;
                midSplit.ColumnStyles.Clear();
                midSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                midSplit.RowStyles.Clear();
                midSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 53F));
                midSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 47F));

                midSplit.Controls.Add(pnlQueueContainer, 0, 0);
                midSplit.Controls.Add(pnlRightContainer, 0, 1);

                pnlHist.Visible = false;
            }
            else
            {
                // NORMAL MODE: 2 Columns Side-by-Side
                this.MaximumSize = Size.Empty;
                this.MinimumSize = new Size(800, 600);
                this.FormBorderStyle = FormBorderStyle.Sizable;

                int normW = Math.Min(1100, workArea.Width - 40);
                int normH = Math.Min(760, workArea.Height - 40);
                this.Size = new Size(normW, normH);
                this.Location = new Point(
                    workArea.Left + (workArea.Width - normW) / 2,
                    workArea.Top + (workArea.Height - normH) / 2
                );

                btnToggleSidebar.Text = "📌 แถบข้าง";
                lblAppTitle.Text = "🏥 ระบบพิมพ์สายรัดข้อมือผู้ป่วย (TUH Wristband)";
                lblAppSub.Visible = true;
                lblUser.Visible = true;
                topBar.Height = 60;

                // Restore all columns
                colHn.Visible = true;
                colWard.Visible = true;
                colAllergy.Visible = true;
                colChk.Width = 40;
                colAn.Width = 65;
                colHn.Width = 65;
                colWard.Width = 85;
                colAllergy.Width = 65;
                colCopies.Width = 32;
                colStatus.Width = 65;
                colName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                // 2-Column Split: 58% Queue, 42% Details
                midSplit.ColumnCount = 2;
                midSplit.RowCount = 1;
                midSplit.RowStyles.Clear();
                midSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                midSplit.ColumnStyles.Clear();
                midSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
                midSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

                midSplit.Controls.Add(pnlQueueContainer, 0, 0);
                midSplit.Controls.Add(pnlRightContainer, 1, 0);

                pnlHist.Visible = true;
            }

            UpdateLayoutSizes();
            midSplit.ResumeLayout(true);
            this.ResumeLayout(true);
            RefreshQueueGrid();
        }

        private void UpdateLayoutSizes()
        {
            if (topBar == null || pnlSearch == null || topQueue == null) return;

            // 1. Top Bar dynamic layout (guarantees zero overlap)
            int tbW = topBar.ClientSize.Width;
            btnLogout.Location = new Point(tbW - 66, 12);
            btnToggleSidebar.Location = new Point(btnLogout.Left - btnToggleSidebar.Width - 8, 12);
            chkAlwaysOnTop.Location = new Point(btnToggleSidebar.Left - chkAlwaysOnTop.Width - 6, 16);

            if (_isSidebarMode || tbW < 520)
            {
                lblUser.Visible = false;
                lblAppSub.Visible = false;
                lblAppTitle.Text = "🏥 TUH Wristband";
                lblAppTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

                btnTopHistory.Size = new Size(68, 28);
                btnTopHistory.Text = "📜 ประวัติ";
                btnTopHistory.Location = new Point(chkAlwaysOnTop.Left - btnTopHistory.Width - 6, 10);

                btnServerBadge.Size = new Size(80, 28);
                btnServerBadge.Text = "📡 Server";
                btnServerBadge.Location = new Point(btnTopHistory.Left - btnServerBadge.Width - 6, 10);

                btnCheckUpdate.Size = new Size(65, 28);
                btnCheckUpdate.Location = new Point(btnServerBadge.Left - btnCheckUpdate.Width - 6, 10);
            }
            else
            {
                lblAppTitle.Text = "🏥 ระบบพิมพ์สายรัดข้อมือผู้ป่วย (TUH Wristband)";
                lblAppTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
                lblAppSub.Visible = true;

                btnTopHistory.Size = new Size(100, 32);
                btnTopHistory.Text = "📜 ประวัติพิมพ์";
                btnTopHistory.Location = new Point(chkAlwaysOnTop.Left - btnTopHistory.Width - 8, 12);

                btnServerBadge.Size = new Size(150, 32);
                btnServerBadge.Text = PrintServerService.Instance.IsRunning 
                    ? ("📡 Server: " + PrintServerService.GetLocalIPAddress()) 
                    : "📡 Print Server";
                btnServerBadge.Location = new Point(btnTopHistory.Left - btnServerBadge.Width - 8, 12);

                btnCheckUpdate.Size = new Size(95, 32);
                btnCheckUpdate.Location = new Point(btnServerBadge.Left - btnCheckUpdate.Width - 8, 12);

                if (btnCheckUpdate.Left - lblAppTitle.Right > 130)
                {
                    lblUser.Visible = true;
                    lblUser.Location = new Point(btnCheckUpdate.Left - lblUser.Width - 8, 17);
                }
                else
                {
                    lblUser.Visible = false;
                }
            }

            // 2. Search Panel responsive sizing
            int sW = pnlSearch.ClientSize.Width;
            if (_isSidebarMode || sW < 640)
            {
                // Compact search layout
                lblSearchHint.Visible = false;
                btnHistorySearch.Visible = false;

                lblBarcodeBadge.Size = new Size(54, 36);
                lblBarcodeBadge.Location = new Point(8, 28);
                lblBarcodeBadge.Text = "HN/AN";
                lblBarcodeBadge.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

                int btnW = 72;
                btnSearch.Size = new Size(btnW, 36);
                btnSearch.Text = "＋ คิว";
                btnClear.Size = new Size(btnW, 36);
                btnClear.Text = "✕ ล้าง";

                int searchBoxW = Math.Max(95, sW - lblBarcodeBadge.Width - (btnW * 2) - 34);
                txtSearch.Location = new Point(lblBarcodeBadge.Right + 4, 28);
                txtSearch.Size = new Size(searchBoxW, 36);

                btnSearch.Location = new Point(txtSearch.Right + 5, 28);
                btnClear.Location = new Point(btnSearch.Right + 5, 28);

                if (cboAdmissions.Visible)
                {
                    pnlSearch.Height = 112;
                    lblAdmissionsPrompt.Location = new Point(10, 72);
                    cboAdmissions.Location = new Point(95, 70);
                    cboAdmissions.Size = new Size(sW - 105, 26);
                }
                else
                {
                    pnlSearch.Height = 76;
                }
            }
            else
            {
                // Full search layout
                lblSearchHint.Visible = true;
                lblSearchHint.Location = new Point(lblSearchTitle.Right + 12, 8);
                btnHistorySearch.Visible = true;

                lblBarcodeBadge.Size = new Size(72, 38);
                lblBarcodeBadge.Location = new Point(12, 29);
                lblBarcodeBadge.Text = "HN / AN";
                lblBarcodeBadge.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

                txtSearch.Location = new Point(lblBarcodeBadge.Right + 4, 30);
                txtSearch.Size = new Size(250, 36);

                btnSearch.Size = new Size(135, 38);
                btnSearch.Text = "＋ เพิ่มเข้าคิว [Enter]";
                btnSearch.Location = new Point(txtSearch.Right + 8, 29);

                btnClear.Size = new Size(115, 38);
                btnClear.Text = "✕ ล้างข้อมูล [Esc]";
                btnClear.Location = new Point(btnSearch.Right + 8, 29);

                btnHistorySearch.Location = new Point(btnClear.Right + 8, 29);
                btnHistorySearch.Size = new Size(125, 38);

                pnlSearch.Height = 84;

                if (cboAdmissions.Visible)
                {
                    lblAdmissionsPrompt.Location = new Point(btnHistorySearch.Right + 16, 8);
                    cboAdmissions.Location = new Point(btnHistorySearch.Right + 16, 30);
                    cboAdmissions.Size = new Size(Math.Max(200, sW - (btnHistorySearch.Right + 30)), 28);
                }
            }

            // 3. Queue header buttons alignment
            int qW = topQueue.ClientSize.Width;
            btnClearQueue.Location = new Point(qW - btnClearQueue.Width - 10, 8);
            btnRemoveSelected.Location = new Point(btnClearQueue.Left - btnRemoveSelected.Width - 6, 8);

            // 4. Compact Right Details responsive sizing
            int rW = pnlRightContainer.ClientSize.Width;
            if (rW > 0)
            {
                pnlLocalPrinter.Width = Math.Max(180, rW - 20);
                pnlRemotePrinter.Width = Math.Max(180, rW - 20);
                pnlAllergy.Width = Math.Max(180, rW - 20);
                lstHistory.Width = Math.Max(180, rW - 20);
            }
        }

        private void ClearSearchAndActivePatient()
        {
            txtSearch.Text = "";
            lblAdmissionsPrompt.Visible = false;
            cboAdmissions.Visible = false;
            cboAdmissions.Items.Clear();

            if (_isSidebarMode)
            {
                pnlSearch.Height = 76;
            }
            else
            {
                pnlSearch.Height = 84;
            }

            ResetPatientView();

            lblStatusMsg.Text = "✓ ล้างข้อมูลเรียบร้อยแล้ว - พร้อมรับการยิงบาร์โค้ดใหม่";
            txtSearch.Focus();
        }

        private void DoSearchAndAddQueue()
        {
            string input = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                txtSearch.Focus();
                return;
            }

            // FAST-PATH: Check in-memory queue first for instant duplicate detection!
            long searchNum = 0;
            long.TryParse(input, out searchNum);
            if (searchNum > 0)
            {
                QueueItem existingInQueue = _queue.Find(q => q.Patient.AN == searchNum || q.Patient.HN == searchNum);
                if (existingInQueue != null)
                {
                    SelectAndHighlightExistingQueueItem(existingInQueue);
                    txtSearch.Text = "";
                    txtSearch.Focus();
                    return;
                }
            }

            lblStatusMsg.Text = string.Format("กำลังค้นหาข้อมูล: {0}...", input);
            this.Cursor = Cursors.WaitCursor;
            Application.DoEvents();

            try
            {
                _recentAdmissions = EphisService.Instance.SearchAdmissions(input);
                if (_recentAdmissions.Count == 0)
                {
                    MessageBox.Show(string.Format("ไม่พบข้อมูลการ Admit สำหรับหมายเลข: {0}", input),
                        "ไม่พบข้อมูล", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtSearch.SelectAll();
                    return;
                }

                if (_recentAdmissions.Count == 1)
                {
                    lblAdmissionsPrompt.Visible = false;
                    cboAdmissions.Visible = false;
                    if (_isSidebarMode) pnlSearch.Height = 76;
                    AddAdmissionToQueue(_recentAdmissions[0].AN);
                }
                else
                {
                    // Multiple admissions found - display dropdown cleanly
                    lblAdmissionsPrompt.Visible = true;
                    cboAdmissions.Visible = true;
                    cboAdmissions.Items.Clear();
                    foreach (var a in _recentAdmissions)
                    {
                        cboAdmissions.Items.Add(string.Format("AN: {0} | {1} | {2}", 
                            a.AN, a.RgtDate, a.WardName ?? "-"));
                    }

                    UpdateLayoutSizes();
                    cboAdmissions.SelectedIndex = 0; // Default to latest admission
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ค้นหาผิดพลาด: " + ex.Message, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void cboAdmissions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboAdmissions.SelectedIndex >= 0 && cboAdmissions.SelectedIndex < _recentAdmissions.Count)
            {
                AddAdmissionToQueue(_recentAdmissions[cboAdmissions.SelectedIndex].AN);
                cboAdmissions.Visible = false;
                lblAdmissionsPrompt.Visible = false;
                if (_isSidebarMode) pnlSearch.Height = 76;
            }
        }

        private void AddAdmissionToQueue(long an)
        {
            // Check if patient AN already in queue! Keep ONLY ONE instance!
            QueueItem existing = _queue.Find(q => q.Patient.AN == an);
            if (existing != null)
            {
                SelectAndHighlightExistingQueueItem(existing);
                txtSearch.Text = "";
                txtSearch.Focus();
                return;
            }

            // Load from database
            WristbandType type = rdoAdult.Checked ? WristbandType.Adult : WristbandType.Child;
            DataStore ds;
            PatientAdmission patient = EphisService.Instance.LoadWristband(an, type, out ds);

            // Supplement with search admission info if any field is empty
            PatientAdmission searched = _recentAdmissions != null ? _recentAdmissions.Find(a => a.AN == an) : null;
            if (searched != null)
            {
                if (patient.HN <= 0) patient.HN = searched.HN;
                if (string.IsNullOrEmpty(patient.PatientName)) patient.PatientName = searched.PatientName;
                if (string.IsNullOrEmpty(patient.WardName)) patient.WardName = searched.WardName;
                if (string.IsNullOrEmpty(patient.RgtDate)) patient.RgtDate = searched.RgtDate;
            }

            // Double check by HN as well (prevent duplicate row for same patient under another admission)
            QueueItem existingHn = _queue.Find(q => q.Patient.HN == patient.HN);
            if (existingHn != null)
            {
                SelectAndHighlightExistingQueueItem(existingHn);
                txtSearch.Text = "";
                txtSearch.Focus();
                return;
            }

            QueueItem item = new QueueItem();
            item.Patient = patient;
            item.DataStore = ds;
            item.Type = type;
            item.Copies = (int)numCopies.Value;
            item.IsSelected = true;

            _queue.Add(item);

            ShowPatientDetails(patient, ds);
            RefreshQueueGrid();

            // Select newly added row
            int newIdx = _queue.Count - 1;
            if (newIdx >= 0 && newIdx < dgvQueue.Rows.Count)
            {
                dgvQueue.ClearSelection();
                dgvQueue.Rows[newIdx].Selected = true;
                dgvQueue.FirstDisplayedScrollingRowIndex = newIdx;
            }

            lblStatusMsg.Text = string.Format("✓ เพิ่ม {0} (AN: {1}) เข้าคิวพิมพ์แล้ว", patient.PatientName, patient.AN);

            // Auto print on scan if enabled
            if (chkAutoPrint.Checked)
            {
                DoPrintSingle();
            }

            txtSearch.Text = "";
            txtSearch.Focus();
        }

        private void SelectAndHighlightExistingQueueItem(QueueItem existing)
        {
            int idx = _queue.IndexOf(existing);
            if (idx >= 0 && idx < dgvQueue.Rows.Count)
            {
                dgvQueue.ClearSelection();
                dgvQueue.Rows[idx].Selected = true;
                dgvQueue.FirstDisplayedScrollingRowIndex = idx;

                ShowPatientDetails(existing.Patient, existing.DataStore);

                FlashRow(idx);

                lblStatusMsg.Text = string.Format("⚠️ ผู้ป่วย: {0} (AN: {1}) มีอยู่ในคิวแล้ว — แสดงเพียง 1 ชื่อที่ซ้ำ", 
                    existing.Patient.PatientName, existing.Patient.AN);
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void FlashRow(int rowIndex)
        {
            if (flashTimer != null)
            {
                flashTimer.Stop();
                flashTimer.Dispose();
                if (flashingRowIndex >= 0 && flashingRowIndex < dgvQueue.Rows.Count)
                {
                    dgvQueue.Rows[flashingRowIndex].DefaultCellStyle.BackColor = originalRowColor;
                }
            }

            flashingRowIndex = rowIndex;
            originalRowColor = dgvQueue.Rows[rowIndex].DefaultCellStyle.BackColor;
            int flashCount = 0;

            flashTimer = new Timer();
            flashTimer.Interval = 180;
            flashTimer.Tick += (s, e) =>
            {
                flashCount++;
                if (flashingRowIndex >= 0 && flashingRowIndex < dgvQueue.Rows.Count)
                {
                    if (flashCount % 2 == 1)
                        dgvQueue.Rows[flashingRowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 240, 138); // Yellow Flash
                    else
                        dgvQueue.Rows[flashingRowIndex].DefaultCellStyle.BackColor = originalRowColor;
                }

                if (flashCount >= 6)
                {
                    flashTimer.Stop();
                    if (flashingRowIndex >= 0 && flashingRowIndex < dgvQueue.Rows.Count)
                    {
                        dgvQueue.Rows[flashingRowIndex].DefaultCellStyle.BackColor = originalRowColor;
                    }
                    flashingRowIndex = -1;
                }
            };
            flashTimer.Start();
        }

        private void RefreshQueueGrid()
        {
            dgvQueue.Rows.Clear();
            for (int i = 0; i < _queue.Count; i++)
            {
                QueueItem item = _queue[i];
                int rowIdx = dgvQueue.Rows.Add(
                    item.IsSelected,
                    item.Patient.AN,
                    item.Patient.HN,
                    item.Patient.PatientName,
                    item.Patient.WardName ?? "-",
                    item.Patient.HasAllergy ? ("⚠️ " + item.Patient.Allergy) : "-",
                    item.Copies,
                    item.Status
                );

                if (item.Patient.HasAllergy)
                {
                    dgvQueue.Rows[rowIdx].Cells[5].Style.ForeColor = Color.Crimson;
                    dgvQueue.Rows[rowIdx].Cells[5].Style.Font = new Font(dgvQueue.Font, FontStyle.Bold);
                }

                if (item.Status == "พิมพ์สำเร็จ" || item.Status == "ส่งเครื่องแม่สำเร็จ")
                {
                    dgvQueue.Rows[rowIdx].Cells[7].Style.ForeColor = Color.FromArgb(16, 149, 106);
                    dgvQueue.Rows[rowIdx].Cells[7].Style.Font = new Font(dgvQueue.Font, FontStyle.Bold);
                }
                else if (item.Status == "ผิดพลาด")
                {
                    dgvQueue.Rows[rowIdx].Cells[7].Style.ForeColor = Color.Crimson;
                }
            }

            UpdateQueueCount();
        }

        private void UpdateQueueCount()
        {
            int total = _queue.Count;
            int sel = _queue.FindAll(q => q.IsSelected).Count;
            lblQueueCount.Text = string.Format("คิวรอพิมพ์: {0} (เลือก {1})", total, sel);
            btnPrintAll.Text = string.Format("🖨️  พิมพ์ทั้งหมดในคิว ({0} รายการ) [F9]", sel);
            btnPrintAll.Enabled = (sel > 0);
        }

        private void RemoveSelectedFromQueue()
        {
            _queue.RemoveAll(q => q.IsSelected);
            RefreshQueueGrid();
            ResetPatientView();
            lblStatusMsg.Text = "ลบรายการที่เลือกออกจากคิวแล้ว";
            txtSearch.Focus();
        }

        private void ClearQueue()
        {
            if (_queue.Count == 0) return;
            if (MessageBox.Show("คุณต้องการล้างคิวพิมพ์ทั้งหมดหรือไม่?", "ยืนยันการล้างคิว", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _queue.Clear();
                RefreshQueueGrid();
                ResetPatientView();
                lblStatusMsg.Text = "ล้างคิวพิมพ์เรียบร้อยแล้ว";
                txtSearch.Focus();
            }
        }

        private bool PrintOnePatient(PatientAdmission patient, DataStore ds, WristbandType type, int copies, out string resultMsg)
        {
            if (rdoPrintRemote != null && rdoPrintRemote.Checked)
            {
                // REMOTE PRINT VIA CENTRAL PRINT SERVER
                string host = txtRemoteHost.Text.Trim();
                if (string.IsNullOrEmpty(host))
                {
                    resultMsg = "กรุณาระบุ IP หรือชื่อเครื่องแม่ในวอร์ด";
                    return false;
                }

                PrintRequestPayload payload = new PrintRequestPayload
                {
                    AN = patient.AN,
                    Type = (type == WristbandType.Adult) ? "Adult" : "Child",
                    Copies = copies,
                    ClientMachine = Environment.MachineName,
                    ClientUser = _currentUser != null ? (_currentUser.DisplayName ?? _currentUser.UserId) : ""
                };

                int port = _config.RemoteServerPort > 0 ? _config.RemoteServerPort : 8099;
                PrintResponsePayload resp = PrintClientService.Instance.SendPrintJob(host, port, payload);
                if (resp != null && resp.Success)
                {
                    resultMsg = resp.Message ?? "ส่งพิมพ์สำเร็จ";
                    return true;
                }
                else
                {
                    resultMsg = resp != null ? resp.Message : "ไม่ได้รับการตอบกลับจากเครื่องแม่";
                    return false;
                }
            }
            else
            {
                // LOCAL DIRECT PRINT
                string prn = cboPrinters.SelectedItem != null ? cboPrinters.SelectedItem.ToString() : "";
                EphisService.Instance.PrintWristband(ds, prn, copies);
                resultMsg = string.Format("พิมพ์ออกเครื่องพิมพ์ {0} สำเร็จ", prn);
                return true;
            }
        }

        private void DoPrintAll()
        {
            List<QueueItem> toPrint = _queue.FindAll(q => q.IsSelected);
            if (toPrint.Count == 0)
            {
                MessageBox.Show("กรุณาเลือกรายการที่ต้องการพิมพ์ในคิวอย่างน้อย 1 รายการ", "แจ้งเตือน", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int successCount = 0;
            this.Cursor = Cursors.WaitCursor;

            foreach (var item in toPrint)
            {
                lblStatusMsg.Text = string.Format("กำลังพิมพ์: {0} (AN: {1})...", item.Patient.PatientName, item.Patient.AN);
                Application.DoEvents();

                string printMsg;
                bool ok = PrintOnePatient(item.Patient, item.DataStore, item.Type, item.Copies, out printMsg);
                if (ok)
                {
                    item.Status = rdoPrintRemote.Checked ? "ส่งเครื่องแม่สำเร็จ" : "พิมพ์สำเร็จ";
                    successCount++;

                    // Record to persistent print history
                    PrintHistoryRecord rec = new PrintHistoryRecord();
                    rec.AN = item.Patient.AN;
                    rec.HN = item.Patient.HN;
                    rec.PatientName = item.Patient.PatientName;
                    rec.Gender = item.Patient.Gender;
                    rec.Age = item.Patient.Age;
                    rec.WardName = item.Patient.WardName;
                    rec.RgtDate = item.Patient.RgtDate;
                    rec.Allergy = item.Patient.Allergy;
                    rec.WristbandType = (item.Type == WristbandType.Adult) ? "ผู้ใหญ่" : "เด็ก";
                    rec.Copies = item.Copies;
                    rec.PrinterName = rdoPrintRemote.Checked 
                        ? ("[เครื่องแม่: " + txtRemoteHost.Text.Trim() + "]") 
                        : (cboPrinters.SelectedItem != null ? cboPrinters.SelectedItem.ToString() : "");
                    rec.PrintedBy = _currentUser != null ? (_currentUser.DisplayName ?? _currentUser.UserId) : "";
                    rec.Status = rdoPrintRemote.Checked ? "ส่งพิมพ์ผ่านเครื่องแม่" : "พิมพ์สำเร็จ";
                    PrintHistoryService.Instance.AddRecord(rec);
                }
                else
                {
                    item.Status = "ผิดพลาด";
                }
            }

            this.Cursor = Cursors.Default;
            RefreshQueueGrid();
            LoadRecentHistoryList();

            lblStatusMsg.Text = string.Format("✓ ดำเนินการสำเร็จ {0}/{1} รายการ", successCount, toPrint.Count);
            txtSearch.Focus();
        }

        private void DoPrintSingle()
        {
            if (_currentPatient == null || _currentDataStore == null)
            {
                MessageBox.Show("กรุณาเลือกผู้ป่วยก่อนสั่งพิมพ์", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int copies = (int)numCopies.Value;
            WristbandType type = rdoAdult.Checked ? WristbandType.Adult : WristbandType.Child;

            try
            {
                string printMsg;
                bool ok = PrintOnePatient(_currentPatient, _currentDataStore, type, copies, out printMsg);
                if (ok)
                {
                    // Record to persistent print history
                    PrintHistoryRecord rec = new PrintHistoryRecord();
                    rec.AN = _currentPatient.AN;
                    rec.HN = _currentPatient.HN;
                    rec.PatientName = _currentPatient.PatientName;
                    rec.Gender = _currentPatient.Gender;
                    rec.Age = _currentPatient.Age;
                    rec.WardName = _currentPatient.WardName;
                    rec.RgtDate = _currentPatient.RgtDate;
                    rec.Allergy = _currentPatient.Allergy;
                    rec.WristbandType = rdoAdult.Checked ? "ผู้ใหญ่" : "เด็ก";
                    rec.Copies = copies;
                    rec.PrinterName = rdoPrintRemote.Checked 
                        ? ("[เครื่องแม่: " + txtRemoteHost.Text.Trim() + "]") 
                        : (cboPrinters.SelectedItem != null ? cboPrinters.SelectedItem.ToString() : "");
                    rec.PrintedBy = _currentUser != null ? (_currentUser.DisplayName ?? _currentUser.UserId) : "";
                    rec.Status = rdoPrintRemote.Checked ? "ส่งพิมพ์ผ่านเครื่องแม่" : "พิมพ์สำเร็จ";
                    PrintHistoryService.Instance.AddRecord(rec);

                    LoadRecentHistoryList();

                    lblStatusMsg.Text = string.Format("✓ สั่งพิมพ์สายรัดข้อมือ AN: {0} เรียบร้อยแล้ว ({1} ใบ)", 
                        _currentPatient.AN, copies);
                }
                else
                {
                    MessageBox.Show("พิมพ์ผิดพลาด:\n" + printMsg, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("พิมพ์ผิดพลาด: " + ex.Message, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenHistoryDialog()
        {
            HistoryForm frm = new HistoryForm();
            frm.OnAddToQueueRequested = (rec) =>
            {
                AddAdmissionToQueue(rec.AN);
            };
            frm.OnReprintRequested = (rec) =>
            {
                try
                {
                    WristbandType type = (rec.WristbandType == "เด็ก" || rec.WristbandType == "Child") 
                        ? WristbandType.Child 
                        : WristbandType.Adult;
                    DataStore ds;
                    PatientAdmission pt = EphisService.Instance.LoadWristband(rec.AN, type, out ds);
                    
                    string printMsg;
                    bool ok = PrintOnePatient(pt, ds, type, rec.Copies, out printMsg);
                    if (ok)
                    {
                        // Save reprint log
                        PrintHistoryRecord newRec = new PrintHistoryRecord();
                        newRec.AN = rec.AN;
                        newRec.HN = rec.HN;
                        newRec.PatientName = rec.PatientName;
                        newRec.Gender = rec.Gender;
                        newRec.Age = rec.Age;
                        newRec.WardName = rec.WardName;
                        newRec.RgtDate = rec.RgtDate;
                        newRec.Allergy = rec.Allergy;
                        newRec.WristbandType = rec.WristbandType;
                        newRec.Copies = rec.Copies;
                        newRec.PrinterName = rdoPrintRemote.Checked 
                            ? ("[เครื่องแม่: " + txtRemoteHost.Text.Trim() + "]") 
                            : (cboPrinters.SelectedItem != null ? cboPrinters.SelectedItem.ToString() : "");
                        newRec.PrintedBy = _currentUser != null ? (_currentUser.DisplayName ?? _currentUser.UserId) : "";
                        newRec.Status = "พิมพ์ซ้ำ";
                        PrintHistoryService.Instance.AddRecord(newRec);

                        LoadRecentHistoryList();

                        MessageBox.Show(string.Format("พิมพ์สายรัดข้อมือซ้ำสำหรับ:\n{0} (AN: {1})\nเรียบร้อยแล้ว", 
                            rec.PatientName, rec.AN), "พิมพ์ซ้ำสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("พิมพ์ซ้ำผิดพลาด: " + printMsg, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("พิมพ์ซ้ำผิดพลาด: " + ex.Message, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            frm.ShowDialog(this);
        }

        private void LoadRecentHistoryList()
        {
            try
            {
                lstHistory.Items.Clear();
                List<PrintHistoryRecord> recs = PrintHistoryService.Instance.GetAll();
                int count = Math.Min(35, recs.Count);
                for (int i = 0; i < count; i++)
                {
                    var r = recs[i];
                    ListViewItem hist = new ListViewItem(r.PrintTime.ToString("HH:mm:ss"));
                    hist.SubItems.Add(r.AN.ToString());
                    hist.SubItems.Add(r.HN.ToString());
                    hist.SubItems.Add(r.PatientName);
                    hist.SubItems.Add(r.WardName ?? "-");
                    hist.Tag = r;
                    lstHistory.Items.Add(hist);
                }
            }
            catch { }
        }

        private void ShowPatientDetails(PatientAdmission patient, DataStore ds)
        {
            _currentPatient = patient;
            _currentDataStore = ds;

            lblPtName.Text = patient.PatientName;
            lblPtHn.Text = "HN: " + patient.HN;
            lblPtAn.Text = "AN: " + patient.AN;
            lblPtGenderAge.Text = string.Format("เพศ: {0} ({1} ปี)", patient.Gender ?? "-", patient.Age ?? "-");
            lblPtWard.Text = "หอผู้ป่วย: " + (patient.WardName ?? "-");
            lblPtAdmitDate.Text = "Admit: " + (patient.RgtDate ?? "-");

            if (patient.HasAllergy)
            {
                pnlAllergy.BackColor = Color.FromArgb(254, 242, 242);
                lblAllergyText.Text = "⚠️ แพ้ยา: " + patient.Allergy;
                lblAllergyText.ForeColor = Color.FromArgb(185, 28, 28);
            }
            else
            {
                pnlAllergy.BackColor = Color.FromArgb(240, 253, 244);
                lblAllergyText.Text = "✓ ไม่มีประวัติแพ้ยา";
                lblAllergyText.ForeColor = Color.FromArgb(21, 128, 61);
            }

            btnPrintSingle.Enabled = true;
            btnPreview.Enabled = true;
        }

        private void ResetPatientView()
        {
            _currentPatient = null;
            _currentDataStore = null;

            lblPtName.Text = "ยังไม่มีการเลือกผู้ป่วย";
            lblPtHn.Text = "HN: -";
            lblPtAn.Text = "AN: -";
            lblPtGenderAge.Text = "เพศ/อายุ: -";
            lblPtWard.Text = "หอผู้ป่วย: -";
            lblPtAdmitDate.Text = "Admit: -";

            pnlAllergy.BackColor = Color.FromArgb(240, 242, 245);
            lblAllergyText.Text = "ประวัติแพ้ยา: -";
            lblAllergyText.ForeColor = Color.FromArgb(100, 116, 139);

            btnPrintSingle.Enabled = false;
            btnPreview.Enabled = false;
        }

        private void ShowPreview()
        {
            if (_currentPatient == null) return;
            WristbandType type = rdoAdult.Checked ? WristbandType.Adult : WristbandType.Child;
            PreviewForm frm = new PreviewForm(_currentPatient, type);
            frm.ShowDialog(this);
            if (frm.PrintRequested)
            {
                DoPrintSingle();
            }
        }

        private void LoadPrinters()
        {
            cboPrinters.Items.Clear();
            string defaultPrn = "";
            try
            {
                PrinterSettings ps = new PrinterSettings();
                defaultPrn = ps.PrinterName;
            }
            catch { }

            foreach (string p in PrinterSettings.InstalledPrinters)
            {
                cboPrinters.Items.Add(p);
            }

            if (!string.IsNullOrEmpty(_config.SelectedPrinter) && cboPrinters.Items.Contains(_config.SelectedPrinter))
            {
                cboPrinters.SelectedItem = _config.SelectedPrinter;
            }
            else if (!string.IsNullOrEmpty(defaultPrn) && cboPrinters.Items.Contains(defaultPrn))
            {
                cboPrinters.SelectedItem = defaultPrn;
            }
            else if (cboPrinters.Items.Count > 0)
            {
                cboPrinters.SelectedIndex = 0;
            }

            if (cboPrinters.SelectedItem != null && rdoPrintLocal != null && rdoPrintLocal.Checked)
            {
                lblCurrentPrinterStatus.Text = "เครื่องพิมพ์: " + cboPrinters.SelectedItem;
            }
        }

        private void SavePrinterConfig()
        {
            _config.PrintMode = rdoPrintRemote.Checked ? "Remote" : "Local";
            _config.RemoteServerHost = txtRemoteHost.Text.Trim();
            if (cboPrinters.SelectedItem != null)
            {
                _config.SelectedPrinter = cboPrinters.SelectedItem.ToString();
            }
            _config.Copies = (int)numCopies.Value;
            _config.DefaultType = rdoAdult.Checked ? "Adult" : "Child";
            _config.Save();

            string msg = rdoPrintRemote.Checked
                ? string.Format("บันทึกการตั้งค่าเป็น 'ส่งพิมพ์ผ่านเครื่องแม่':\nIP เครื่องแม่: {0}\nสำเนา: {1} ใบ", 
                    _config.RemoteServerHost, _config.Copies)
                : string.Format("บันทึกการตั้งค่าเป็น 'พิมพ์ตรงจากเครื่องนี้':\nเครื่องพิมพ์: {0}\nสำเนา: {1} ใบ", 
                    _config.SelectedPrinter, _config.Copies);

            MessageBox.Show(msg, "บันทึกสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Logout()
        {
            if (MessageBox.Show("คุณต้องการออกจากระบบหรือไม่?\n(การออกจากระบบจะยกเลิกการล็อกอินอัตโนมัติ)", 
                "ยืนยันการออกจากระบบ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _config.EncryptedPassword = "";
                _config.StayLoggedIn = false;
                _config.Save();

                this.Hide();
                LoginForm login = new LoginForm();
                if (login.ShowDialog() == DialogResult.OK)
                {
                    _currentUser = login.AuthenticatedUser;
                    this.Show();
                    ResetPatientView();
                    _queue.Clear();
                    RefreshQueueGrid();
                    LoadRecentHistoryList();
                }
                else
                {
                    Application.Exit();
                }
            }
        }

        #region Auto Update

        private void CheckUpdateInBackground(bool isManual)
        {
            if (isManual)
            {
                btnCheckUpdate.Text = "โณ เธเธณเธฅเธฑเธเน€เธเนเธ...";
                this.Cursor = Cursors.WaitCursor;
            }

            ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    string host = _config.RemoteServerHost;
                    int port = _config.RemoteServerPort;
                    UpdateInfo info = UpdateService.Instance.CheckForUpdate(host, port);

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (isManual) this.Cursor = Cursors.Default;

                        if (info.HasUpdate)
                        {
                            btnCheckUpdate.Text = "๐ เธกเธตเธญเธฑเธเน€เธ”เธ• " + info.LatestVersion;
                            btnCheckUpdate.BackColor = Color.FromArgb(220, 38, 38); // Red
                            btnCheckUpdate.ForeColor = Color.White;

                            string msg = string.Format(
                                "๐ เธเธเธเธฒเธฃเธญเธฑเธเน€เธ”เธ•เน€เธงเธญเธฃเนเธเธฑเธเนเธซเธกเนเธเธญเธ TUH Wristband Printer!\n\n" +
                                "โ€ข เน€เธงเธญเธฃเนเธเธฑเธเนเธซเธกเน:  v{0} (เธเธฑเธเธเธธเธเธฑเธ v{1})\n" +
                                "โ€ข เนเธซเธฅเนเธเธ—เธตเนเธกเธฒ:  {2}\n" +
                                "โ€ข เธฃเธฒเธขเธฅเธฐเน€เธญเธตเธขเธ”:  {3}\n\n" +
                                "เธ•เนเธญเธเธเธฒเธฃเธ”เธฒเธงเธเนเนเธซเธฅเธ”เนเธฅเธฐเธ•เธดเธ”เธ•เธฑเนเธเธเธฒเธฃเธญเธฑเธเน€เธ”เธ•เธ—เธฑเธเธ—เธตเธซเธฃเธทเธญเนเธกเน?\n" +
                                "(เนเธเธฃเนเธเธฃเธกเธเธฐเธ”เธฒเธงเธเนเนเธซเธฅเธ”เนเธฅเธฐเน€เธเธดเธ”เน€เธงเธญเธฃเนเธเธฑเธเนเธซเธกเนเนเธซเนเธญเธฑเธ•เนเธเธกเธฑเธ•เธด)",
                                info.LatestVersion, info.CurrentVersion, info.Source, 
                                string.IsNullOrEmpty(info.Changelog) ? "เธเธฃเธฑเธเธเธฃเธธเธเธเธฃเธฐเธชเธดเธ—เธเธดเธ เธฒเธเนเธฅเธฐเนเธเนเนเธเธเนเธญเธเธดเธ”เธเธฅเธฒเธ”" : info.Changelog);

                            if (MessageBox.Show(this, msg, "เธเธเน€เธงเธญเธฃเนเธเธฑเธเนเธซเธกเน v" + info.LatestVersion, 
                                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                            {
                                PromptApplyUpdate(info);
                            }
                        }
                        else
                        {
                            btnCheckUpdate.Text = "๐” v" + UpdateService.CurrentVersion;
                            btnCheckUpdate.BackColor = Color.FromArgb(24, 76, 120);
                            btnCheckUpdate.ForeColor = Color.White;

                            if (isManual)
                            {
                                MessageBox.Show(this, 
                                    "เธเธธเธ“เธเธณเธฅเธฑเธเนเธเนเธเธฒเธเนเธเธฃเนเธเธฃเธกเน€เธงเธญเธฃเนเธเธฑเธเธฅเนเธฒเธชเธธเธ”เนเธฅเนเธง (v" + UpdateService.CurrentVersion + ")", 
                                    "เธ•เธฃเธงเธเธชเธญเธเธเธฒเธฃเธญเธฑเธเน€เธ”เธ•", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (isManual)
                        {
                            this.Cursor = Cursors.Default;
                            btnCheckUpdate.Text = "๐” v" + UpdateService.CurrentVersion;
                            MessageBox.Show(this, "เนเธกเนเธชเธฒเธกเธฒเธฃเธ–เธ•เธฃเธงเธเธชเธญเธเธเธฒเธฃเธญเธฑเธเน€เธ”เธ•เนเธ”เน: " + ex.Message, 
                                "เธ•เธฃเธงเธเธชเธญเธเธเธฒเธฃเธญเธฑเธเน€เธ”เธ•", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    });
                }
            });
        }

        private void PromptApplyUpdate(UpdateInfo info)
        {
            Form progressForm = new Form();
            progressForm.Text = "เธเธณเธฅเธฑเธเธญเธฑเธเน€เธ”เธ•เนเธเธฃเนเธเธฃเธก...";
            progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            progressForm.StartPosition = FormStartPosition.CenterScreen;
            progressForm.Size = new Size(400, 150);
            progressForm.MaximizeBox = false;
            progressForm.MinimizeBox = false;
            progressForm.ControlBox = false;

            Label lbl = new Label();
            lbl.Text = "เธเธณเธฅเธฑเธเธ”เธฒเธงเธเนเนเธซเธฅเธ”เนเธเธฅเนเธญเธฑเธเน€เธ”เธ•เธเธฒเธ " + info.Source + "...\nเธเธฃเธธเธ“เธฒเธฃเธญเธชเธฑเธเธเธฃเธนเน เนเธเธฃเนเธเธฃเธกเธเธฐเธฃเธตเธชเธ•เธฒเธฃเนเธ—เน€เธเนเธเน€เธงเธญเธฃเนเธเธฑเธเนเธซเธกเนเนเธ”เธขเธญเธฑเธ•เนเธเธกเธฑเธ•เธด";
            lbl.Location = new Point(20, 20);
            lbl.Size = new Size(350, 40);
            lbl.Font = new Font("Segoe UI", 9F);
            progressForm.Controls.Add(lbl);

            ProgressBar pb = new ProgressBar();
            pb.Style = ProgressBarStyle.Marquee;
            pb.Location = new Point(20, 65);
            pb.Size = new Size(345, 22);
            progressForm.Controls.Add(pb);

            progressForm.Shown += (s, e) =>
            {
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        UpdateService.Instance.DownloadAndApplyUpdate(info.DownloadUrl);
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            progressForm.Close();
                            MessageBox.Show(this, "เน€เธเธดเธ”เธเนเธญเธเธดเธ”เธเธฅเธฒเธ”เนเธเธเธฒเธฃเธ”เธฒเธงเธเนเนเธซเธฅเธ”เธญเธฑเธเน€เธ”เธ•: " + ex.Message, 
                                "เธเนเธญเธเธดเธ”เธเธฅเธฒเธ”", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                });
            };

            progressForm.ShowDialog(this);
        }

        #endregion
    }
}
