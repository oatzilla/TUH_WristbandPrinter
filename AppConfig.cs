using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TUH_WristbandPrinter
{
    public class AppConfig
    {
        public string SelectedPrinter { get; set; }
        public int Copies { get; set; }
        public bool AutoPrintOnScan { get; set; }
        public string DefaultType { get; set; } // "Adult" or "Child"
        public string LastUserId { get; set; }
        public bool RememberUser { get; set; }
        public bool StayLoggedIn { get; set; }
        public string EncryptedPassword { get; set; }
        public string WindowMode { get; set; } // "Normal" or "Sidebar"
        public bool AlwaysOnTop { get; set; }

        // Central Print Server settings
        public string PrintMode { get; set; } // "Local" or "Remote"
        public string RemoteServerHost { get; set; }
        public int RemoteServerPort { get; set; }
        public bool EnablePrintServer { get; set; }
        public int ServerPort { get; set; }

        // Auto Update settings
        public bool AutoCheckUpdate { get; set; }

        private static string ConfigPath
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "printer_config.ini");
            }
        }

        public AppConfig()
        {
            SelectedPrinter = "";
            Copies = 1;
            AutoPrintOnScan = false;
            DefaultType = "Adult";
            LastUserId = "";
            RememberUser = true;
            StayLoggedIn = true;
            EncryptedPassword = "";
            WindowMode = "Normal";
            AlwaysOnTop = false;

            PrintMode = "Local";
            RemoteServerHost = "";
            RemoteServerPort = 8099;
            EnablePrintServer = true;
            ServerPort = 8099;

            AutoCheckUpdate = true;
        }

        public static string EncryptSecret(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plain);
                byte[] enc = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch { return ""; }
        }

        public static string DecryptSecret(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return "";
            try
            {
                byte[] enc = Convert.FromBase64String(cipher);
                byte[] data = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch { return ""; }
        }

        public static AppConfig Load()
        {
            AppConfig config = new AppConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string[] lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || !line.Contains("="))
                            continue;

                        int idx = line.IndexOf('=');
                        string key = line.Substring(0, idx).Trim();
                        string val = line.Substring(idx + 1).Trim();

                        switch (key.ToLower())
                        {
                            case "selectedprinter":
                                config.SelectedPrinter = val;
                                break;
                            case "copies":
                                int c;
                                if (int.TryParse(val, out c) && c > 0) config.Copies = c;
                                break;
                            case "autoprintonscan":
                                bool auto;
                                if (bool.TryParse(val, out auto)) config.AutoPrintOnScan = auto;
                                break;
                            case "defaulttype":
                                config.DefaultType = "Adult"; // Enforce Adult
                                break;
                            case "lastuserid":
                                config.LastUserId = val;
                                break;
                            case "rememberuser":
                                bool rem;
                                if (bool.TryParse(val, out rem)) config.RememberUser = rem;
                                break;
                            case "stayloggedin":
                                bool stay;
                                if (bool.TryParse(val, out stay)) config.StayLoggedIn = stay;
                                break;
                            case "encryptedpassword":
                                config.EncryptedPassword = val;
                                break;
                            case "windowmode":
                                config.WindowMode = val;
                                break;
                            case "alwaysontop":
                                bool top;
                                if (bool.TryParse(val, out top)) config.AlwaysOnTop = top;
                                break;
                            case "printmode":
                                config.PrintMode = val;
                                break;
                            case "remoteserverhost":
                                config.RemoteServerHost = val;
                                break;
                            case "remoteserverport":
                                int rp;
                                if (int.TryParse(val, out rp) && rp > 0) config.RemoteServerPort = rp;
                                break;
                            case "enableprintserver":
                                bool es;
                                if (bool.TryParse(val, out es)) config.EnablePrintServer = es;
                                break;
                            case "serverport":
                                int sp;
                                if (int.TryParse(val, out sp) && sp > 0) config.ServerPort = sp;
                                break;
                            case "autocheckupdate":
                                bool acu;
                                if (bool.TryParse(val, out acu)) config.AutoCheckUpdate = acu;
                                break;
                        }
                    }
                }
            }
            catch { }
            return config;
        }

        public void Save()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(ConfigPath, false, Encoding.UTF8))
                {
                    sw.WriteLine("# TUH Wristband Printer Configuration");
                    sw.WriteLine("SelectedPrinter=" + (SelectedPrinter ?? ""));
                    sw.WriteLine("Copies=" + Copies);
                    sw.WriteLine("AutoPrintOnScan=" + AutoPrintOnScan);
                    sw.WriteLine("DefaultType=" + (DefaultType ?? "Adult"));
                    sw.WriteLine("LastUserId=" + (LastUserId ?? ""));
                    sw.WriteLine("RememberUser=" + RememberUser);
                    sw.WriteLine("StayLoggedIn=" + StayLoggedIn);
                    sw.WriteLine("EncryptedPassword=" + (EncryptedPassword ?? ""));
                    sw.WriteLine("WindowMode=" + (WindowMode ?? "Normal"));
                    sw.WriteLine("AlwaysOnTop=" + AlwaysOnTop);
                    sw.WriteLine("PrintMode=" + (PrintMode ?? "Local"));
                    sw.WriteLine("RemoteServerHost=" + (RemoteServerHost ?? ""));
                    sw.WriteLine("RemoteServerPort=" + RemoteServerPort);
                    sw.WriteLine("EnablePrintServer=" + EnablePrintServer);
                    sw.WriteLine("ServerPort=" + ServerPort);
                    sw.WriteLine("AutoCheckUpdate=" + AutoCheckUpdate);
                }
            }
            catch { }
        }
    }
}
