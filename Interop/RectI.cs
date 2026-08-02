namespace KeyboardLayoutIndicator.Interop
{
    public readonly struct RectI
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public RectI(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Left => X;
        public int Top => Y;
    }
}
