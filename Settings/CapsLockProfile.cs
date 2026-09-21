using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Settings
{
    /// <summary>
    /// Настройки дополнительного индикатора состояния CapsLock. Работает
    /// поверх обычного индикатора раскладки: если включён и CapsLock активен,
    /// его рамка/заливка накладывается на то, что уже показано для текущей
    /// раскладки (а не заменяет это).
    /// </summary>
    public sealed class CapsLockProfile
    {
        public bool Enabled { get; set; } = false;
        public OverlayMode Mode { get; set; } = OverlayMode.Border;
        public RgbColor Color { get; set; } = RgbColor.White;
        public int Thickness { get; set; } = 6;
    }
}
