using System;
using System.Runtime.InteropServices;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Overlay
{
    /// <summary>
    /// Полупрозрачное окно без рамки, всегда поверх всех окон, прозрачное для
    /// кликов и не перехватывающее фокус — используется для рамки/заливки-индикатора.
    /// Рисуется через UpdateLayeredWindow, чтобы иметь честный per-pixel альфа-канал.
    /// Это обычное Win32-окно (без System.Windows.Forms.Form), созданное напрямую
    /// через CreateWindowEx, чтобы не тянуть в бинарник WinForms.
    /// </summary>
    public sealed class LayeredOverlayWindow : IDisposable
    {
        private const string ClassName = "KLI_OverlayWindowClass";
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;

        // Делегат WndProc хранится статически, чтобы GC не собрал его, пока
        // класс окна зарегистрирован в системе (регистрация на весь процесс).
        private static readonly NativeMethods.WndProc s_wndProc = WndProc;
        private static bool s_classRegistered;

        private readonly IntPtr _hwnd;

        public LayeredOverlayWindow()
        {
            EnsureClassRegistered();

            IntPtr hInstance = NativeMethods.GetModuleHandle(null);

            _hwnd = NativeMethods.CreateWindowEx(
                NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT |
                NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST,
                ClassName, string.Empty, NativeMethods.WS_POPUP,
                0, 0, 1, 1,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        }

        private static void EnsureClassRegistered()
        {
            if (s_classRegistered) return;

            var wc = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = s_wndProc,
                hInstance = NativeMethods.GetModuleHandle(null),
                hCursor = NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW),
                hbrBackground = IntPtr.Zero,
                lpszClassName = ClassName
            };

            NativeMethods.RegisterClassEx(ref wc);
            s_classRegistered = true;
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
            => NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);

        /// <summary>
        /// Задаёт содержимое окна: буфер BGRA (предумноженная альфа, top-down)
        /// и его положение/размер на экране (в пикселях виртуального экрана).
        /// UpdateLayeredWindow сам обновляет и положение/размер окна на экране,
        /// отдельно двигать/масштабировать его не нужно.
        /// </summary>
        public void SetPixels(byte[] bgraPremultiplied, RectI bounds)
        {
            if (_hwnd == IntPtr.Zero) return;

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
                    _hwnd, screenDc, ref dstPos, ref size,
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
            if (_hwnd == IntPtr.Zero) return;
            if (!NativeMethods.IsWindowVisible(_hwnd))
                NativeMethods.ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
        }

        public void HideOverlay()
        {
            if (_hwnd == IntPtr.Zero) return;
            if (NativeMethods.IsWindowVisible(_hwnd))
                NativeMethods.ShowWindow(_hwnd, SW_HIDE);
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
                NativeMethods.DestroyWindow(_hwnd);
        }
    }
}
