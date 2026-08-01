using System.Drawing;

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
        public Color Color { get; set; } = Color.Red;
        public int Thickness { get; set; } = 12;
        public bool Sound { get; set; } = false;
        public string SoundFile { get; set; } = "";
    }
}
