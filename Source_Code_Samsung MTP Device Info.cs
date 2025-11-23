using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Samsung_MTP_Device_Info
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }
        private void frmMain_Load(object sender, EventArgs e)
        {

        }
        private async void btnReadInfo_Click(object sender, EventArgs e)
        {
            rtInfo.Clear();
            rtInfo.AppendText("Searching for Samsung modem port...");

            string port = DetectSamsungModemPort();
            if (port == null)
            {
                rtInfo.AppendText("NOT FOUND\n"); //​រក ss modem port អត់ឃើញ
                return;
            }

            rtInfo.AppendText($"{port}\n\n");
            rtInfo.AppendText("Reading device info...\n\n");

            SamsungDeviceInfo info = await ReadSamsungDeviceInfo(port);

            if (info == null)
            {
                rtInfo.AppendText("Failed to read information.\n");
                return;
            }

            rtInfo.AppendText(
                $"Model: {info.Model}\n" +
                $"IMEI: {info.IMEI}\n" +
                $"SN: {info.SN}\n" +
                $"AP: {info.AP}\n" +
                $"BL: {info.BL}\n" +
                $"CP: {info.CP}\n" +
                $"CSC: {info.CSC}\n" +
                $"Region: {info.Region}\n" +
                $"Country: {info.Country}\n" +
                $"MCC: {info.MCC}\n" +
                $"MNC: {info.MNC}\n" +
                $"USB Mode: {info.UsbMode}\n" +
                $"Unique Number: {info.UQN}\n" +
                $"Android Version: {info.AndroidVersion}\n" +
                $"FRP Status: {info.FRP}\n"
            );
        }

        private void rtInfo_TextChanged(object sender, EventArgs e) { }
        private string DetectSamsungModemPort()
        {
            try
            {
                ManagementClass mc = new ManagementClass("Win32_SerialPort");
                foreach (ManagementBaseObject obj in mc.GetInstances())
                {
                    string caption = obj["Caption"]?.ToString() ?? "";
                    string port = obj["DeviceID"]?.ToString() ?? "";

                    if (caption.Contains("SAMSUNG Mobile USB Modem"))
                        return port;
                }
            }
            catch { }
            return null;
        }
        private string Clean(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return "";
            return v
                .Replace("OK", "")
                .Replace(");", "")
                .Replace("(", " ")
                .Replace(")", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();
        }
        private string ExtractClean(string src, string key)
        {
            string value = Regex.Match(src, key + "\\((.+?)\\)").Groups[1].Value;
            return Clean(value);
        }
        private string FastAT(SerialPort sp, string cmd, int timeoutMs = 4000)
        {
            sp.DiscardInBuffer();
            sp.Write(cmd + "\r");

            StringBuilder sb = new StringBuilder();
            int waited = 0;

            while (waited < timeoutMs)
            {
                string data = sp.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    sb.Append(data);

                    if (sb.ToString().Contains("OK") || sb.ToString().Contains("#OK#"))
                        break;
                }

                Thread.Sleep(10);
                waited += 10;
            }

            return sb.ToString();
        }
        private SamsungDeviceInfo ParseDevConInfo(string raw)
        {
            SamsungDeviceInfo d = new SamsungDeviceInfo();

            d.Model = ExtractClean(raw, "MN");
            d.IMEI = ExtractClean(raw, "IMEI");
            d.SN = ExtractClean(raw, "SN");
            d.Region = ExtractClean(raw, "PRD");
            d.Country = ExtractClean(raw, "CC");
            d.MCC = ExtractClean(raw, "MCC");
            d.MNC = ExtractClean(raw, "MNC");
            d.UQN = ExtractClean(raw, "UN");
            d.UsbMode = ExtractClean(raw, "CON");

            string ver = ExtractClean(raw, "VER");
            if (!string.IsNullOrEmpty(ver))
            {
                var parts = ver.Split('/');
                if (parts.Length == 4)
                {
                    d.AP = Clean(parts[0]);
                    d.BL = Clean(parts[1]);
                    d.CP = Clean(parts[2]);
                    d.CSC = Clean(parts[3]);
                }
            }

            return d;
        }
        private async Task<SamsungDeviceInfo> ReadSamsungDeviceInfo(string comPort)
        {
            SamsungDeviceInfo device = new SamsungDeviceInfo();

            using (SerialPort sp = new SerialPort(comPort, 115200))
            {
                sp.NewLine = "\r\n";
                sp.ReadTimeout = 3000;

                try { sp.Open(); }
                catch
                {
                    return null;
                }

                await Task.Delay(200);
                string raw = FastAT(sp, "AT+DEVCONINFO", 6000);

                if (raw.Length < 20 || !raw.Contains("VER"))
                    return null;

                device = ParseDevConInfo(raw);
                string av = FastAT(sp, "AT+VERSNAME=3,2,3", 4000);
                if (av.Contains("VERSNAME:3,"))
                {
                    device.AndroidVersion = Clean(av.Substring(av.LastIndexOf(":3,") + 3));
                }
                string frp = FastAT(sp, "AT+REACTIVE=1,0,0", 4000);
                if (frp.Contains("REACTIVE:1,"))
                {
                    device.FRP = Clean(frp.Substring(frp.LastIndexOf(":1,") + 3));
                }
                return device;
            }
        }
        class SamsungDeviceInfo
        {
            public string IMEI, SN, Model, AP, BL, CP, CSC, FRP,
                MCC, MNC, Country, Region, UQN, UsbMode, AndroidVersion;
        }
    }
}