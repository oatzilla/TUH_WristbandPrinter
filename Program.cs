using System;
using System.Windows.Forms;

namespace TUH_WristbandPrinter
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                AppConfig config = AppConfig.Load();
                StaffUser autoUser = null;

                // Try silent auto-login if stay logged in is enabled
                if (config.StayLoggedIn && !string.IsNullOrEmpty(config.LastUserId) && !string.IsNullOrEmpty(config.EncryptedPassword))
                {
                    try
                    {
                        string plainPwd = AppConfig.DecryptSecret(config.EncryptedPassword);
                        if (!string.IsNullOrEmpty(plainPwd))
                        {
                            autoUser = EphisService.Instance.Login(config.LastUserId, plainPwd);
                        }
                    }
                    catch
                    {
                        autoUser = null;
                    }
                }

                if (autoUser != null)
                {
                    Application.Run(new MainForm(autoUser));
                }
                else
                {
                    LoginForm login = new LoginForm();
                    if (login.ShowDialog() == DialogResult.OK)
                    {
                        Application.Run(new MainForm(login.AuthenticatedUser));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("เกิดข้อผิดพลาดในการเริ่มต้นโปรแกรม:\n" + ex.Message, 
                    "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
