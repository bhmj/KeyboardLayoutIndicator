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

            // Намеренно делаем окно на 1 пиксель ниже, чем монитор(ы), а не
            // ровно по их границе. Если topmost-окно по размеру в точности
            // совпадает с монитором (или полностью его перекрывает), Windows
            // считает это "полноэкранным приложением" (тот же приём, которым
            // сама программа определяет fullscreen — см. coversMonitor в
            // FullscreenDetector) и на это время прячет панель задач под
            // обычные окна — из-за этого все окна, перекрывавшие таскбар,
            // "всплывали" над ним, пока индикатор был показан. Обрезав нижнюю
            // строку пикселей, мы гарантированно не покрываем монитор целиком,
            // и это срабатывание больше не происходит; потеря одной строки
            // пикселей у самого низа экрана незаметна глазу.
            int drawWidth = bounds.Width;
            int drawHeight = Math.Max(1, bounds.Height - 1);

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
                        biWidth = drawWidth,
                        biHeight = -drawHeight, // отрицательная высота = top-down DIB
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0 // BI_RGB
                    }
                };

                hBitmap = NativeMethods.CreateDIBSection(screenDc, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
                if (hBitmap == IntPtr.Zero || bits == IntPtr.Zero)
                    return;

                // Буфер построен на полную высоту bounds.Height (top-down), поэтому
                // здесь просто берём его первые drawHeight строк, отбрасывая самую
                // нижнюю — само по себе взятие среза, а не изменение содержимого.
                Marshal.Copy(bgraPremultiplied, 0, bits, drawWidth * drawHeight * 4);

                oldObj = NativeMethods.SelectObject(memDc, hBitmap);

                var srcPos = new NativeMethods.POINT(0, 0);
                var size = new NativeMethods.SIZE(drawWidth, drawHeight);
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

            // Переустанавливаем окно в topmost-полосу при каждом показе (это
            // дёшево — не двигает и не меняет размер окна, SWP_NOMOVE|SWP_NOSIZE).
            // Без этого после закрытия эксклюзивного fullscreen-приложения (игры
            // со сменой видеорежима) оверлей остаётся с флагом WS_EX_TOPMOST, но
            // фактически проваливается под обычные окна — см. комментарий у
            // HWND_TOPMOST в NativeMethods.cs.
            //
            // ВАЖНО: HWND_INSERTAFTER здесь обязан быть либо HWND_TOPMOST, либо
            // HWND_NOTOPMOST/HWND_TOP/HWND_BOTTOM — если передать сюда handle
            // обычного (не topmost) окна, например панели задач, Windows не
            // просто переставит окно в очереди, а СНИМЕТ у него topmost-статус
            // (задокументированное поведение SetWindowPos), и оверлей тут же
            // проваливается под обычные окна. Причина глюка с "всплытием" окон
            // над таскбаром была не в этом вызове, а в том, что оверлей по
            // размеру совпадал с монитором целиком — исправлено в SetPixels().
            NativeMethods.SetWindowPos(
                _hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
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
