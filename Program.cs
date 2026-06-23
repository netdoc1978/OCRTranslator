using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace OCRTranslator
{
    internal static class Program
    {
        private static Mutex mutex = null;

        [STAThread]
        static void Main()
        {
            const string mutexName = "OCRTranslator_SingleInstance_Mutex";

            bool createdNew;
            mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("程序已在运行中！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());

            if (mutex != null)
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                string exePath = Application.ExecutablePath;
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                        key.SetValue("OCRTranslator", string.Format("\"{0}\"", exePath));
                    else
                        key.DeleteValue("OCRTranslator", false);
                    key.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置开机自启动失败: " + ex.Message, "错误");
            }
        }

        public static bool GetAutoStartStatus()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                if (key != null)
                {
                    object value = key.GetValue("OCRTranslator");
                    key.Close();
                    return value != null;
                }
            }
            catch { }
            return false;
        }
    }
}
