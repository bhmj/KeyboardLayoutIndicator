using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Settings
{
    public enum OverlayMode
    {
        None,
        Border,
        Overlay
    }

    public sealed class LayoutProfile
    {
        public OverlayMode Mode { get; set; } = OverlayMode.None;
        public RgbColor Color { get; set; } = RgbColor.Red;
        public int Thickness { get; set; } = 12;
        public bool Sound { get; set; } = false;
        public string SoundFile { get; set; } = "";
    }
}
