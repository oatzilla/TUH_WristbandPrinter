using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web.Script.Serialization;

namespace TUH_WristbandPrinter
{
    public class DiscoveredServerInfo
    {
        public string IP { get; set; }
        public string MachineName { get; set; }
        public string PrinterName { get; set; }
        public int Port { get; set; }

        public string DisplayText
        {
            get
            {
                return string.Format("{0} ({1}) - เครื่องพิมพ์: {2}", MachineName, IP, 
                    string.IsNullOrEmpty(PrinterName) ? "ไม่ได้ระบุ" : PrinterName);
            }
        }
    }

    public class PrintClientService
    {
        private static PrintClientService _instance;
        public static PrintClientService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PrintClientService();
                return _instance;
            }
        }

        private const int DiscoveryPort = 8098;

        private PrintClientService() { }

        public DiscoveredServerInfo DiscoverServer(int timeoutMs = 2500)
        {
            try
            {
                using (UdpClient udp = new UdpClient())
                {
                    udp.EnableBroadcast = true;
                    udp.Client.ReceiveTimeout = timeoutMs;

                    byte[] ping = Encoding.UTF8.GetBytes("TUH_DISCOVER_PRINT_SERVER");
                    IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                    udp.Send(ping, ping.Length, broadcastEP);

                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] response = udp.Receive(ref remoteEP);
                    string reply = Encoding.UTF8.GetString(response);

                    if (reply.StartsWith("TUH_SERVER_HERE|"))
                    {
                        string[] parts = reply.Split('|');
                        if (parts.Length >= 5)
                        {
                            int port;
                            int.TryParse(parts[4], out port);
                            if (port <= 0) port = 8099;

                            return new DiscoveredServerInfo
                            {
                                IP = parts[1],
                                MachineName = parts[2],
                                PrinterName = parts[3],
                                Port = port
                            };
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public ServerStatusPayload TestConnection(string host, int port)
        {
            if (string.IsNullOrEmpty(host))
                throw new Exception("กรุณาระบุ IP หรือชื่อเครื่องแม่");

            if (port <= 0) port = 8099;

            string url = string.Format("http://{0}:{1}/api/status", host.Trim(), port);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 4000;
            req.Method = "GET";

            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                return serializer.Deserialize<ServerStatusPayload>(json);
            }
        }

        public PrintResponsePayload SendPrintJob(string host, int port, PrintRequestPayload payload)
        {
            if (string.IsNullOrEmpty(host))
                throw new Exception("กรุณาระบุ IP หรือชื่อเครื่องแม่");

            if (port <= 0) port = 8099;

            string url = string.Format("http://{0}:{1}/api/print", host.Trim(), port);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 12000; // Allow 12 seconds for remoting & print execution
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string jsonBody = serializer.Serialize(payload);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            req.ContentLength = bodyBytes.Length;

            using (Stream reqStream = req.GetRequestStream())
            {
                reqStream.Write(bodyBytes, 0, bodyBytes.Length);
            }

            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
            {
                string resJson = sr.ReadToEnd();
                return serializer.Deserialize<PrintResponsePayload>(resJson);
            }
        }
    }
}
