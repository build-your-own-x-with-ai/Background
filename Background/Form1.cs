using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Background
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            try
            {
                Console.WriteLine("=== �������ť��ʱ�����汳�� ===");
                Console.WriteLine("������1. �������½ǰ�ť�ɵ���������� 2. �� Ctrl+C �˳�");

                // 1. ���ɲ����ó�ʼ���汳������ʱ�ӣ�
                WallpaperManager.UpdateWallpaper();

                // 2. ����͸���������ڣ����ؿɵ����ť��
                Application.Run(new TransparentButtonForm());

                // 3. �����˳�ʱ������ʱ�ļ�
                WallpaperManager.CleanupTempFile();
                Console.WriteLine("�������˳�����ʱ�ļ�������");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"����{ex.Message}");
                WallpaperManager.CleanupTempFile();
                Console.ReadKey();
            }
        }

        // ��ֽ�����ࣨ����֮ǰ��ʱ�ӱ��������߼���
        public static class WallpaperManager
        {
            // ϵͳAPI���������ñ�ֽ��ɾ����ʱ�ļ���
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            private static extern int SystemParametersInfo(int uAction, int uParam, string lpNewDeskWallpaper, int fuWinIni);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, MoveFileFlags dwFlags);

            [Flags]
            private enum MoveFileFlags : uint
            {
                MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004
            }

            // ��������
            private const int SPI_SETDESKWALLPAPER = 20;
            private const int SPIF_UPDATEINIFILE = 1;
            private const int SPIF_SENDCHANGE = 2;
            private static string _lastTempFilePath = null;

            /// <summary>
            /// ���ɴ�ʱ�ӵĽ��䱳��������Ϊ�����ֽ
            /// </summary>
            public static void UpdateWallpaper()
            {
                var screenSize = Screen.PrimaryScreen.Bounds;
                int width = screenSize.Width;
                int height = screenSize.Height;
                string newTempPath = Path.Combine(Path.GetTempPath(), $"ClockWallpaper_{Guid.NewGuid()}.png");

                try
                {
                    // 1. ���Ʊ���������+ʱ�ӣ�
                    using (Bitmap bmp = new Bitmap(width, height))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        DrawGradientBackground(g, width, height);
                        DrawClock(g, width, height, DateTime.Now);
                        bmp.Save(newTempPath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    // 2. ���ñ�ֽ
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, newTempPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                    // 3. ������ļ�
                    CleanupTempFile();
                    _lastTempFilePath = newTempPath;
                }
                catch
                {
                    if (File.Exists(newTempPath)) TryDeleteFile(newTempPath);
                    throw;
                }
            }

            /// <summary>
            /// ���ƽ��䱳��
            /// </summary>
            private static void DrawGradientBackground(Graphics g, int width, int height)
            {
                Random rand = new Random();
                Color color1 = Color.FromArgb(255, rand.Next(256), rand.Next(256), rand.Next(256));
                Color color2 = Color.FromArgb(255, rand.Next(256), rand.Next(256), rand.Next(256));
                using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), color1, color2, (LinearGradientMode)rand.Next(4)))
                {
                    g.FillRectangle(brush, 0, 0, width, height);
                }
            }

            /// <summary>
            /// ����ʵʱʱ�ӣ�����֮ǰ���߼���
            /// </summary>
            private static void DrawClock(Graphics g, int width, int height, DateTime time)
            {
                int radius = Math.Min(width, height) / 4;
                PointF center = new PointF(width / 2, height / 2);

                // ��ͼ��������
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // ���Ʊ���
                using (Pen dialPen = new Pen(Color.Black, 3))
                {
                    g.DrawEllipse(dialPen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
                }

                // ���ƿ̶�
                for (int i = 0; i < 60; i++)
                {
                    float angle = i * 6f;
                    double rad = angle * Math.PI / 180;
                    PointF start = new PointF(center.X + (float)(radius * Math.Cos(rad)), center.Y + (float)(radius * Math.Sin(rad)));
                    PointF end = new PointF(center.X + (float)((i % 5 == 0 ? radius * 0.85f : radius * 0.95f) * Math.Cos(rad)), center.Y + (float)((i % 5 == 0 ? radius * 0.85f : radius * 0.95f) * Math.Sin(rad)));
                    using (Pen pen = new Pen(Color.Black, i % 5 == 0 ? 3 : 1))
                    {
                        g.DrawLine(pen, start, end);
                    }
                }

                // ����ָ��
                float secAngle = time.Second * 6f - 90f;
                float minAngle = (time.Minute * 6f + time.Second * 0.1f) - 90f;
                float hourAngle = (time.Hour % 12 * 30f + time.Minute * 0.5f) - 90f;

                DrawHand(g, center, radius * 0.85f, secAngle, Color.Red, 2);
                DrawHand(g, center, radius * 0.7f, minAngle, Color.Black, 4);
                DrawHand(g, center, radius * 0.5f, hourAngle, Color.Black, 6);

                // �������ĵ�
                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    g.FillEllipse(brush, center.X - 5, center.Y - 5, 10, 10);
                }
            }

            /// <summary>
            /// ����ʱ��ָ��
            /// </summary>
            private static void DrawHand(Graphics g, PointF center, float length, float angle, Color color, int thickness)
            {
                double rad = angle * Math.PI / 180;
                PointF end = new PointF(center.X + (float)(length * Math.Cos(rad)), center.Y + (float)(length * Math.Sin(rad)));
                using (Pen pen = new Pen(color, thickness))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, center, end);
                }
            }

            /// <summary>
            /// ������ʱ�ļ�
            /// </summary>
            public static void CleanupTempFile()
            {
                if (!string.IsNullOrEmpty(_lastTempFilePath) && File.Exists(_lastTempFilePath))
                {
                    TryDeleteFile(_lastTempFilePath);
                    _lastTempFilePath = null;
                }
            }

            /// <summary>
            /// ����ɾ���ļ�������ռ�������
            /// </summary>
            private static void TryDeleteFile(string path)
            {
                try { File.Delete(path); }
                catch { MoveFileEx(path, null, MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT); }
            }
        }

        // ͸���������ڣ����ؿɵ����ť��
        public class TransparentButtonForm : Form
        {
            // ��ť����
            private readonly Rectangle _buttonRect; // ��ťλ�úʹ�С
            private bool _isMouseOverButton; // ����Ƿ������ڰ�ť��
            private readonly System.Windows.Forms.Timer _wallpaperUpdateTimer; // ����ʱ�Ӹ��¶�ʱ��

            public TransparentButtonForm()
            {
                // 1. ���ڻ������ã��ؼ���ȫ͸�����ޱ߽硢������ʾ��
                InitializeWindowSettings();

                // 2. ��ʼ����ť��λ�ã��������½ǣ���С��120x40��
                int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = Screen.PrimaryScreen.Bounds.Height;
                _buttonRect = new Rectangle(
                    x: screenWidth - 140, // �Ҽ��20px
                    y: screenHeight - 80, // �¼��40px
                    width: 120,
                    height: 40
                );

                // 3. ��ʼ��ʱ�ӱ������¶�ʱ����ÿ�����һ�Σ�
                _wallpaperUpdateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                _wallpaperUpdateTimer.Tick += (s, e) => WallpaperManager.UpdateWallpaper();
                _wallpaperUpdateTimer.Start();
            }

            /// <summary>
            /// ���ڻ������ã�ʵ��͸������Ч����
            /// </summary>
            private void InitializeWindowSettings()
            {
                // ���ڴ�С = ��Ļ��С�������������棩
                Bounds = Screen.PrimaryScreen.Bounds;
                // �ޱ��������ޱ߿�
                FormBorderStyle = FormBorderStyle.None;
                // ȫ͸��������ť����ɼ���
                BackColor = Color.Magenta; // ѡ��һ�������õ���ɫ��Ϊ��͸������
                TransparencyKey = Color.Magenta; // ����ɫ����ȫ͸��
                                                 // �������涥�㣨�����ڵ�����Ӧ�ô��ڣ�ͨ��ShowInTaskbar=falseʵ�֣�
                TopMost = true;
                ShowInTaskbar = false; // ������������ʾ���������
                                       // ������괩͸������ť�����⣬����ᴩ͸������/����Ӧ�ã�
                SetWindowLongPtr(Handle, GWL_EXSTYLE, GetWindowLongPtr(Handle, GWL_EXSTYLE) | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }

            /// <summary>
            /// ���ư�ť���ڴ���͸�������ϻ����Զ��尴ť��
            /// </summary>
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias; // ����ݣ���ť��Ե��ƽ��

                // ��ť��ʽ���ã������������״̬�л���ɫ��
                Color btnColor = _isMouseOverButton ? Color.FromArgb(200, 70, 130, 180) : Color.FromArgb(150, 70, 130, 180);
                Color textColor = Color.White;
                Font btnFont = new Font("΢���ź�", 12, FontStyle.Bold);

                // 1. ���ư�ťԲ�Ǿ��Σ�Բ�ǰ뾶8px��
                using (SolidBrush btnBrush = new SolidBrush(btnColor))
                using (GraphicsPath path = CreateRoundedRectanglePath(_buttonRect, 8))
                {
                    g.FillPath(btnBrush, path);
                }

                // 2. ���ư�ť���֣�������ʾ�������������
                using (SolidBrush textBrush = new SolidBrush(textColor))
                {
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("�������", btnFont, textBrush, _buttonRect, sf);
                }
            }

            /// <summary>
            /// ����Բ�Ǿ���·�������ڻ��ƴ�Բ�ǵİ�ť��
            /// </summary>
            private GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
            {
                GraphicsPath path = new GraphicsPath();
                // ���Ͻ�Բ��
                path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
                // ���Ͻ�Բ��
                path.AddArc(rect.X + rect.Width - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
                // ���½�Բ��
                path.AddArc(rect.X + rect.Width - radius * 2, rect.Y + rect.Height - radius * 2, radius * 2, radius * 2, 0, 90);
                // ���½�Բ��
                path.AddArc(rect.X, rect.Y + rect.Height - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseAllFigures();
                return path;
            }

            /// <summary>
            /// ����ƶ��¼�������Ƿ������ڰ�ť�ϣ����°�ť��ʽ��
            /// </summary>
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                bool newMouseOver = _buttonRect.Contains(e.Location);
                if (newMouseOver != _isMouseOverButton)
                {
                    _isMouseOverButton = newMouseOver;
                    Invalidate(_buttonRect); // ���ػ水ť������������
                }
            }

            /// <summary>
            /// ������¼�������Ƿ�����ť������������
            /// </summary>
            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (_buttonRect.Contains(e.Location))
                {
                    // �����ť�󵯳�MessageBox����ʾ��ǰʱ��
                    string message = $"��ǰʱ�䣺{DateTime.Now:HH:mm:ss}\n�������汳����ť�����ĵ�����";
                    MessageBox.Show(message, "���水ť����", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            /// <summary>
            /// ���ڹر�ʱֹͣ��ʱ��
            /// </summary>
            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                base.OnFormClosing(e);
                _wallpaperUpdateTimer?.Stop();
                _wallpaperUpdateTimer?.Dispose();
            }

            // ������ʽAPI��ʵ����괩͸��
            private const int GWL_EXSTYLE = -20;
            private const int WS_EX_LAYERED = 0x80000;
            private const int WS_EX_TRANSPARENT = 0x20;

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        }
    }
}
