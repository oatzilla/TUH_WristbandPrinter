using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TUH_WristbandPrinter
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; }
        public string CurrentVersion { get; set; }
        public string Source { get; set; } // "เครื่องแม่ในวอร์ด (172.30.249.158)" or "GitHub"
        public string DownloadUrl { get; set; }
        public string ReleaseDate { get; set; }
        public string Changelog { get; set; }
        public long FileSize { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class UpdateService
    {
        public const string CurrentVersion = "1.4.4";
        public const string GitHubVersionUrl = "https://raw.githubusercontent.com/oatzilla/TUH_WristbandPrinter/main/version.json";

        private static UpdateService _instance;
        public static UpdateService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new UpdateService();
                return _instance;
            }
        }

        private UpdateService() { }

        public UpdateInfo CheckForUpdate(string preferredServerHost, int serverPort)
        {
            UpdateInfo info = new UpdateInfo
            {
                HasUpdate = false,
                CurrentVersion = CurrentVersion,
                LatestVersion = CurrentVersion
            };

            JavaScriptSerializer serializer = new JavaScriptSerializer();

            // 1. Try checking Ward Central Print Server first
            string hostToTry = preferredServerHost;
            if (string.IsNullOrEmpty(hostToTry))
            {
                try
                {
                    var discovered = PrintClientService.Instance.DiscoverServer(1500);
                    if (discovered != null)
                    {
                        hostToTry = discovered.IP;
                        if (discovered.Port > 0) serverPort = discovered.Port;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(hostToTry))
            {
                try
                {
                    if (serverPort <= 0) serverPort = 8099;
                    string url = string.Format("http://{0}:{1}/api/version", hostToTry.Trim(), serverPort);
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.Timeout = 2500;
                    req.Method = "GET";

                    using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                    using (StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();
                        var data = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (data != null && data.ContainsKey("version"))
                        {
                            string serverVer = data["version"].ToString();
                            if (IsNewerVersion(serverVer, CurrentVersion))
                            {
                                info.HasUpdate = true;
                                info.LatestVersion = serverVer;
                                info.Source = string.Format("เครื่องแม่ในวอร์ด ({0})", hostToTry);
                                info.DownloadUrl = data.ContainsKey("downloadUrl") 
                                    ? data["downloadUrl"].ToString() 
                                    : string.Format("http://{0}:{1}/api/download/exe", hostToTry, serverPort);
                                info.Changelog = data.ContainsKey("changelog") ? data["changelog"].ToString() : "เวอร์ชันใหม่จากเครื่องแม่ในวอร์ด";
                                info.ReleaseDate = data.ContainsKey("releaseDate") ? data["releaseDate"].ToString() : "";
                                return info;
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Try checking GitHub (for Host PC or PCs with internet access)
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 /* Tls12 */ | SecurityProtocolType.Tls;
                HttpWebRequest ghReq = (HttpWebRequest)WebRequest.Create(GitHubVersionUrl);
                ghReq.Timeout = 3500;
                ghReq.UserAgent = "TUH_WristbandPrinter/" + CurrentVersion;
                ghReq.Method = "GET";

                using (HttpWebResponse ghRes = (HttpWebResponse)ghReq.GetResponse())
                using (StreamReader sr = new StreamReader(ghRes.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    var data = serializer.Deserialize<Dictionary<string, object>>(json);
                    if (data != null && data.ContainsKey("version"))
                    {
                        string ghVer = data["version"].ToString();
                        if (IsNewerVersion(ghVer, CurrentVersion))
                        {
                            info.HasUpdate = true;
                            info.LatestVersion = ghVer;
                            info.Source = "GitHub";
                            info.DownloadUrl = data.ContainsKey("downloadUrl") ? data["downloadUrl"].ToString() : "";
                            info.Changelog = data.ContainsKey("changelog") ? data["changelog"].ToString() : "";
                            info.ReleaseDate = data.ContainsKey("releaseDate") ? data["releaseDate"].ToString() : "";
                            return info;
                        }
                    }
                }
            }
            catch { }

            return info;
        }

        public static bool IsNewerVersion(string newVer, string curVer)
        {
            if (string.IsNullOrEmpty(newVer)) return false;
            try
            {
                string[] p1 = newVer.Trim().TrimStart('v', 'V').Split('.');
                string[] p2 = curVer.Trim().TrimStart('v', 'V').Split('.');

                int max = Math.Max(p1.Length, p2.Length);
                for (int i = 0; i < max; i++)
                {
                    int n1 = i < p1.Length ? int.Parse(p1[i]) : 0;
                    int n2 = i < p2.Length ? int.Parse(p2[i]) : 0;
                    if (n1 > n2) return true;
                    if (n1 < n2) return false;
                }
            }
            catch { }
            return false;
        }

        public void DownloadAndApplyUpdate(string downloadUrl, Action<int> progressCallback = null)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new Exception("ไม่พบที่อยู่ดาวน์โหลดไฟล์อัปเดต");

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempExe = Path.Combine(baseDir, "TUH_WristbandPrinter_new.exe");

            // Clean previous temp file if any
            if (File.Exists(tempExe))
            {
                try { File.Delete(tempExe); } catch { }
            }

            // Download file
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 /* Tls12 */ | SecurityProtocolType.Tls;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(downloadUrl);
            req.Timeout = 30000;
            req.UserAgent = "TUH_WristbandPrinter_Updater";

            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (Stream inStream = res.GetResponseStream())
            using (FileStream outStream = new FileStream(tempExe, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long totalBytes = res.ContentLength;
                byte[] buffer = new byte[8192];
                long readTotal = 0;
                int read;

                while ((read = inStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outStream.Write(buffer, 0, read);
                    readTotal += read;
                    if (totalBytes > 0 && progressCallback != null)
                    {
                        int pct = (int)((readTotal * 100) / totalBytes);
                        progressCallback(pct);
                    }
                }
            }

            // Verify downloaded file size
            FileInfo fi = new FileInfo(tempExe);
            if (!fi.Exists || fi.Length < 50000)
            {
                try { File.Delete(tempExe); } catch { }
                throw new Exception("ไฟล์อัปเดตที่ดาวน์โหลดมาไม่สมบูรณ์ (ขนาดไฟล์เล็กเกินไป)");
            }

            // Write updater batch script
            string batchPath = Path.Combine(baseDir, "update_apply.cmd");
            string batchContent = 
                "@echo off\r\n" +
                "chcp 65001 >nul\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                ":retry\r\n" +
                "copy /y \"TUH_WristbandPrinter_new.exe\" \"TUH_WristbandPrinter.exe\" >nul\r\n" +
                "if errorlevel 1 (\r\n" +
                "    timeout /t 1 /nobreak >nul\r\n" +
                "    goto retry\r\n" +
                ")\r\n" +
                "del \"TUH_WristbandPrinter_new.exe\" >nul 2>&1\r\n" +
                "start \"\" \"TUH_WristbandPrinter.exe\"\r\n" +
                "del \"%~f0\" >nul 2>&1\r\n";

            File.WriteAllText(batchPath, batchContent, Encoding.Default);

            // Execute batch script and exit application cleanly
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = batchPath;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.WorkingDirectory = baseDir;

            Process.Start(psi);

            try { Application.Exit(); } catch { }
            Environment.Exit(0);
        }
    }
}
