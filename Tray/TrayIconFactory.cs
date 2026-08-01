using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KeyboardLayoutIndicator.Tray
{
    /// <summary>
    /// Рисует простую иконку "клавиатуры" для трея во время выполнения,
    /// чтобы не требовался отдельный файл .ico.
    /// </summary>
    public static class TrayIconFactory
    {
        public static Icon CreateIcon()
        {
            using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using var bodyBrush = new SolidBrush(Color.FromArgb(255, 40, 120, 220));
                using var bodyPath = RoundedRect(new Rectangle(2, 6, 28, 20), 5);
                g.FillPath(bodyBrush, bodyPath);

                using var pen = new Pen(Color.White, 2f);
                g.DrawLine(pen, 7, 13, 11, 13);
                g.DrawLine(pen, 14, 13, 18, 13);
                g.DrawLine(pen, 21, 13, 25, 13);
                g.DrawLine(pen, 7, 20, 25, 20);
            }

            System.IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
