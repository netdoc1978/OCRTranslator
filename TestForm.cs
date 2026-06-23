using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace OCRTranslator
{
    public class TestForm : Form
    {
        private Button btnTest;
        private TextBox txtResult;
        
        public TestForm()
        {
            this.Text = "WebClient测试";
            this.ClientSize = new System.Drawing.Size(700, 500);
            
            Label lbl = new Label();
            lbl.Text = "使用WebClient测试:";
            lbl.Location = new System.Drawing.Point(10, 10);
            lbl.AutoSize = true;
            
            btnTest = new Button();
            btnTest.Text = "WebClient测试";
            btnTest.Location = new System.Drawing.Point(10, 40);
            btnTest.Size = new System.Drawing.Size(120, 30);
            btnTest.Click += (s, e) => RunTest();
            
            txtResult = new TextBox();
            txtResult.Location = new System.Drawing.Point(10, 80);
            txtResult.Size = new System.Drawing.Size(660, 400);
            txtResult.Multiline = true;
            txtResult.ScrollBars = ScrollBars.Vertical;
            txtResult.Font = new System.Drawing.Font("Consolas", 9);
            
            this.Controls.Add(lbl);
            this.Controls.Add(btnTest);
            this.Controls.Add(txtResult);
        }
        
        private void RunTest()
        {
            txtResult.Clear();
            
            try
            {
                Log("=== 使用WebClient测试 ===\n");
                
                // 1. 创建测试图片
                Log("1. 创建测试图片...");
                byte[] imageBytes;
                using (Bitmap bmp = new Bitmap(300, 100))
                using (Graphics g = Graphics.FromImage(bmp))
                using (MemoryStream ms = new MemoryStream())
                {
                    g.Clear(Color.White);
                    using (Font font = new Font("Arial", 24))
                    using (Brush brush = new SolidBrush(Color.Black))
                    {
                        g.DrawString("Hello OCR Test", font, brush, 20, 30);
                    }
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    imageBytes = ms.ToArray();
                }
                Log("   图片大小: " + imageBytes.Length + " 字节\n");
                
                // 2. 获取Token
                Log("2. 获取Token...");
                string token = GetToken();
                Log("   Token: " + token.Substring(0, 20) + "...\n");
                
                // 3. 使用WebClient上传
                Log("3. 使用WebClient上传...");
                string result = UploadWithWebClient(token, imageBytes);
                Log("   结果: " + result);
                
            }
            catch (Exception ex)
            {
                Log("错误: " + ex.Message);
            }
        }
        
        private string GetToken()
        {
            string API_KEY = "dSoLFs3M82yIv0hFdI6dr4kY";
            string SECRET_KEY = "DGp4llrUK0xOBEkm9UJxNWmIzhSWoQYx";
            
            string url = "https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id=" + API_KEY + "&client_secret=" + SECRET_KEY;
            
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                string json = client.UploadString(url, "POST", "");
                
                int start = json.IndexOf("\"access_token\":\"") + 16;
                int end = json.IndexOf("\"", start);
                return json.Substring(start, end - start);
            }
        }
        
        private string UploadWithWebClient(string token, byte[] imageBytes)
        {
            string base64 = Convert.ToBase64String(imageBytes);
            string url = "https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token=" + token;
            
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                
                NameValueCollection data = new NameValueCollection();
                data["image"] = base64;
                data["language_type"] = "CHN_ENG";
                
                try
                {
                    byte[] response = client.UploadValues(url, "POST", data);
                    string result = Encoding.UTF8.GetString(response);
                    
                    if (result.Contains("words_result"))
                        return "成功!";
                    return "失败: " + GetError(result);
                }
                catch (WebException ex)
                {
                    using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        return "失败: " + reader.ReadToEnd();
                    }
                }
            }
        }
        
        private string GetError(string json)
        {
            try
            {
                int codeStart = json.IndexOf("\"error_code\":") + 12;
                int codeEnd = json.IndexOf(",", codeStart);
                if (codeEnd < 0) codeEnd = json.IndexOf("}", codeStart);
                string code = json.Substring(codeStart, codeEnd - codeStart).Trim();
                
                int msgStart = json.IndexOf("\"error_msg\":") + 12;
                int msgEnd = json.IndexOf("\"", msgStart);
                if (msgEnd < 0) msgEnd = json.Length;
                string msg = json.Substring(msgStart, msgEnd - msgStart).Trim();
                
                return code + " - " + msg;
            }
            catch { return json.Substring(0, Math.Min(100, json.Length)); }
        }
        
        private void Log(string msg)
        {
            txtResult.AppendText(msg + "\r\n");
            txtResult.Refresh();
        }
    }
    
    public class TestProgram
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new TestForm());
        }
    }
}
