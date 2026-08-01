using System;
using System.Drawing;
using System.Windows.Forms;
using KeyboardLayoutIndicator.Settings;

namespace KeyboardLayoutIndicator.Overlay
{
    /// <summary>
    /// Показывает рамку и/или заливку на весь экран в соответствии с профилем
    /// текущей раскладки, поверх которой (если включено) накладывается
    /// индикатор состояния CapsLock. Оба слоя рисуются в один общий буфер
    /// одного и того же layered-окна.
    /// </summary>
    public sealed class OverlayManager : IDisposable
    {
        private LayeredOverlayWindow? _window;

        private readonly record struct BufferKey(
            OverlayMode LayoutMode, int LayoutColor, int LayoutThickness, double LayoutOpacity,
            bool CapsShown, OverlayMode CapsMode, int CapsColor, int CapsThickness, double CapsOpacity,
            int ScreenWidth, int ScreenHeight);

        private BufferKey? _lastKey;

        public void Apply(LayoutProfile profile, AppOptions options, CapsLockProfile capsProfile, bool capsLockOn)
        {
            bool layoutShown = profile.Mode != OverlayMode.None;
            bool capsShown = capsProfile.Enabled && capsProfile.Mode != OverlayMode.None && capsLockOn;

            if (!layoutShown && !capsShown)
            {
                Hide();
                return;
            }

            double layoutOpacity = profile.Mode == OverlayMode.Border ? options.BorderOpacity : options.OverlayOpacity;
            double capsOpacity = capsProfile.Mode == OverlayMode.Border ? options.BorderOpacity : options.OverlayOpacity;

            Rectangle bounds = SystemInformation.VirtualScreen;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);

            var key = new BufferKey(
                profile.Mode, profile.Color.ToArgb(), profile.Thickness, layoutOpacity,
                capsShown, capsProfile.Mode, capsProfile.Color.ToArgb(), capsProfile.Thickness, capsOpacity,
                bounds.Width, bounds.Height);

            _window ??= new LayeredOverlayWindow();

            if (_lastKey == null || !_lastKey.Equals(key))
            {
                byte[] buffer = new byte[bounds.Width * bounds.Height * 4];

                if (layoutShown)
                {
                    buffer = profile.Mode == OverlayMode.Border
                        ? AlphaBitmapBuilder.BuildBorder(bounds.Width, bounds.Height, profile.Thickness, profile.Color, layoutOpacity)
                        : AlphaBitmapBuilder.BuildFill(bounds.Width, bounds.Height, profile.Color, layoutOpacity);
                }

                if (capsShown)
                {
                    if (capsProfile.Mode == OverlayMode.Border)
                    {
                        // Если раскладка тоже рисует рамку — сдвигаем рамку CapsLock
                        // внутрь на её толщину, чтобы получились две видимые рамки,
                        // а не слияние в одну линию.
                        int inset = (layoutShown && profile.Mode == OverlayMode.Border) ? profile.Thickness : 0;
                        AlphaBitmapBuilder.BlendBorderOver(buffer, bounds.Width, bounds.Height, capsProfile.Thickness, capsProfile.Color, capsOpacity, inset);
                    }
                    else
                    {
                        AlphaBitmapBuilder.BlendFillOver(buffer, bounds.Width, bounds.Height, capsProfile.Color, capsOpacity);
                    }
                }

                _window.SetPixels(buffer, bounds);
                _lastKey = key;
            }

            _window.ShowOverlay();
        }

        public void Hide()
        {
            _window?.HideOverlay();
        }

        public void Dispose()
        {
            _window?.Dispose();
            _window = null;
        }
    }
}
