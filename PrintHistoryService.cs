using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace TUH_WristbandPrinter
{
    public class PrintHistoryRecord
    {
        public string Id { get; set; }
        public DateTime PrintTime { get; set; }
        public long AN { get; set; }
        public long HN { get; set; }
        public string PatientName { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string WardName { get; set; }
        public string RgtDate { get; set; }
        public string Allergy { get; set; }
        public string WristbandType { get; set; } // "ผู้ใหญ่" or "เด็ก"
        public int Copies { get; set; }
        public string PrinterName { get; set; }
        public string PrintedBy { get; set; }
        public string Status { get; set; }

        public PrintHistoryRecord()
        {
            Id = Guid.NewGuid().ToString("N");
            PrintTime = DateTime.Now;
            Copies = 1;
            WristbandType = "ผู้ใหญ่";
            Status = "พิมพ์สำเร็จ";
        }
    }

    public class PrintHistoryService
    {
        private static readonly object _lock = new object();
        private static PrintHistoryService _instance;
        public static PrintHistoryService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PrintHistoryService();
                return _instance;
            }
        }

        private const int MaxHistoryRecords = 2000;

        private string FilePath
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "print_history.json");
            }
        }

        private PrintHistoryService() { }

        public List<PrintHistoryRecord> GetAll()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(FilePath))
                        return new List<PrintHistoryRecord>();

                    string json = File.ReadAllText(FilePath, Encoding.UTF8);
                    if (string.IsNullOrEmpty(json) || json.Trim() == "[]")
                        return new List<PrintHistoryRecord>();

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    var list = serializer.Deserialize<List<PrintHistoryRecord>>(json);
                    return list ?? new List<PrintHistoryRecord>();
                }
                catch
                {
                    return new List<PrintHistoryRecord>();
                }
            }
        }

        public void AddRecord(PrintHistoryRecord record)
        {
            if (record == null) return;
            lock (_lock)
            {
                try
                {
                    List<PrintHistoryRecord> list = GetAll();
                    // Insert at beginning (newest first)
                    list.Insert(0, record);

                    // Prune old records if exceeding max
                    if (list.Count > MaxHistoryRecords)
                    {
                        list.RemoveRange(MaxHistoryRecords, list.Count - MaxHistoryRecords);
                    }

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    string json = serializer.Serialize(list);
                    File.WriteAllText(FilePath, json, Encoding.UTF8);
                }
                catch { }
            }
        }

        public List<PrintHistoryRecord> Search(string query, string dateFilter)
        {
            List<PrintHistoryRecord> all = GetAll();
            if (all.Count == 0) return all;

            DateTime now = DateTime.Now;
            DateTime? cutoff = null;

            if (dateFilter == "TODAY")
                cutoff = now.Date;
            else if (dateFilter == "3DAYS")
                cutoff = now.Date.AddDays(-3);
            else if (dateFilter == "7DAYS")
                cutoff = now.Date.AddDays(-7);
            else if (dateFilter == "30DAYS")
                cutoff = now.Date.AddDays(-30);

            string q = (query ?? "").Trim().ToLower();

            List<PrintHistoryRecord> filtered = new List<PrintHistoryRecord>();
            foreach (var r in all)
            {
                if (cutoff.HasValue && r.PrintTime < cutoff.Value)
                    continue;

                if (!string.IsNullOrEmpty(q))
                {
                    bool match = false;
                    if (r.HN.ToString().Contains(q)) match = true;
                    else if (r.AN.ToString().Contains(q)) match = true;
                    else if (!string.IsNullOrEmpty(r.PatientName) && r.PatientName.ToLower().Contains(q)) match = true;
                    else if (!string.IsNullOrEmpty(r.WardName) && r.WardName.ToLower().Contains(q)) match = true;
                    else if (!string.IsNullOrEmpty(r.PrintedBy) && r.PrintedBy.ToLower().Contains(q)) match = true;

                    if (!match) continue;
                }

                filtered.Add(r);
            }

            return filtered;
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(FilePath))
                    {
                        File.WriteAllText(FilePath, "[]", Encoding.UTF8);
                    }
                }
                catch { }
            }
        }
    }
}
