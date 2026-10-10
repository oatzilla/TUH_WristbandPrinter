using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace TUH_WristbandPrinter
{
    public class PrintRequestPayload
    {
        public long AN { get; set; }
        public string Type { get; set; } // "Adult" or "Child"
        public int Copies { get; set; }
        public string ClientMachine { get; set; }
        public string ClientUser { get; set; }
    }

    public class PrintResponsePayload
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string PatientName { get; set; }
        public long AN { get; set; }
    }

    public class ServerStatusPayload
    {
        public string Status { get; set; }
        public string MachineName { get; set; }
        public string IP { get; set; }
        public int Port { get; set; }
        public string SelectedPrinter { get; set; }
        public string Version { get; set; }
    }

    public class PrintServerService
    {
        private static PrintServerService _instance;
        public static PrintServerService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PrintServerService();
                return _instance;
            }
        }

        private TcpListener _tcpListener;
        private UdpClient _udpListener;
        private Thread _tcpThread;
        private Thread _udpThread;
        private bool _isRunning = false;
        private int _port = 8099;
        private const int DiscoveryPort = 8098;

        public bool IsRunning { get { return _isRunning; } }
        public int Port { get { return _port; } }

        public Func<PrintRequestPayload, PrintResponsePayload> OnPrintRequested;
        public Func<ServerStatusPayload> OnStatusRequested;

        private PrintServerService() { }

        public void Start(int port, Func<PrintRequestPayload, PrintResponsePayload> printHandler, Func<ServerStatusPayload> statusHandler)
        {
            if (_isRunning) return;

            _port = port;
            OnPrintRequested = printHandler;
            OnStatusRequested = statusHandler;

            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, _port);
                _tcpListener.Start();
                _isRunning = true;

                _tcpThread = new Thread(ListenTcpConnections);
                _tcpThread.IsBackground = true;
                _tcpThread.Start();

                // Start UDP discovery responder
                StartUdpDiscovery();
            }
            catch (Exception ex)
            {
                _isRunning = false;
                throw new Exception("ไม่สามารถเริ่ม Print Server บนพอร์ต " + _port + ": " + ex.Message);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try { if (_tcpListener != null) _tcpListener.Stop(); } catch { }
            try { if (_udpListener != null) _udpListener.Close(); } catch { }
        }

        private void StartUdpDiscovery()
        {
            try
            {
                _udpListener = new UdpClient(DiscoveryPort);
                _udpThread = new Thread(ListenUdpDiscovery);
                _udpThread.IsBackground = true;
                _udpThread.Start();
            }
            catch { }
        }

        private void ListenUdpDiscovery()
        {
            while (_isRunning)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpListener.Receive(ref remoteEP);
                    string msg = Encoding.UTF8.GetString(data);

                    if (msg.Trim() == "TUH_DISCOVER_PRINT_SERVER")
                    {
                        string ip = GetLocalIPAddress();
                        string machine = Environment.MachineName;
                        string printer = "";
                        if (OnStatusRequested != null)
                        {
                            var stat = OnStatusRequested();
                            if (stat != null) printer = stat.SelectedPrinter ?? "";
                        }

                        string reply = string.Format("TUH_SERVER_HERE|{0}|{1}|{2}|{3}", ip, machine, printer, _port);
                        byte[] replyBytes = Encoding.UTF8.GetBytes(reply);
                        _udpListener.Send(replyBytes, replyBytes.Length, remoteEP);
                    }
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private void ListenTcpConnections()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _tcpListener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClientRequest, client);
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private void HandleClientRequest(object obj)
        {
            TcpClient client = (TcpClient)obj;
            try
            {
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                {
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(requestLine)) return;

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0].ToUpper();
                    string path = parts[1];

                    // Read headers
                    int contentLength = 0;
                    string headerLine;
                    while (!string.IsNullOrEmpty(headerLine = reader.ReadLine()))
                    {
                        if (headerLine.ToLower().StartsWith("content-length:"))
                        {
                            int.TryParse(headerLine.Substring(15).Trim(), out contentLength);
                        }
                    }

                    // CORS preflight
                    if (method == "OPTIONS")
                    {
                        SendHttpResponse(writer, 200, "OK", "application/json", "{}");
                        return;
                    }

                    JavaScriptSerializer serializer = new JavaScriptSerializer();

                    if (method == "GET" && (path == "/" || path == "/api/status"))
                    {
                        ServerStatusPayload status = null;
                        if (OnStatusRequested != null)
                            status = OnStatusRequested();

                        if (status == null)
                        {
                            status = new ServerStatusPayload
                            {
                                Status = "online",
                                MachineName = Environment.MachineName,
                                IP = GetLocalIPAddress(),
                                Port = _port,
                                SelectedPrinter = "",
                                Version = "1.2.0"
                            };
                        }

                        string json = serializer.Serialize(status);
                        SendHttpResponse(writer, 200, "OK", "application/json", json);
                    }
                    else if (method == "POST" && path == "/api/print")
                    {
                        string body = "";
                        if (contentLength > 0)
                        {
                            char[] buffer = new char[contentLength];
                            int read = 0;
                            while (read < contentLength)
                            {
                                int chunk = reader.Read(buffer, read, contentLength - read);
                                if (chunk <= 0) break;
                                read += chunk;
                            }
                            body = new string(buffer, 0, read);
                        }

                        PrintResponsePayload resp;
                        try
                        {
                            PrintRequestPayload req = serializer.Deserialize<PrintRequestPayload>(body);
                            if (req == null)
                                throw new Exception("ข้อมูลคำขอพิมพ์ไม่ถูกต้อง");

                            if (OnPrintRequested != null)
                                resp = OnPrintRequested(req);
                            else
                                resp = new PrintResponsePayload { Success = false, Message = "Print handler not registered" };
                        }
                        catch (Exception ex)
                        {
                            resp = new PrintResponsePayload { Success = false, Message = "เกิดข้อผิดพลาดในการพิมพ์: " + ex.Message };
                        }

                        string respJson = serializer.Serialize(resp);
                        int statusCode = resp.Success ? 200 : 500;
                        SendHttpResponse(writer, statusCode, resp.Success ? "OK" : "Error", "application/json", respJson);
                    }
                    else
                    {
                        SendHttpResponse(writer, 404, "Not Found", "text/plain", "Endpoint not found");
                    }
                }
            }
            catch { }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private void SendHttpResponse(StreamWriter writer, int statusCode, string statusMsg, string contentType, string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            writer.WriteLine(string.Format("HTTP/1.1 {0} {1}", statusCode, statusMsg));
            writer.WriteLine(string.Format("Content-Type: {0}; charset=utf-8", contentType));
            writer.WriteLine("Content-Length: " + bodyBytes.Length);
            writer.WriteLine("Access-Control-Allow-Origin: *");
            writer.WriteLine("Access-Control-Allow-Methods: GET, POST, OPTIONS");
            writer.WriteLine("Access-Control-Allow-Headers: Content-Type");
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.Write(body);
            writer.Flush();
        }

        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        string str = ip.ToString();
                        // Prefer typical hospital intranet IP ranges (172.x, 192.168.x, 10.x)
                        if (str.StartsWith("172.") || str.StartsWith("192.168.") || str.StartsWith("10."))
                            return str;
                    }
                }
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }
    }
}
