using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TUH_WristbandPrinter
{
    public class HistoryForm : Form
    {
        private TextBox txtSearch;
        private ComboBox cboDate;
        private Label lblCount;
        private DataGridView dgv;
        private Button btnReprint;
        private Button btnAddToQueue;
        private Button btnPreview;
        private Button btnExport;
        private Button btnClear;
        private Button btnClose;

        public Action<PrintHistoryRecord> OnAddToQueueRequested;
        public Action<PrintHistoryRecord> OnReprintRequested;

        private List<PrintHistoryRecord> _records = new List<PrintHistoryRecord>();

        public HistoryForm()
        {
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "📜 ประวัติการพิมพ์สายรัดข้อมือย้อนหลัง (TUH Wristband Print History)";
            this.Size = new Size(1050, 640);
            this.MinimumSize = new Size(820, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9.5F);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(iconPath))
                    this.Icon = new Icon(iconPath);
            }
            catch { }

            // 1. Top Filter Panel
            Panel topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 72;
            topPanel.BackColor = Color.White;
            topPanel.BorderStyle = BorderStyle.FixedSingle;
            topPanel.Padding = new Padding(12, 10, 12, 10);
            this.Controls.Add(topPanel);

            Label lblSearchTitle = new Label();
            lblSearchTitle.Text = "🔍 ค้นหา (HN, AN, ชื่อ หรือ หอผู้ป่วย):";
            lblSearchTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSearchTitle.ForeColor = Color.FromArgb(30, 41, 59);
            lblSearchTitle.Location = new Point(14, 10);
            lblSearchTitle.AutoSize = true;
            topPanel.Controls.Add(lblSearchTitle);

            txtSearch = new TextBox();
            txtSearch.Location = new Point(14, 30);
            txtSearch.Size = new Size(300, 29);
            txtSearch.Font = new Font("Segoe UI", 11F);
            txtSearch.TextChanged += (s, e) => FilterData();
            topPanel.Controls.Add(txtSearch);

            Label lblDateTitle = new Label();
            lblDateTitle.Text = "ช่วงเวลา:";
            lblDateTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblDateTitle.ForeColor = Color.FromArgb(30, 41, 59);
            lblDateTitle.Location = new Point(330, 10);
            lblDateTitle.AutoSize = true;
            topPanel.Controls.Add(lblDateTitle);

            cboDate = new ComboBox();
            cboDate.Location = new Point(330, 30);
            cboDate.Size = new Size(170, 29);
            cboDate.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDate.Font = new Font("Segoe UI", 10F);
            cboDate.Items.AddRange(new object[] {
                "ทั้งหมด (All)",
                "วันนี้ (Today)",
                "ย้อนหลัง 3 วัน (Last 3 days)",
                "ย้อนหลัง 7 วัน (Last 7 days)",
                "ย้อนหลัง 30 วัน (Last 30 days)"
            });
            cboDate.SelectedIndex = 0;
            cboDate.SelectedIndexChanged += (s, e) => FilterData();
            topPanel.Controls.Add(cboDate);

            Button btnRefresh = new Button();
            btnRefresh.Text = "🔄 รีเฟรช";
            btnRefresh.Location = new Point(515, 28);
            btnRefresh.Size = new Size(95, 33);
            btnRefresh.BackColor = Color.FromArgb(241, 245, 249);
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnRefresh.Cursor = Cursors.Hand;
            btnRefresh.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnRefresh.Click += (s, e) => LoadData();
            topPanel.Controls.Add(btnRefresh);

            lblCount = new Label();
            lblCount.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblCount.ForeColor = Color.FromArgb(24, 76, 120);
            lblCount.Location = new Point(625, 33);
            lblCount.AutoSize = true;
            topPanel.Controls.Add(lblCount);

            // 2. Bottom Actions Panel
            Panel bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 60;
            bottomPanel.BackColor = Color.FromArgb(248, 250, 252);
            bottomPanel.BorderStyle = BorderStyle.FixedSingle;
            bottomPanel.Padding = new Padding(12, 10, 12, 10);
            this.Controls.Add(bottomPanel);

            btnReprint = new Button();
            btnReprint.Text = "🖨️ พิมพ์ซ้ำทันที (Reprint)";
            btnReprint.Location = new Point(14, 11);
            btnReprint.Size = new Size(185, 38);
            btnReprint.BackColor = Color.FromArgb(16, 149, 106); // Emerald Green
            btnReprint.ForeColor = Color.White;
            btnReprint.FlatStyle = FlatStyle.Flat;
            btnReprint.FlatAppearance.BorderSize = 0;
            btnReprint.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnReprint.Cursor = Cursors.Hand;
            btnReprint.Click += (s, e) => DoReprintSelected();
            bottomPanel.Controls.Add(btnReprint);

            btnAddToQueue = new Button();
            btnAddToQueue.Text = "＋ เพิ่มกลับเข้าคิวพิมพ์";
            btnAddToQueue.Location = new Point(208, 11);
            btnAddToQueue.Size = new Size(165, 38);
            btnAddToQueue.BackColor = Color.FromArgb(24, 76, 120); // Medical Navy
            btnAddToQueue.ForeColor = Color.White;
            btnAddToQueue.FlatStyle = FlatStyle.Flat;
            btnAddToQueue.FlatAppearance.BorderSize = 0;
            btnAddToQueue.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnAddToQueue.Cursor = Cursors.Hand;
            btnAddToQueue.Click += (s, e) => DoAddToQueueSelected();
            bottomPanel.Controls.Add(btnAddToQueue);

            btnPreview = new Button();
            btnPreview.Text = "👁️ ตัวอย่าง (Preview)";
            btnPreview.Location = new Point(382, 11);
            btnPreview.Size = new Size(150, 38);
            btnPreview.BackColor = Color.FromArgb(226, 232, 240);
            btnPreview.ForeColor = Color.FromArgb(30, 41, 59);
            btnPreview.FlatStyle = FlatStyle.Flat;
            btnPreview.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnPreview.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnPreview.Cursor = Cursors.Hand;
            btnPreview.Click += (s, e) => DoPreviewSelected();
            bottomPanel.Controls.Add(btnPreview);

            btnExport = new Button();
            btnExport.Text = "📊 ส่งออก CSV";
            btnExport.Location = new Point(540, 11);
            btnExport.Size = new Size(130, 38);
            btnExport.BackColor = Color.FromArgb(241, 245, 249);
            btnExport.ForeColor = Color.FromArgb(51, 65, 85);
            btnExport.FlatStyle = FlatStyle.Flat;
            btnExport.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnExport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnExport.Cursor = Cursors.Hand;
            btnExport.Click += (s, e) => DoExportCsv();
            bottomPanel.Controls.Add(btnExport);

            btnClear = new Button();
            btnClear.Text = "🗑️ ล้างประวัติ";
            btnClear.Location = new Point(680, 11);
            btnClear.Size = new Size(110, 38);
            btnClear.BackColor = Color.FromArgb(254, 242, 242);
            btnClear.ForeColor = Color.FromArgb(185, 28, 28);
            btnClear.FlatStyle = FlatStyle.Flat;
            btnClear.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
            btnClear.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnClear.Cursor = Cursors.Hand;
            btnClear.Click += (s, e) => DoClearHistory();
            bottomPanel.Controls.Add(btnClear);

            btnClose = new Button();
            btnClose.Text = "ปิด";
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(this.ClientSize.Width - 110, 11);
            btnClose.Size = new Size(95, 38);
            btnClose.BackColor = Color.FromArgb(226, 232, 240);
            btnClose.ForeColor = Color.FromArgb(30, 41, 59);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();
            bottomPanel.Controls.Add(btnClose);

            // 3. Grid
            dgv = new DataGridView();
            dgv.Dock = DockStyle.Fill;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.RowTemplate.Height = 32;
            dgv.Font = new Font("Segoe UI", 9F);
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 36;
            dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) DoReprintSelected(); };

            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "วัน-เวลาที่พิมพ์", Width = 135 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "HN", Width = 75 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "AN", Width = 75 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ชื่อ-สกุล ผู้ป่วย", Width = 160, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "เพศ/อายุ", Width = 80 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "หอผู้ป่วย", Width = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ประเภท", Width = 65 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "จำนวน", Width = 50 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "เครื่องพิมพ์", Width = 120 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ผู้พิมพ์", Width = 75 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ประวัติแพ้ยา", Width = 130 });

            this.Controls.Add(dgv);
            dgv.BringToFront();
        }

        private void LoadData()
        {
            FilterData();
        }

        private void FilterData()
        {
            string dateFilter = "ALL";
            switch (cboDate.SelectedIndex)
            {
                case 1: dateFilter = "TODAY"; break;
                case 2: dateFilter = "3DAYS"; break;
                case 3: dateFilter = "7DAYS"; break;
                case 4: dateFilter = "30DAYS"; break;
            }

            _records = PrintHistoryService.Instance.Search(txtSearch.Text, dateFilter);

            dgv.Rows.Clear();
            foreach (var r in _records)
            {
                int idx = dgv.Rows.Add(
                    r.PrintTime.ToString("dd/MM/yyyy HH:mm:ss"),
                    r.HN,
                    r.AN,
                    r.PatientName,
                    string.Format("{0} ({1})", r.Gender ?? "-", r.Age ?? "-"),
                    r.WardName ?? "-",
                    r.WristbandType,
                    r.Copies,
                    r.PrinterName ?? "-",
                    r.PrintedBy ?? "-",
                    r.Allergy ?? "-"
                );

                dgv.Rows[idx].Tag = r;

                // Allergy highlight
                if (!string.IsNullOrEmpty(r.Allergy) && r.Allergy != "-" && r.Allergy != "[None]")
                {
                    dgv.Rows[idx].Cells[10].Style.ForeColor = Color.Crimson;
                    dgv.Rows[idx].Cells[10].Style.Font = new Font(dgv.Font, FontStyle.Bold);
                }
            }

            lblCount.Text = string.Format("พบ {0} รายการ", _records.Count);
        }

        private PrintHistoryRecord GetSelectedRecord()
        {
            if (dgv.SelectedRows.Count > 0)
            {
                return dgv.SelectedRows[0].Tag as PrintHistoryRecord;
            }
            return null;
        }

        private void DoReprintSelected()
        {
            PrintHistoryRecord r = GetSelectedRecord();
            if (r == null)
            {
                MessageBox.Show("กรุณาเลือกรายการที่ต้องการพิมพ์ซ้ำ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(string.Format("ต้องการพิมพ์สายรัดข้อมือซ้ำสำหรับ:\n{0} (AN: {1}, HN: {2})\nจำนวน: {3} ใบ หรือไม่?", 
                r.PatientName, r.AN, r.HN, r.Copies), 
                "ยืนยันพิมพ์ซ้ำ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (OnReprintRequested != null)
                {
                    OnReprintRequested(r);
                    this.Close();
                }
            }
        }

        private void DoAddToQueueSelected()
        {
            PrintHistoryRecord r = GetSelectedRecord();
            if (r == null)
            {
                MessageBox.Show("กรุณาเลือกรายการที่ต้องการเพิ่มเข้าคิว", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (OnAddToQueueRequested != null)
            {
                OnAddToQueueRequested(r);
                this.Close();
            }
        }

        private void DoPreviewSelected()
        {
            PrintHistoryRecord r = GetSelectedRecord();
            if (r == null) return;

            try
            {
                WristbandType type = (r.WristbandType == "เด็ก" || r.WristbandType == "Child") 
                    ? WristbandType.Child 
                    : WristbandType.Adult;

                Sybase.DataWindow.DataStore ds;
                PatientAdmission patient = EphisService.Instance.LoadWristband(r.AN, type, out ds);
                PreviewForm preview = new PreviewForm(patient, type);
                preview.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("ไม่สามารถเปิดตัวอย่างได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoExportCsv()
        {
            if (_records.Count == 0)
            {
                MessageBox.Show("ไม่มีข้อมูลสำหรับส่งออก", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV File (*.csv)|*.csv";
            sfd.FileName = string.Format("TUH_Wristband_PrintHistory_{0}.csv", DateTime.Now.ToString("yyyyMMdd_HHmm"));

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("PrintTime,HN,AN,PatientName,Gender,Age,Ward,Type,Copies,Printer,PrintedBy,Allergy");
                    foreach (var r in _records)
                    {
                        sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\"",
                            r.PrintTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            r.HN,
                            r.AN,
                            (r.PatientName ?? "").Replace("\"", "\"\""),
                            r.Gender ?? "",
                            r.Age ?? "",
                            (r.WardName ?? "").Replace("\"", "\"\""),
                            r.WristbandType,
                            r.Copies,
                            (r.PrinterName ?? "").Replace("\"", "\"\""),
                            (r.PrintedBy ?? "").Replace("\"", "\"\""),
                            (r.Allergy ?? "").Replace("\"", "\"\"")
                        ));
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("ส่งออกข้อมูลเรียบร้อยแล้ว:\n" + sfd.FileName, "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("เกิดข้อผิดพลาดในการส่งออก: " + ex.Message, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DoClearHistory()
        {
            if (MessageBox.Show("คุณแน่ใจหรือไม่ว่าต้องการล้างประวัติการพิมพ์ทั้งหมด?\n(ข้อมูลจะไม่สามารถกู้คืนได้)", 
                "ยืนยันการล้างประวัติ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                PrintHistoryService.Instance.ClearAll();
                FilterData();
            }
        }
    }
}
