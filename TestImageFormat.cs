using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace OCRTranslator
{
    public class TestForm : Form
    {
        private Button btnTest1;
        private Button btnTest2;
        private Button btnTest3;
        private TextBox txtResult;
        private PictureBox picPreview;
        
        public TestForm()
        {
            this.Text = "图片格式测试";
            this.Size = new Size(800, 600);
            
            Label lbl = new Label();
            lbl.Text = "测试不同的图片格式转换方法：";
            lbl.Location = new Point(10, 10);
            lbl.AutoSize = true;
            
            btnTest1 = new Button();
            btnTest1.Text = "方法1: MemoryStream+JPEG";
            btnTest1.Location = new Point(10, 40);
            btnTest1.Size = new Size(200, 30);
            btnTest1.Click += (s, e) => TestMethod1();
            
            btnTest2 = new Button();
            btnTest2.Text = "方法2: 直接Save到文件";
            btnTest2.Location = new Point(220, 40);
            btnTest2.Size = new Size(200, 30);
            btnTest2.Click += (s, e) => TestMethod2();
            
            btnTest3 = new Button();
            btnTest3.Text = "方法3: PNG格式";
            btnTest3.Location = new Point(430, 40);
            btnTest3.Size = new Size(200, 30);
            btnTest3.Click += (s, e) => TestMethod3();
            
            Label lblPreview = new Label();
            lblPreview.Text = "图片预览:";
            lblPreview.Location = new Point(10, 80);
            lblPreview.AutoSize = true;
            
            picPreview = new PictureBox();
            picPreview.Location = new Point(10, 110);
            picPreview.Size = new Size(300, 200);
            picPreview.BorderStyle = BorderStyle.FixedSingle;
            picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            
            Label lblResult = new Label();
            lblResult.Text = "测试结果:";
            lblResult.Location = new Point(320, 80);
            lblResult.AutoSize = true;
            
            txtResult = new TextBox();
            txtResult.Location = new Point(320, 110);
            txtResult.Size = new Size(450, 400);
            txtResult.Multiline = true;
            txtResult.ScrollBars = ScrollBars.Vertical;
            txtResult.Font = new Font("Consolas", 9);
            
            this.Controls.AddRange(new Control[] { lbl, btnTest1, btnTest2, btnTest3, lblPreview, picPreview, lblResult, txtResult });
        }
        
        private void TestMethod1()
        {
            txtResult.Text = "";
            try
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "图片|*.jpg;*.png;*.bmp;*.gif";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    Bitmap bmp = new Bitmap(d.FileName);
                    picPreview.Image = bmp;
                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Jpeg);
                        byte[] data = ms.ToArray();
                        string base64 = Convert.ToBase64String(data);
                        
                        txtResult.Text = "原始大小: " + bmp.Width + "x" + bmp.Height + "\r\n";
                        txtResult.Text += "JPEG字节数: " + data.Length + "\r\n";
                        txtResult.Text += "Base64长度: " + base64.Length + "\r\n";
                        txtResult.Text += "Base64前100字符: " + base64.Substring(0, Math.Min(100, base64.Length)) + "...";
                    }
                }
            }
            catch (Exception ex)
            {
                txtResult.Text = "错误: " + ex.Message;
            }
        }
        
        private void TestMethod2()
        {
            txtResult.Text = "";
            try
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "图片|*.jpg;*.png;*.bmp;*.gif";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    Bitmap bmp = new Bitmap(d.FileName);
                    picPreview.Image = bmp;
                    
                    string tempFile = Path.Combine(Path.GetTempPath(), "test.jpg");
                    bmp.Save(tempFile, ImageFormat.Jpeg);
                    byte[] data = File.ReadAllBytes(tempFile);
                    string base64 = Convert.ToBase64String(data);
                    
                    txtResult.Text = "方法2结果:\r\n";
                    txtResult.Text += "JPEG字节数: " + data.Length + "\r\n";
                    txtResult.Text += "Base64长度: " + base64.Length + "\r\n";
                    txtResult.Text += "前100字符: " + base64.Substring(0, Math.Min(100, base64.Length)) + "...";
                    
                    File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                txtResult.Text = "错误: " + ex.Message;
            }
        }
        
        private void TestMethod3()
        {
            txtResult.Text = "";
            try
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "图片|*.jpg;*.png;*.bmp;*.gif";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    Bitmap bmp = new Bitmap(d.FileName);
                    picPreview.Image = bmp;
                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        byte[] data = ms.ToArray();
                        string base64 = Convert.ToBase64String(data);
                        
                        txtResult.Text = "PNG格式结果:\r\n";
                        txtResult.Text += "PNG字节数: " + data.Length + "\r\n";
                        txtResult.Text += "Base64长度: " + base64.Length + "\r\n";
                        txtResult.Text += "前100字符: " + base64.Substring(0, Math.Min(100, base64.Length)) + "...";
                    }
                }
            }
            catch (Exception ex)
            {
                txtResult.Text = "错误: " + ex.Message;
            }
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
