using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Overlay
{
    /// <summary>
    /// Полупрозрачное окно без рамки, всегда поверх всех окон, прозрачное для
    /// кликов и не перехватывающее фокус — используется для рамки/заливки-индикатора.
    /// Рисуется через UpdateLayeredWindow, чтобы иметь честный per-pixel альфа-канал.
    /// </summary>
    public sealed class LayeredOverlayWindow : Form
    {
        public LayeredOverlayWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Bounds = new Rectangle(0, 0, 1, 1);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED
                             | NativeMethods.WS_EX_TRANSPARENT
                             | NativeMethods.WS_EX_TOOLWINDOW
                             | NativeMethods.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        /// <summary>
        /// Задаёт содержимое окна: буфер BGRA (предумноженная альфа, top-down)
        /// и его положение/размер на экране (в пикселях виртуального экрана).
        /// </summary>
        public void SetPixels(byte[] bgraPremultiplied, Rectangle bounds)
        {
            if (!IsHandleCreated)
            {
                _ = Handle; // форсируем создание хэндла без показа окна
            }

            Bounds = bounds;

            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldObj = IntPtr.Zero;

            try
            {
                memDc = NativeMethods.CreateCompatibleDC(screenDc);

                var bmi = new NativeMethods.BITMAPINFO
                {
                    bmiHeader = new NativeMethods.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                        biWidth = bounds.Width,
                        biHeight = -bounds.Height, // отрицательная высота = top-down DIB
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0 // BI_RGB
                    }
                };

                hBitmap = NativeMethods.CreateDIBSection(screenDc, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
                if (hBitmap == IntPtr.Zero || bits == IntPtr.Zero)
                    return;

                Marshal.Copy(bgraPremultiplied, 0, bits, bgraPremultiplied.Length);

                oldObj = NativeMethods.SelectObject(memDc, hBitmap);

                var srcPos = new NativeMethods.POINT(0, 0);
                var size = new NativeMethods.SIZE(bounds.Width, bounds.Height);
                var dstPos = new NativeMethods.POINT(bounds.Left, bounds.Top);
                var blend = new NativeMethods.BLENDFUNCTION
                {
                    BlendOp = NativeMethods.AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = NativeMethods.AC_SRC_ALPHA
                };

                NativeMethods.UpdateLayeredWindow(
                    Handle, screenDc, ref dstPos, ref size,
                    memDc, ref srcPos, 0, ref blend, NativeMethods.ULW_ALPHA);
            }
            finally
            {
                if (oldObj != IntPtr.Zero) NativeMethods.SelectObject(memDc, oldObj);
                if (hBitmap != IntPtr.Zero) NativeMethods.DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) NativeMethods.DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public void ShowOverlay()
        {
            if (!Visible) Show();
        }

        public void HideOverlay()
        {
            if (Visible) Hide();
        }
    }
}
