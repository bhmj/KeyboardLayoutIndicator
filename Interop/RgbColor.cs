namespace KeyboardLayoutIndicator.Interop
{
    public readonly struct RgbColor
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public RgbColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static readonly RgbColor Red = new(255, 0, 0);
        public static readonly RgbColor White = new(255, 255, 255);

        public int ToArgbKey() => (R << 16) | (G << 8) | B;
    }
}
