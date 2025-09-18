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
                Console.WriteLine("=== 带点击按钮的时钟桌面背景 ===");
                Console.WriteLine("操作：1. 桌面右下角按钮可点击（弹窗） 2. 按 Ctrl+C 退出");

                // 1. 生成并设置初始桌面背景（含时钟）
                WallpaperManager.UpdateWallpaper();

                // 2. 启动透明悬浮窗口（承载可点击按钮）
                Application.Run(new TransparentButtonForm());

                // 3. 程序退出时清理临时文件
                WallpaperManager.CleanupTempFile();
                Console.WriteLine("程序已退出，临时文件已清理");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"出错：{ex.Message}");
                WallpaperManager.CleanupTempFile();
                Console.ReadKey();
            }
        }

        // 壁纸管理类（复用之前的时钟背景生成逻辑）
        public static class WallpaperManager
        {
            // 系统API声明（设置壁纸、删除临时文件）
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            private static extern int SystemParametersInfo(int uAction, int uParam, string lpNewDeskWallpaper, int fuWinIni);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, MoveFileFlags dwFlags);

            [Flags]
            private enum MoveFileFlags : uint
            {
                MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004
            }

            // 常量定义
            private const int SPI_SETDESKWALLPAPER = 20;
            private const int SPIF_UPDATEINIFILE = 1;
            private const int SPIF_SENDCHANGE = 2;
            private static string _lastTempFilePath = null;

            /// <summary>
            /// 生成带时钟的渐变背景并设置为桌面壁纸
            /// </summary>
            public static void UpdateWallpaper()
            {
                var screenSize = Screen.PrimaryScreen.Bounds;
                int width = screenSize.Width;
                int height = screenSize.Height;
                string newTempPath = Path.Combine(Path.GetTempPath(), $"ClockWallpaper_{Guid.NewGuid()}.png");

                try
                {
                    // 1. 绘制背景（渐变+时钟）
                    using (Bitmap bmp = new Bitmap(width, height))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        DrawGradientBackground(g, width, height);
                        DrawClock(g, width, height, DateTime.Now);
                        bmp.Save(newTempPath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    // 2. 设置壁纸
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, newTempPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                    // 3. 清理旧文件
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
            /// 绘制渐变背景
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
            /// 绘制实时时钟（复用之前的逻辑）
            /// </summary>
            private static void DrawClock(Graphics g, int width, int height, DateTime time)
            {
                int radius = Math.Min(width, height) / 4;
                PointF center = new PointF(width / 2, height / 2);

                // 绘图质量配置
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 绘制表盘
                using (Pen dialPen = new Pen(Color.Black, 3))
                {
                    g.DrawEllipse(dialPen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
                }

                // 绘制刻度
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

                // 绘制指针
                float secAngle = time.Second * 6f - 90f;
                float minAngle = (time.Minute * 6f + time.Second * 0.1f) - 90f;
                float hourAngle = (time.Hour % 12 * 30f + time.Minute * 0.5f) - 90f;

                DrawHand(g, center, radius * 0.85f, secAngle, Color.Red, 2);
                DrawHand(g, center, radius * 0.7f, minAngle, Color.Black, 4);
                DrawHand(g, center, radius * 0.5f, hourAngle, Color.Black, 6);

                // 绘制中心点
                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    g.FillEllipse(brush, center.X - 5, center.Y - 5, 10, 10);
                }
            }

            /// <summary>
            /// 绘制时钟指针
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
            /// 清理临时文件
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
            /// 尝试删除文件（处理占用情况）
            /// </summary>
            private static void TryDeleteFile(string path)
            {
                try { File.Delete(path); }
                catch { MoveFileEx(path, null, MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT); }
            }
        }

        // 透明悬浮窗口（承载可点击按钮）
        public class TransparentButtonForm : Form
        {
            // 按钮配置
            private readonly Rectangle _buttonRect; // 按钮位置和大小
            private bool _isMouseOverButton; // 鼠标是否悬浮在按钮上
            private readonly System.Windows.Forms.Timer _wallpaperUpdateTimer; // 背景时钟更新定时器

            public TransparentButtonForm()
            {
                // 1. 窗口基础配置（关键：全透明、无边界、顶层显示）
                InitializeWindowSettings();

                // 2. 初始化按钮（位置：桌面右下角，大小：120x40）
                int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = Screen.PrimaryScreen.Bounds.Height;
                _buttonRect = new Rectangle(
                    x: screenWidth - 140, // 右间距20px
                    y: screenHeight - 80, // 下间距40px
                    width: 120,
                    height: 40
                );

                // 3. 初始化时钟背景更新定时器（每秒更新一次）
                _wallpaperUpdateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                _wallpaperUpdateTimer.Tick += (s, e) => WallpaperManager.UpdateWallpaper();
                _wallpaperUpdateTimer.Start();
            }

            /// <summary>
            /// 窗口基础配置（实现透明悬浮效果）
            /// </summary>
            private void InitializeWindowSettings()
            {
                // 窗口大小 = 屏幕大小（覆盖整个桌面）
                Bounds = Screen.PrimaryScreen.Bounds;
                // 无标题栏、无边框
                FormBorderStyle = FormBorderStyle.None;
                // 全透明（仅按钮区域可见）
                BackColor = Color.Magenta; // 选择一个不常用的颜色作为“透明键”
                TransparencyKey = Color.Magenta; // 该颜色将完全透明
                                                 // 置于桌面顶层（但不遮挡其他应用窗口，通过ShowInTaskbar=false实现）
                TopMost = true;
                ShowInTaskbar = false; // 不在任务栏显示，避免干扰
                                       // 允许鼠标穿透（除按钮区域外，点击会穿透到桌面/其他应用）
                SetWindowLongPtr(Handle, GWL_EXSTYLE, GetWindowLongPtr(Handle, GWL_EXSTYLE) | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }

            /// <summary>
            /// 绘制按钮（在窗口透明背景上绘制自定义按钮）
            /// </summary>
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias; // 抗锯齿，按钮边缘更平滑

                // 按钮样式配置（根据鼠标悬浮状态切换颜色）
                Color btnColor = _isMouseOverButton ? Color.FromArgb(200, 70, 130, 180) : Color.FromArgb(150, 70, 130, 180);
                Color textColor = Color.White;
                Font btnFont = new Font("微软雅黑", 12, FontStyle.Bold);

                // 1. 绘制按钮圆角矩形（圆角半径8px）
                using (SolidBrush btnBrush = new SolidBrush(btnColor))
                using (GraphicsPath path = CreateRoundedRectanglePath(_buttonRect, 8))
                {
                    g.FillPath(btnBrush, path);
                }

                // 2. 绘制按钮文字（居中显示“点击弹窗”）
                using (SolidBrush textBrush = new SolidBrush(textColor))
                {
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("点击弹窗", btnFont, textBrush, _buttonRect, sf);
                }
            }

            /// <summary>
            /// 创建圆角矩形路径（用于绘制带圆角的按钮）
            /// </summary>
            private GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
            {
                GraphicsPath path = new GraphicsPath();
                // 左上角圆角
                path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
                // 右上角圆角
                path.AddArc(rect.X + rect.Width - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
                // 右下角圆角
                path.AddArc(rect.X + rect.Width - radius * 2, rect.Y + rect.Height - radius * 2, radius * 2, radius * 2, 0, 90);
                // 左下角圆角
                path.AddArc(rect.X, rect.Y + rect.Height - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseAllFigures();
                return path;
            }

            /// <summary>
            /// 鼠标移动事件（检测是否悬浮在按钮上，更新按钮样式）
            /// </summary>
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                bool newMouseOver = _buttonRect.Contains(e.Location);
                if (newMouseOver != _isMouseOverButton)
                {
                    _isMouseOverButton = newMouseOver;
                    Invalidate(_buttonRect); // 仅重绘按钮区域，提升性能
                }
            }

            /// <summary>
            /// 鼠标点击事件（检测是否点击按钮，触发弹窗）
            /// </summary>
            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (_buttonRect.Contains(e.Location))
                {
                    // 点击按钮后弹出MessageBox，显示当前时间
                    string message = $"当前时间：{DateTime.Now:HH:mm:ss}\n这是桌面背景按钮触发的弹窗！";
                    MessageBox.Show(message, "桌面按钮弹窗", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            /// <summary>
            /// 窗口关闭时停止定时器
            /// </summary>
            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                base.OnFormClosing(e);
                _wallpaperUpdateTimer?.Stop();
                _wallpaperUpdateTimer?.Dispose();
            }

            // 窗口样式API（实现鼠标穿透）
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
