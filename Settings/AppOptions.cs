namespace KeyboardLayoutIndicator.Settings
{
    public sealed class AppOptions
    {
        /// <summary>Как часто (в мс) проверять текущую раскладку клавиатуры.</summary>
        public int PollIntervalMs { get; set; } = 120;

        /// <summary>Отключать рамку/заливку/звук, пока активно полноэкранное приложение (игра).</summary>
        public bool DisableInFullscreen { get; set; } = true;

        /// <summary>Прозрачность рамки (0.0 - невидима, 1.0 - непрозрачная).</summary>
        public double BorderOpacity { get; set; } = 0.55;

        /// <summary>Прозрачность заливки на весь экран (0.0 - невидима, 1.0 - непрозрачная).</summary>
        public double OverlayOpacity { get; set; } = 0.12;
    }
}
