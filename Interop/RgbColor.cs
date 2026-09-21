namespace KeyboardLayoutIndicator.Interop
{
    /// <summary>
    /// Простой RGB-цвет без альфы (её храним отдельно как opacity 0.0-1.0).
    /// Заменяет System.Drawing.Color, чтобы не тянуть System.Drawing.Common
    /// в бинарник ради одной структуры из трёх байт.
    /// </summary>
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
