using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Remoting;
using AbsTools.CliNmSpace;
using AbsTools.Srv;
using AbsTools.SrvCliNmSpace;
using AbsTools.UI;
using Sybase.DataWindow;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace TUH_WristbandPrinter
{
    public class EphisService
    {
        private static EphisService _instance;
        public static EphisService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new EphisService();
                return _instance;
            }
        }

        private MainSrvLib _srv;
        private bool _isConfigured = false;

        private EphisService()
        {
            InitializeRemoting();
        }

        private void InitializeRemoting()
        {
            if (_isConfigured) return;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(baseDir, "mnphis.exe.config");
            if (!File.Exists(configPath))
            {
                configPath = @"C:\PhisApp\Login_exe_TUH\mnphis.exe.config";
            }

            if (File.Exists(configPath))
            {
                RemotingConfiguration.Configure(configPath, false);
                _isConfigured = true;
            }
            _srv = new MainSrvLib();
        }

        public StaffUser Login(string userId, string password)
        {
            InitializeRemoting();

            UserAuth auth = new UserAuth(_srv);
            auth.CreateHptProp();

            EUserAuthResult result = auth.VerifyLog(userId, password);
            if (result != EUserAuthResult.Success)
            {
                string msg = "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง";
                if (result == EUserAuthResult.ConnectionFailure)
                    msg = "ไม่สามารถเชื่อมต่อเครื่องแม่ข่ายโรงพยาบาลได้";
                else if (result == EUserAuthResult.PassExp)
                    msg = "รหัสผ่านหมดอายุ กรุณาติดต่อผู้ดูแลระบบ";

                throw new Exception(msg);
            }

            StaffUser staff = new StaffUser();
            staff.UserId = userId;
            staff.DisplayName = userId;

            try
            {
                if (auth.SysInfo != null && !string.IsNullOrEmpty(auth.SysInfo.DspName))
                {
                    staff.DisplayName = auth.SysInfo.DspName;
                }
                else
                {
                    // Fallback: Query phisuser table
                    string sql = string.Format("select dspname, dct from phisuser where userid = '{0}'", userId.Replace("'", "''"));
                    RemoteDfRet ret = _srv.GetRunTimeDataWindow(sql, (FlagNonQuery)1);
                    if (ret.RowCount > 0 && ret.FullState != null)
                    {
                        DataStore ds = new DataStore();
                        ds.SetFullState(ret.FullState);
                        if (ds.RowCount > 0)
                        {
                            staff.DisplayName = ds.GetItemString(1, 1);
                        }
                    }
                }
            }
            catch { }

            return staff;
        }

        public List<PatientAdmission> SearchAdmissions(string input)
        {
            InitializeRemoting();
            List<PatientAdmission> list = new List<PatientAdmission>();

            long num = 0;
            if (!long.TryParse(input.Trim(), out num))
            {
                throw new Exception("กรุณาระบุ HN หรือ AN เป็นตัวเลขเท่านั้น");
            }

            // 1. Try search as AN first
            string sqlAn = string.Format(
                "select ipt.an, ipt.hn, ipt.rgtdate, pt.dspname, ward.dspname as wardname " +
                "from ipt " +
                "join pt on ipt.hn = pt.hn " +
                "left outer join ward on ipt.ward = ward.ward " +
                "where ipt.an = {0} and ipt.canceldate is null", num);

            try
            {
                RemoteDfRet retAn = _srv.GetRunTimeDataWindow(sqlAn, (FlagNonQuery)1);
                if (retAn.RowCount > 0 && retAn.FullState != null)
                {
                    DataStore ds = new DataStore();
                    ds.SetFullState(retAn.FullState);
                    for (int i = 1; i <= ds.RowCount; i++)
                    {
                        PatientAdmission adm = new PatientAdmission();
                        adm.AN = (long)ds.GetItemDouble(i, 1);
                        adm.HN = (long)ds.GetItemDouble(i, 2);
                        adm.RgtDate = ds.GetItemDateTime(i, 3).ToString("dd/MM/yyyy HH:mm");
                        adm.PatientName = ds.GetItemString(i, 4);
                        if (!ds.IsItemNull(i, 5)) adm.WardName = ds.GetItemString(i, 5);
                        list.Add(adm);
                    }
                    if (list.Count > 0) return list;
                }
            }
            catch { }

            // 2. Search as HN
            string sqlHn = string.Format(
                "select ipt.an, ipt.hn, ipt.rgtdate, pt.dspname, ward.dspname as wardname " +
                "from ipt " +
                "join pt on ipt.hn = pt.hn " +
                "left outer join ward on ipt.ward = ward.ward " +
                "where ipt.hn = {0} and ipt.canceldate is null " +
                "order by ipt.rgtdate desc", num);

            try
            {
                RemoteDfRet retHn = _srv.GetRunTimeDataWindow(sqlHn, (FlagNonQuery)1);
                if (retHn.RowCount > 0 && retHn.FullState != null)
                {
                    DataStore ds = new DataStore();
                    ds.SetFullState(retHn.FullState);
                    for (int i = 1; i <= ds.RowCount; i++)
                    {
                        PatientAdmission adm = new PatientAdmission();
                        adm.AN = (long)ds.GetItemDouble(i, 1);
                        adm.HN = (long)ds.GetItemDouble(i, 2);
                        try
                        {
                            adm.RgtDate = ds.GetItemDateTime(i, 3).ToString("dd/MM/yyyy HH:mm");
                        }
                        catch
                        {
                            adm.RgtDate = ds.GetItemString(i, 3);
                        }
                        adm.PatientName = ds.GetItemString(i, 4);
                        if (!ds.IsItemNull(i, 5)) adm.WardName = ds.GetItemString(i, 5);
                        list.Add(adm);
                    }
                }
            }
            catch { }

            return list;
        }

        #region Safe DataStore Helper Methods

        private static bool HasColumn(DataStore ds, string colName)
        {
            if (ds == null || string.IsNullOrEmpty(colName)) return false;
            try
            {
                string desc = ds.Describe(colName + ".Name");
                if (!string.IsNullOrEmpty(desc) && desc != "!" && desc != "?")
                    return true;
            }
            catch { }
            return false;
        }

        private static string SafeGetString(DataStore ds, int row, string colName, string defVal = "")
        {
            if (ds == null || row <= 0 || row > ds.RowCount) return defVal;
            try
            {
                if (HasColumn(ds, colName) && !ds.IsItemNull(row, colName))
                {
                    string val = ds.GetItemString(row, colName);
                    return val != null ? val : defVal;
                }
            }
            catch { }
            return defVal;
        }

        private static double SafeGetDouble(DataStore ds, int row, string colName, double defVal = 0)
        {
            if (ds == null || row <= 0 || row > ds.RowCount) return defVal;
            try
            {
                if (HasColumn(ds, colName) && !ds.IsItemNull(row, colName))
                {
                    return ds.GetItemDouble(row, colName);
                }
            }
            catch { }
            return defVal;
        }

        private static string SafeGetDate(DataStore ds, int row, string colName, string format = "dd/MM/yyyy")
        {
            if (ds == null || row <= 0 || row > ds.RowCount) return "";
            try
            {
                if (HasColumn(ds, colName) && !ds.IsItemNull(row, colName))
                {
                    return ds.GetItemDateTime(row, colName).ToString(format);
                }
            }
            catch
            {
                try
                {
                    string s = ds.GetItemString(row, colName);
                    return s ?? "";
                }
                catch { }
            }
            return "";
        }

        #endregion

        public PatientAdmission LoadWristband(long an, WristbandType type, out DataStore dataStore)
        {
            InitializeRemoting();

            string dwName = (type == WristbandType.Adult) 
                ? "dw_doc_sticker_listband_001" 
                : "dw_doc_sticker_listband_child";

            RemoteDw[] remoteDws = new RemoteDw[1];
            remoteDws[0] = new RemoteDw();
            remoteDws[0].Name = dwName;
            remoteDws[0].PBLName = @"ipdmns\wdiprs\wdiprs.doc.pbl";
            remoteDws[0].RetrievalArguments = new object[] { (int)an };

            RemoteDfRet[] ret = _srv.GetFullStateDws(remoteDws);
            if (ret == null || ret.Length == 0 || ret[0].FullState == null || ret[0].RowCount == 0)
            {
                throw new Exception(string.Format("ไม่พบข้อมูลสายรัดข้อมือสำหรับ AN: {0}", an));
            }

            DataStore ds = new DataStore();
            ds.SetFullState(ret[0].FullState);

            PatientAdmission patient = new PatientAdmission();
            patient.AN = (long)SafeGetDouble(ds, 1, "an", an);
            patient.HN = (long)SafeGetDouble(ds, 1, "hn", 0);
            patient.PatientName = SafeGetString(ds, 1, "dspname", "");

            // Ward name
            patient.WardName = SafeGetString(ds, 1, "pt_dspname", "");
            if (string.IsNullOrEmpty(patient.WardName))
                patient.WardName = SafeGetString(ds, 1, "wardname", "");

            patient.Age = SafeGetString(ds, 1, "age", "");
            patient.Gender = SafeGetString(ds, 1, "male_name", "");
            patient.IdCard = SafeGetString(ds, 1, "ptno_pt_idc", "");
            patient.RgtDate = SafeGetDate(ds, 1, "rgtdate", "dd/MM/yyyy");

            // Patient type / rights
            patient.PtTypeName = SafeGetString(ds, 1, "pttypenm", "");
            if (string.IsNullOrEmpty(patient.PtTypeName))
                patient.PtTypeName = SafeGetString(ds, 1, "pttype_name", "");

            // Allergy
            patient.Allergy = SafeGetString(ds, 1, "allergynm", "-");
            if (string.IsNullOrEmpty(patient.Allergy))
                patient.Allergy = SafeGetString(ds, 1, "allergy", "-");
            if (string.IsNullOrEmpty(patient.Allergy))
                patient.Allergy = "-";

            // QR Code Generation
            string qrText = SafeGetString(ds, 1, "s_qrcode", "").Trim();
            if (string.IsNullOrEmpty(qrText))
                qrText = SafeGetString(ds, 1, "qrcode", "").Trim();
            if (string.IsNullOrEmpty(qrText))
                qrText = an.ToString();

            patient.QrCodeText = qrText;

            // Generate QR Code bitmap via ZXing
            try
            {
                QrCodeEncodingOptions options = new QrCodeEncodingOptions();
                options.CharacterSet = "UTF-8";
                options.Margin = 0;
                options.DisableECI = true;
                options.Height = 55;
                options.Width = 55;

                BarcodeWriter writer = new BarcodeWriter();
                writer.Format = BarcodeFormat.QR_CODE;
                writer.Options = options;

                Bitmap bmp = writer.Write(qrText);
                string tempPath = Path.Combine(Path.GetTempPath(), string.Format("{0}_qrcode.jpg", an));
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                bmp.Save(tempPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                patient.QrCodeImagePath = tempPath;

                ds.SetProperty("p_qrcode.Filename", tempPath);
            }
            catch { }

            dataStore = ds;
            return patient;
        }

        public void PrintWristband(DataStore ds, string printerName, int copies)
        {
            if (ds == null || ds.RowCount == 0)
                throw new Exception("ไม่มีข้อมูลสายรัดข้อมือสำหรับการพิมพ์");

            if (!string.IsNullOrEmpty(printerName))
            {
                ds.Modify(string.Format("DataWindow.Printer='{0}'", printerName));
            }

            if (copies <= 0) copies = 1;

            // Duplicate rows if copies > 1
            for (int i = 1; i < copies; i++)
            {
                ds.RowsCopy(1, 1, DataBuffer.Primary, ds, 1, DataBuffer.Primary);
            }

            ds.Print();
        }
    }
}
