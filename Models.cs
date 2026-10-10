using System;
using Sybase.DataWindow;

namespace TUH_WristbandPrinter
{
    public class StaffUser
    {
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public string Department { get; set; }
    }

    public enum WristbandType
    {
        Adult = 1,
        Child = 2
    }

    public class PatientAdmission
    {
        public long AN { get; set; }
        public long HN { get; set; }
        public string PatientName { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string IdCard { get; set; }
        public string WardName { get; set; }
        public string RgtDate { get; set; }
        public string PtTypeName { get; set; }
        public string Allergy { get; set; }
        public string QrCodeText { get; set; }
        public string QrCodeImagePath { get; set; }

        public bool HasAllergy
        {
            get
            {
                return !string.IsNullOrEmpty(Allergy) && Allergy.Trim() != "-" && Allergy.Trim() != "[None]";
            }
        }
    }

    public class QueueItem
    {
        public bool IsSelected { get; set; }
        public PatientAdmission Patient { get; set; }
        public DataStore DataStore { get; set; }
        public WristbandType Type { get; set; }
        public int Copies { get; set; }
        public DateTime AddedTime { get; set; }
        public string Status { get; set; } // "รอพิมพ์", "กำลังพิมพ์...", "พิมพ์สำเร็จ", "ผิดพลาด"

        public QueueItem()
        {
            IsSelected = true;
            Copies = 1;
            AddedTime = DateTime.Now;
            Status = "รอพิมพ์";
            Type = WristbandType.Adult;
        }
    }
}
