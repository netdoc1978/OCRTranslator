using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OCRTranslator
{
    public class ScreenCaptureHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, 
            IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SRCCOPY = 0x00CC0020;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public static Bitmap CaptureScreen()
        {
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            IntPtr hDesk = GetDesktopWindow();
            IntPtr hSrce = GetWindowDC(hDesk);
            IntPtr hDest = CreateCompatibleDC(hSrce);
            IntPtr hBmp = CreateCompatibleBitmap(hSrce, screenWidth, screenHeight);
            IntPtr hOldBmp = SelectObject(hDest, hBmp);

            BitBlt(hDest, 0, 0, screenWidth, screenHeight, hSrce, 0, 0, SRCCOPY);

            SelectObject(hDest, hOldBmp);

            Bitmap bmp = Bitmap.FromHbitmap(hBmp);

            DeleteObject(hBmp);
            DeleteDC(hDest);
            ReleaseDC(hDesk, hSrce);

            return bmp;
        }

        public static Bitmap CaptureRegion(Rectangle region)
        {
            Bitmap bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(region.Location, Point.Empty, region.Size);
            }
            return bmp;
        }

        public static void DragWindow(Form form)
        {
            ReleaseCapture();
            SendMessage(form.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
    }

    public class ScreenCaptureForm : Form
    {
        private Point startPoint;
        private Point endPoint;
        private bool isSelecting;
        private Rectangle selection;
        private Bitmap screenBitmap;
        private Pen selectionPen;
        private Brush selectionBrush;

        public Bitmap CapturedRegion { get; private set; }

        public ScreenCaptureForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Cross;

            selectionPen = new Pen(Color.FromArgb(0, 120, 215), 2);
            selectionBrush = new SolidBrush(Color.FromArgb(50, 0, 120, 215));

            this.MouseDown += ScreenCaptureForm_MouseDown;
            this.MouseMove += ScreenCaptureForm_MouseMove;
            this.MouseUp += ScreenCaptureForm_MouseUp;
            this.Paint += ScreenCaptureForm_Paint;
            this.KeyDown += ScreenCaptureForm_KeyDown;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            screenBitmap = ScreenCaptureHelper.CaptureScreen();
        }

        private void ScreenCaptureForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void ScreenCaptureForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                startPoint = e.Location;
                isSelecting = true;
            }
        }

        private void ScreenCaptureForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                endPoint = e.Location;
                selection = new Rectangle(
                    Math.Min(startPoint.X, endPoint.X),
                    Math.Min(startPoint.Y, endPoint.Y),
                    Math.Abs(endPoint.X - startPoint.X),
                    Math.Abs(endPoint.Y - startPoint.Y));
                this.Invalidate();
            }
        }

        private void ScreenCaptureForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isSelecting)
            {
                isSelecting = false;
                endPoint = e.Location;
                selection = new Rectangle(
                    Math.Min(startPoint.X, endPoint.X),
                    Math.Min(startPoint.Y, endPoint.Y),
                    Math.Abs(endPoint.X - startPoint.X),
                    Math.Abs(endPoint.Y - startPoint.Y));

                if (selection.Width > 5 && selection.Height > 5)
                {
                    CapturedRegion = ScreenCaptureHelper.CaptureRegion(selection);
                    this.DialogResult = DialogResult.OK;
                }
                else
                {
                    this.DialogResult = DialogResult.Cancel;
                }
                this.Close();
            }
        }

        private void ScreenCaptureForm_Paint(object sender, PaintEventArgs e)
        {
            if (screenBitmap != null)
            {
                e.Graphics.DrawImage(screenBitmap, 0, 0);
            }

            if (isSelecting || selection.Width > 0)
            {
                e.Graphics.FillRectangle(selectionBrush, selection);
                e.Graphics.DrawRectangle(selectionPen, selection);

                string sizeText = string.Format("{0} x {1}", selection.Width, selection.Height);
                using (Font font = new Font("Microsoft YaHei UI", 10))
                using (Brush textBrush = new SolidBrush(Color.White))
                {
                    SizeF textSize = e.Graphics.MeasureString(sizeText, font);
                    Point textPos = new Point(
                        selection.X + selection.Width / 2 - (int)(textSize.Width / 2),
                        selection.Y - (int)textSize.Height - 5);
                    
                    if (textPos.Y < 0)
                        textPos.Y = selection.Y + selection.Height + 5;

                    e.Graphics.DrawString(sizeText, font, textBrush, textPos);
                }
            }

            using (Font tipFont = new Font("Microsoft YaHei UI", 12))
            using (Brush tipBrush = new SolidBrush(Color.White))
            {
                string tip = "按住鼠标左键拖动选择区域，按ESC键取消";
                e.Graphics.DrawString(tip, tipFont, tipBrush, 20, 20);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (screenBitmap != null)
                    screenBitmap.Dispose();
                if (selectionPen != null)
                    selectionPen.Dispose();
                if (selectionBrush != null)
                    selectionBrush.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}