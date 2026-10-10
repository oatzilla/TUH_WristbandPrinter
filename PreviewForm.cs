using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TUH_WristbandPrinter
{
    public class PreviewForm : Form
    {
        private PatientAdmission _patient;
        private WristbandType _type;

        public bool PrintRequested { get; private set; }

        public PreviewForm(PatientAdmission patient, WristbandType type)
        {
            _patient = patient;
            _type = type;
            PrintRequested = false;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "ตัวอย่างป้ายสายรัดข้อมือ - " + (_type == WristbandType.Adult ? "ผู้ใหญ่" : "เด็ก");
            this.Size = new Size(720, 320);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(240, 242, 245);
            this.Font = new Font("Segoe UI", 9.5F);

            // Top Info
            Label lblHeader = new Label();
            lblHeader.Text = string.Format("ตัวอย่างสายรัดข้อมือ ({0}) - AN: {1}", 
                _type == WristbandType.Adult ? "ผู้ใหญ่" : "เด็ก", _patient.AN);
            lblHeader.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblHeader.ForeColor = Color.FromArgb(30, 41, 59);
            lblHeader.Location = new Point(20, 15);
            lblHeader.AutoSize = true;
            this.Controls.Add(lblHeader);

            // Wristband Container (Simulates the physical strip)
            Panel stripPanel = new Panel();
            stripPanel.Location = new Point(20, 45);
            stripPanel.Size = new Size(665, 160);
            stripPanel.BackColor = Color.White;
            stripPanel.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(stripPanel);

            // Left QR Code
            PictureBox picQr = new PictureBox();
            picQr.Location = new Point(15, 20);
            picQr.Size = new Size(115, 115);
            picQr.SizeMode = PictureBoxSizeMode.Zoom;
            picQr.BorderStyle = BorderStyle.FixedSingle;
            if (!string.IsNullOrEmpty(_patient.QrCodeImagePath) && File.Exists(_patient.QrCodeImagePath))
            {
                try { picQr.Image = Image.FromFile(_patient.QrCodeImagePath); } catch { }
            }
            stripPanel.Controls.Add(picQr);

            Label lblQrSub = new Label();
            lblQrSub.Text = "AN: " + _patient.AN;
            lblQrSub.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblQrSub.Location = new Point(15, 138);
            lblQrSub.Size = new Size(115, 16);
            lblQrSub.TextAlign = ContentAlignment.MiddleCenter;
            stripPanel.Controls.Add(lblQrSub);

            // Center details
            int x = 145;
            Label lblHospital = new Label();
            lblHospital.Text = "โรงพยาบาลธรรมศาสตร์เฉลิมพระเกียรติ";
            lblHospital.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblHospital.ForeColor = Color.FromArgb(24, 76, 120);
            lblHospital.Location = new Point(x, 15);
            lblHospital.AutoSize = true;
            stripPanel.Controls.Add(lblHospital);

            Label lblName = new Label();
            lblName.Text = _patient.PatientName;
            lblName.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblName.ForeColor = Color.Black;
            lblName.Location = new Point(x, 38);
            lblName.AutoSize = true;
            stripPanel.Controls.Add(lblName);

            Label lblHnAn = new Label();
            lblHnAn.Text = string.Format("HN: {0}        AN: {1}        เพศ: {2}    อายุ: {3} ปี", 
                _patient.HN, _patient.AN, _patient.Gender, _patient.Age);
            lblHnAn.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            lblHnAn.Location = new Point(x, 68);
            lblHnAn.AutoSize = true;
            stripPanel.Controls.Add(lblHnAn);

            Label lblWard = new Label();
            lblWard.Text = string.Format("หอผู้ป่วย: {0}        Admit: {1}", 
                _patient.WardName ?? "-", _patient.RgtDate ?? "-");
            lblWard.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            lblWard.Location = new Point(x, 92);
            lblWard.AutoSize = true;
            stripPanel.Controls.Add(lblWard);

            Label lblAllergy = new Label();
            if (_patient.HasAllergy)
            {
                lblAllergy.Text = "⚠️ ประวัติแพ้ยา: " + _patient.Allergy;
                lblAllergy.ForeColor = Color.Crimson;
                lblAllergy.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            }
            else
            {
                lblAllergy.Text = "✓ ไม่มีประวัติแพ้ยา";
                lblAllergy.ForeColor = Color.ForestGreen;
                lblAllergy.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            }
            lblAllergy.Location = new Point(x, 116);
            lblAllergy.AutoSize = true;
            stripPanel.Controls.Add(lblAllergy);

            // Bottom Buttons
            Button btnPrint = new Button();
            btnPrint.Text = "🖨️ สั่งพิมพ์ (Print)";
            btnPrint.Location = new Point(410, 225);
            btnPrint.Size = new Size(150, 38);
            btnPrint.BackColor = Color.FromArgb(16, 149, 106); // Emerald Green
            btnPrint.ForeColor = Color.White;
            btnPrint.FlatStyle = FlatStyle.Flat;
            btnPrint.FlatAppearance.BorderSize = 0;
            btnPrint.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnPrint.Cursor = Cursors.Hand;
            btnPrint.Click += (s, e) => { PrintRequested = true; this.Close(); };
            this.Controls.Add(btnPrint);

            Button btnClose = new Button();
            btnClose.Text = "ปิด";
            btnClose.Location = new Point(570, 225);
            btnClose.Size = new Size(115, 38);
            btnClose.BackColor = Color.FromArgb(220, 225, 230);
            btnClose.ForeColor = Color.FromArgb(40, 40, 40);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => { this.Close(); };
            this.Controls.Add(btnClose);
        }
    }
}
