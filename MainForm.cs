using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace OCRTranslator
{
    public class MainForm : Form
    {
        private PictureBox picPreview;
        private TextBox txtSource;
        private TextBox txtTranslated;
        private ComboBox cmbOCRType;
        private ComboBox cmbOCRModel;
        private ComboBox cmbTranslateLang;
        private ComboBox cmbTranslateType;
        private CheckBox chkAutoStart;
        private CheckBox chkTopMost;
        private Label lblStatus;
        private Label lblFooter;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayMenu;
        private Bitmap currentImage;
        private string currentImagePath = null;
        
        private const int HOTKEY_ID = 1;
        private const uint MOD_NONE = 0x0000;
        private const uint VK_F4 = 0x73;
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainForm()
        {
            InitializeComponent();
            SetupTray();
            LoadSettings();
        }

        private void SetupTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示主窗口", null, (s, e) => ShowForm());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("截图识别", null, (s, e) => DoCapture());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("退出", null, (s, e) => ExitApp());

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Text = "OCR识别翻译工具";
            notifyIcon.Visible = true;
            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.DoubleClick += (s, e) => ShowForm();
        }

        private void InitializeComponent()
        {
            this.Text = "OCR识别翻译工具 v1.0";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 243, 249);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(800, 600);
            this.Closing += (s, e) => { e.Cancel = true; this.Hide(); notifyIcon.ShowBalloonTip(1000, "提示", "程序已最小化到托盘", ToolTipIcon.Info); };

            Panel leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Left;
            leftPanel.Width = 300;
            leftPanel.BackColor = Color.White;

            Label lblPreview = new Label();
            lblPreview.Text = "图片预览";
            lblPreview.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            lblPreview.Location = new Point(10, 10);
            lblPreview.AutoSize = true;

            picPreview = new PictureBox();
            picPreview.Location = new Point(10, 40);
            picPreview.Size = new Size(280, 350);
            picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            picPreview.BackColor = Color.FromArgb(250, 250, 250);
            picPreview.BorderStyle = BorderStyle.FixedSingle;

            Panel btnPanel = new Panel();
            btnPanel.Location = new Point(10, 400);
            btnPanel.Size = new Size(280, 110);
            btnPanel.BackColor = Color.White;

            Button btnOpenImg = CreateButton("打开图片", 5, 5, 130, 30);
            btnOpenImg.Click += (s, e) => OpenImage();

            Button btnPaste = CreateButton("粘贴图片", 5, 40, 130, 30);
            btnPaste.Click += (s, e) => PasteImage();

            Button btnCapture = CreateButton("截图识别(F4)", 5, 75, 130, 30);
            btnCapture.BackColor = Color.FromArgb(0, 120, 215);
            btnCapture.Click += (s, e) => DoCapture();

            btnPanel.Controls.AddRange(new Control[] { btnOpenImg, btnPaste, btnCapture });

            Panel setPanel = new Panel();
            setPanel.Location = new Point(10, 520);
            setPanel.Size = new Size(280, 130);
            setPanel.BackColor = Color.FromArgb(245, 247, 252);
            setPanel.Padding = new Padding(10);

            Label lblSet = new Label();
            lblSet.Text = "设置";
            lblSet.Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold);
            lblSet.Location = new Point(10, 5);
            lblSet.AutoSize = true;

            chkAutoStart = new CheckBox();
            chkAutoStart.Text = "开机自启动";
            chkAutoStart.Location = new Point(10, 30);
            chkAutoStart.AutoSize = true;
            chkAutoStart.Click += (s, e) => { Program.SetAutoStart(chkAutoStart.Checked); };

            chkTopMost = new CheckBox();
            chkTopMost.Text = "窗口置顶";
            chkTopMost.Location = new Point(10, 55);
            chkTopMost.AutoSize = true;
            chkTopMost.Click += (s, e) => { this.TopMost = chkTopMost.Checked; };

            setPanel.Controls.AddRange(new Control[] { lblSet, chkAutoStart, chkTopMost });
            leftPanel.Controls.AddRange(new Control[] { setPanel, btnPanel, picPreview, lblPreview });

            Panel rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;

            Panel ocrPanel = new Panel();
            ocrPanel.Dock = DockStyle.Top;
            ocrPanel.Height = 280;
            ocrPanel.BackColor = Color.White;
            ocrPanel.Padding = new Padding(5);
            ocrPanel.BorderStyle = BorderStyle.FixedSingle;

            Label lblOcr = new Label();
            lblOcr.Text = "识别文本";
            lblOcr.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            lblOcr.Dock = DockStyle.Top;
            lblOcr.Height = 30;

            txtSource = new TextBox();
            txtSource.Dock = DockStyle.Fill;
            txtSource.Multiline = true;
            txtSource.ScrollBars = ScrollBars.Both;
            txtSource.Font = new Font("Microsoft YaHei UI", 11);
            txtSource.Padding = new Padding(5);

            Panel ocrBtnPanel = new Panel();
            ocrBtnPanel.Dock = DockStyle.Bottom;
            ocrBtnPanel.Height = 60;
            ocrBtnPanel.BackColor = Color.FromArgb(245, 247, 252);
            ocrBtnPanel.Padding = new Padding(5);

            Label lblType = new Label();
            lblType.Text = "识别语言:";
            lblType.Location = new Point(5, 8);
            lblType.AutoSize = true;

            cmbOCRType = new ComboBox();
            cmbOCRType.Location = new Point(75, 5);
            cmbOCRType.Width = 100;
            cmbOCRType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbOCRType.Items.AddRange(new object[] { "中英文混合", "中文", "英文", "日文", "韩文" });
            cmbOCRType.SelectedIndex = 0;

            Label lblModel = new Label();
            lblModel.Text = "识别模型:";
            lblModel.Location = new Point(180, 8);
            lblModel.AutoSize = true;

            cmbOCRModel = new ComboBox();
            cmbOCRModel.Location = new Point(255, 5);
            cmbOCRModel.Width = 110;
            cmbOCRModel.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbOCRModel.Items.AddRange(new object[] { 
                new KeyValuePair<string, string>("general", "通用识别"),
                new KeyValuePair<string, string>("accurate", "高精度识别")
            });
            cmbOCRModel.DisplayMember = "Value";
            cmbOCRModel.ValueMember = "Key";
            cmbOCRModel.SelectedIndex = 1;

            Button btnStartOCR = CreateButton("开始识别", 380, 5, 100, 32);
            btnStartOCR.BackColor = Color.FromArgb(0, 120, 215);
            btnStartOCR.Click += (s, e) => StartOCR();

            Button btnCopySrc = CreateButton("复制原文", 490, 5, 100, 32);
            btnCopySrc.Click += (s, e) => { if (!string.IsNullOrEmpty(txtSource.Text)) { Clipboard.SetText(txtSource.Text); SetStatus("已复制原文"); } };

            ocrBtnPanel.Controls.AddRange(new Control[] { lblType, cmbOCRType, lblModel, cmbOCRModel, btnStartOCR, btnCopySrc });
            ocrPanel.Controls.AddRange(new Control[] { txtSource, ocrBtnPanel, lblOcr });

            Panel transPanel = new Panel();
            transPanel.Dock = DockStyle.Fill;
            transPanel.BackColor = Color.White;
            transPanel.Padding = new Padding(5);
            transPanel.BorderStyle = BorderStyle.FixedSingle;

            Label lblTrans = new Label();
            lblTrans.Text = "翻译结果";
            lblTrans.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            lblTrans.Dock = DockStyle.Top;
            lblTrans.Height = 30;

            txtTranslated = new TextBox();
            txtTranslated.Dock = DockStyle.Fill;
            txtTranslated.Multiline = true;
            txtTranslated.ScrollBars = ScrollBars.Both;
            txtTranslated.Font = new Font("Microsoft YaHei UI", 11);
            txtTranslated.Padding = new Padding(5);

            Panel transBtnPanel = new Panel();
            transBtnPanel.Dock = DockStyle.Bottom;
            transBtnPanel.Height = 40;
            transBtnPanel.BackColor = Color.FromArgb(245, 247, 252);
            transBtnPanel.Padding = new Padding(5);

            Label lblLang = new Label();
            lblLang.Text = "翻译方向:";
            lblLang.Location = new Point(5, 8);
            lblLang.AutoSize = true;

            cmbTranslateLang = new ComboBox();
            cmbTranslateLang.Location = new Point(80, 5);
            cmbTranslateLang.Width = 90;
            cmbTranslateLang.DropDownStyle = ComboBoxStyle.DropDownList;

            Label lblTransType = new Label();
            lblTransType.Text = "翻译类型:";
            lblTransType.Location = new Point(180, 8);
            lblTransType.AutoSize = true;

            cmbTranslateType = new ComboBox();
            cmbTranslateType.Location = new Point(255, 5);
            cmbTranslateType.Width = 110;
            cmbTranslateType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTranslateType.Items.AddRange(new object[] {
                new KeyValuePair<string, string>("general", "通用文本翻译"),
                new KeyValuePair<string, string>("mt", "大模型文本翻译")
            });
            cmbTranslateType.DisplayMember = "Value";
            cmbTranslateType.ValueMember = "Key";
            cmbTranslateType.SelectedIndex = 0;

            Button btnTranslate = CreateButton("翻译", 380, 3, 90, 32);
            btnTranslate.BackColor = Color.FromArgb(0, 150, 136);
            btnTranslate.Click += (s, e) => StartTranslate();

            Button btnCopyTrans = CreateButton("复制译文", 480, 3, 100, 32);
            btnCopyTrans.Click += (s, e) => { if (!string.IsNullOrEmpty(txtTranslated.Text)) { Clipboard.SetText(txtTranslated.Text); SetStatus("已复制译文"); } };

            Button btnClear = CreateButton("清空", 590, 3, 70, 32);
            btnClear.Click += (s, e) => ClearAll();

            transBtnPanel.Controls.AddRange(new Control[] { lblLang, cmbTranslateLang, lblTransType, cmbTranslateType, btnTranslate, btnCopyTrans, btnClear });
            transPanel.Controls.AddRange(new Control[] { txtTranslated, transBtnPanel, lblTrans });

            rightPanel.Controls.AddRange(new Control[] { transPanel, ocrPanel });

            lblStatus = new Label();
            lblStatus.Text = "就绪";
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Height = 25;
            lblStatus.BackColor = Color.FromArgb(230, 233, 239);
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Padding = new Padding(10, 0, 0, 0);

            lblFooter = new Label();
            lblFooter.Text = "使用本程序过程中有问题或建议可致信netdoc@foxmail.com";
            lblFooter.Dock = DockStyle.Bottom;
            lblFooter.Height = 22;
            lblFooter.BackColor = Color.FromArgb(45, 55, 72);
            lblFooter.ForeColor = Color.White;
            lblFooter.TextAlign = ContentAlignment.MiddleCenter;
            lblFooter.Font = new Font("Microsoft YaHei UI", 8);

            this.Controls.AddRange(new Control[] { rightPanel, leftPanel, lblStatus, lblFooter });

            LoadLanguages();
        }

        private Button CreateButton(string text, int x, int y, int w, int h)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Location = new Point(x, y);
            btn.Size = new Size(w, h);
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Color.FromArgb(45, 55, 72);
            btn.ForeColor = Color.White;
            btn.Font = new Font("Microsoft YaHei UI", 9);
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void LoadLanguages()
        {
            cmbTranslateLang.Items.Clear();
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("en2zh", "英中"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("zh2en", "中英"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("jp2zh", "日中"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("zh2jp", "中日"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("kor2zh", "韩中"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("zh2kor", "中韩"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("fra2zh", "法中"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("zh2fra", "中法"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("spa2zh", "西中"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("zh2spa", "中西"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("ru2zh", "俄中"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("zh2ru", "中俄"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("de2zh", "德中"));
            cmbTranslateLang.Items.Add(new KeyValuePair<string, string>("zh2de", "中德"));
            cmbTranslateLang.DisplayMember = "Value";
            cmbTranslateLang.ValueMember = "Key";
            cmbTranslateLang.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try { chkAutoStart.Checked = Program.GetAutoStartStatus(); }
            catch { }
        }

        private void SetStatus(string msg)
        {
            if (lblStatus != null && !lblStatus.IsDisposed)
                lblStatus.Text = msg + "  |  " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ShowForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void ExitApp()
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            if (notifyIcon != null) { notifyIcon.Visible = false; notifyIcon.Dispose(); }
            Application.Exit();
        }

        private void OpenImage()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*";
            d.Title = "选择图片";
            if (d.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (currentImage != null) { currentImage.Dispose(); currentImage = null; }
                    currentImagePath = d.FileName;
                    currentImage = new Bitmap(d.FileName);
                    if (picPreview.Image != null) { var old = picPreview.Image; picPreview.Image = null; old.Dispose(); }
                    picPreview.Image = currentImage;
                    SetStatus("已加载: " + Path.GetFileName(d.FileName));
                    StartOCR();
                }
                catch (Exception ex) { MessageBox.Show("加载图片失败: " + ex.Message, "错误"); }
            }
        }

        private void PasteImage()
        {
            try
            {
                Image img = null;
                if (Clipboard.ContainsImage()) img = Clipboard.GetImage();
                if (img == null && Clipboard.ContainsFileDropList())
                {
                    foreach (string file in Clipboard.GetFileDropList())
                    {
                        if (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                        { img = Image.FromFile(file); break; }
                    }
                }
                if (img == null && Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText().Trim();
                    if (File.Exists(text)) img = Image.FromFile(text);
                }
                if (img != null)
                {
                    if (currentImage != null) { currentImage.Dispose(); currentImage = null; }
                    currentImage = new Bitmap(img);
                    currentImagePath = null;
                    if (picPreview.Image != null) { var old = picPreview.Image; picPreview.Image = null; old.Dispose(); }
                    picPreview.Image = currentImage;
                    img.Dispose();
                    SetStatus("已粘贴图片");
                    StartOCR();
                }
                else { MessageBox.Show("剪贴板中没有图片", "提示"); }
            }
            catch (Exception ex) { MessageBox.Show("粘贴失败: " + ex.Message, "错误"); }
        }

        private void DoCapture()
        {
            this.Hide();
            Thread.Sleep(300);
            try
            {
                using (ScreenCaptureForm frm = new ScreenCaptureForm())
                {
                    if (frm.ShowDialog() == DialogResult.OK && frm.CapturedRegion != null)
                    {
                        if (currentImage != null) { currentImage.Dispose(); currentImage = null; }
                        currentImage = frm.CapturedRegion;
                        currentImagePath = null;
                        if (picPreview.Image != null) { var old = picPreview.Image; picPreview.Image = null; old.Dispose(); }
                        picPreview.Image = currentImage;
                        SetStatus("截图成功，开始识别...");
                        StartOCR();
                    }
                    else { SetStatus("截图已取消"); }
                }
            }
            catch (Exception ex) { MessageBox.Show("截图失败: " + ex.Message, "错误"); }
            finally { this.Show(); this.Activate(); }
        }

        private void StartOCR()
        {
            if (currentImage == null) { MessageBox.Show("请先打开图片", "提示"); return; }
            SetStatus("正在识别...");
            new Thread(() =>
            {
                try
                {
                    Bitmap bmp = null;
                    try
                    {
                        bmp = (currentImagePath != null && File.Exists(currentImagePath)) ? new Bitmap(currentImagePath) : currentImage.Clone(new Rectangle(0, 0, currentImage.Width, currentImage.Height), currentImage.PixelFormat);
                        string lang = "CHN_ENG";
                        int idx = 0;
                        this.Invoke((MethodInvoker)delegate { idx = cmbOCRType.SelectedIndex; });
                        if (idx == 2) lang = "ENG"; else if (idx == 3) lang = "JPN"; else if (idx == 4) lang = "KOR";
                        string model = "accurate";
                        this.Invoke((MethodInvoker)delegate { var item = (KeyValuePair<string, string>)cmbOCRModel.SelectedItem; model = item.Key; });
                        string result = BaiDuOCRHelper.RecognizeText(bmp, lang, model);
                        this.Invoke((MethodInvoker)delegate
                        {
                            txtSource.Text = result;
                            if (!string.IsNullOrEmpty(result)) { Clipboard.SetText(result); SetStatus("识别完成，已复制到剪切板"); }
                            else SetStatus("识别完成");
                        });
                    }
                    finally { if (bmp != null) bmp.Dispose(); }
                }
                catch (Exception ex) { this.Invoke((MethodInvoker)delegate { MessageBox.Show(ex.Message, "识别失败"); SetStatus("识别失败"); }); }
            }).Start();
        }

        private void StartTranslate()
        {
            if (string.IsNullOrEmpty(txtSource.Text)) { MessageBox.Show("请先进行OCR识别", "提示"); return; }
            SetStatus("正在翻译...");
            new Thread(() =>
            {
                try
                {
                    string pair = ""; string text = ""; string transType = "general";
                    this.Invoke((MethodInvoker)delegate {
                        var item = (KeyValuePair<string, string>)cmbTranslateLang.SelectedItem;
                        pair = item.Key;
                        text = txtSource.Text;
                        var typeItem = (KeyValuePair<string, string>)cmbTranslateType.SelectedItem;
                        transType = typeItem.Key;
                    });
                    string[] langs = BaiduTranslateHelper.ParseLanguagePair(pair);
                    string result = BaiduTranslateHelper.Translate(text, langs[0], langs[1], transType);
                    this.Invoke((MethodInvoker)delegate { txtTranslated.Text = result; SetStatus("翻译完成"); });
                }
                catch (Exception ex) { this.Invoke((MethodInvoker)delegate { MessageBox.Show(ex.Message, "翻译失败"); SetStatus("翻译失败"); }); }
            }).Start();
        }

        private void ClearAll()
        {
            txtSource.Text = ""; txtTranslated.Text = "";
            if (currentImage != null) { currentImage.Dispose(); currentImage = null; }
            currentImagePath = null;
            if (picPreview.Image != null) { var old = picPreview.Image; picPreview.Image = null; old.Dispose(); }
            SetStatus("已清空");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!RegisterHotKey(this.Handle, HOTKEY_ID, MOD_NONE, VK_F4)) SetStatus("F4热键注册失败");
            else SetStatus("F4快捷键已就绪");
            this.Hide();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) DoCapture();
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterHotKey(this.Handle, HOTKEY_ID);
                if (currentImage != null) currentImage.Dispose();
                if (picPreview.Image != null) picPreview.Image.Dispose();
                if (notifyIcon != null) { notifyIcon.Visible = false; notifyIcon.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }
}
