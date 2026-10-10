using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TUH_WristbandPrinter
{
    public class LoginForm : Form
    {
        private TextBox txtUserId;
        private TextBox txtPassword;
        private CheckBox chkRemember;
        private CheckBox chkStayLoggedIn;
        private Button btnLogin;
        private Button btnCancel;
        private Label lblStatus;
        private Panel headerPanel;

        public StaffUser AuthenticatedUser { get; private set; }

        public LoginForm()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            this.Text = "เข้าสู่ระบบ - TUH Wristband Printer";
            this.Size = new Size(430, 390);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(iconPath))
                    this.Icon = new Icon(iconPath);
            }
            catch { }

            // Header panel
            headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 85;
            headerPanel.BackColor = Color.FromArgb(24, 76, 120); // Medical Navy Blue
            this.Controls.Add(headerPanel);

            Label lblTitle = new Label();
            lblTitle.Text = "🏥 ระบบพิมพ์สายรัดข้อมือผู้ป่วย";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(20, 16);
            headerPanel.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "โรงพยาบาลธรรมศาสตร์เฉลิมพระเกียรติ (TUH)";
            lblSub.ForeColor = Color.FromArgb(190, 220, 245);
            lblSub.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblSub.AutoSize = true;
            lblSub.Location = new Point(23, 48);
            headerPanel.Controls.Add(lblSub);

            // Body
            int startY = 105;

            Label lblUser = new Label();
            lblUser.Text = "ชื่อผู้ใช้งาน (User ID):";
            lblUser.Location = new Point(35, startY);
            lblUser.AutoSize = true;
            lblUser.ForeColor = Color.FromArgb(50, 50, 50);
            this.Controls.Add(lblUser);

            txtUserId = new TextBox();
            txtUserId.Location = new Point(35, startY + 24);
            txtUserId.Size = new Size(345, 29);
            txtUserId.Font = new Font("Segoe UI", 11F);
            this.Controls.Add(txtUserId);

            Label lblPwd = new Label();
            lblPwd.Text = "รหัสผ่าน (Password):";
            lblPwd.Location = new Point(35, startY + 62);
            lblPwd.AutoSize = true;
            lblPwd.ForeColor = Color.FromArgb(50, 50, 50);
            this.Controls.Add(lblPwd);

            txtPassword = new TextBox();
            txtPassword.Location = new Point(35, startY + 86);
            txtPassword.Size = new Size(345, 29);
            txtPassword.Font = new Font("Segoe UI", 11F);
            txtPassword.UseSystemPasswordChar = true;
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoLogin(); };
            this.Controls.Add(txtPassword);

            chkRemember = new CheckBox();
            chkRemember.Text = "จำชื่อผู้ใช้งาน";
            chkRemember.Location = new Point(35, startY + 120);
            chkRemember.AutoSize = true;
            chkRemember.ForeColor = Color.FromArgb(80, 80, 80);
            this.Controls.Add(chkRemember);

            chkStayLoggedIn = new CheckBox();
            chkStayLoggedIn.Text = "ล็อกอินค้างไว้ตลอด (เข้าสู่ระบบอัตโนมัติไม่ต้องล็อกอินใหม่)";
            chkStayLoggedIn.Location = new Point(35, startY + 144);
            chkStayLoggedIn.AutoSize = true;
            chkStayLoggedIn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            chkStayLoggedIn.ForeColor = Color.FromArgb(24, 76, 120);
            chkStayLoggedIn.Checked = true;
            this.Controls.Add(chkStayLoggedIn);

            lblStatus = new Label();
            lblStatus.Location = new Point(35, startY + 172);
            lblStatus.Size = new Size(345, 20);
            lblStatus.ForeColor = Color.Crimson;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            this.Controls.Add(lblStatus);

            btnLogin = new Button();
            btnLogin.Text = "เข้าสู่ระบบ";
            btnLogin.Location = new Point(35, startY + 196);
            btnLogin.Size = new Size(230, 38);
            btnLogin.BackColor = Color.FromArgb(24, 76, 120);
            btnLogin.ForeColor = Color.White;
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            btnLogin.Cursor = Cursors.Hand;
            btnLogin.Click += (s, e) => DoLogin();
            this.Controls.Add(btnLogin);

            btnCancel = new Button();
            btnCancel.Text = "ยกเลิก";
            btnCancel.Location = new Point(275, startY + 196);
            btnCancel.Size = new Size(105, 38);
            btnCancel.BackColor = Color.FromArgb(220, 225, 230);
            btnCancel.ForeColor = Color.FromArgb(50, 50, 50);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);
        }

        private void LoadConfig()
        {
            AppConfig config = AppConfig.Load();
            chkStayLoggedIn.Checked = config.StayLoggedIn;
            if (config.RememberUser && !string.IsNullOrEmpty(config.LastUserId))
            {
                txtUserId.Text = config.LastUserId;
                chkRemember.Checked = true;
                this.ActiveControl = txtPassword;
            }
            else
            {
                chkRemember.Checked = false;
                this.ActiveControl = txtUserId;
            }
        }

        private void DoLogin()
        {
            string u = txtUserId.Text.Trim();
            string p = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(u))
            {
                lblStatus.Text = "กรุณาระบุชื่อผู้ใช้งาน";
                txtUserId.Focus();
                return;
            }

            if (string.IsNullOrEmpty(p))
            {
                lblStatus.Text = "กรุณาระบุรหัสผ่าน";
                txtPassword.Focus();
                return;
            }

            lblStatus.ForeColor = Color.FromArgb(24, 76, 120);
            lblStatus.Text = "กำลังตรวจสอบสิทธิ์...";
            btnLogin.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            Application.DoEvents();

            try
            {
                StaffUser staff = EphisService.Instance.Login(u, p);
                AuthenticatedUser = staff;

                // Save remember user & persistent login
                AppConfig config = AppConfig.Load();
                config.RememberUser = chkRemember.Checked;
                config.LastUserId = chkRemember.Checked ? u : "";
                config.StayLoggedIn = chkStayLoggedIn.Checked;
                if (chkStayLoggedIn.Checked)
                {
                    config.EncryptedPassword = AppConfig.EncryptSecret(p);
                }
                else
                {
                    config.EncryptedPassword = "";
                }
                config.Save();

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.Crimson;
                lblStatus.Text = ex.Message;
                txtPassword.SelectAll();
                txtPassword.Focus();
            }
            finally
            {
                btnLogin.Enabled = true;
                this.Cursor = Cursors.Default;
            }
        }
    }
}
