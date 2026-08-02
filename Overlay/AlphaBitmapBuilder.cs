using System;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Overlay
{
    /// <summary>
    /// Строит буфер пикселей BGRA (с предумноженной альфой, top-down) для
    /// layered-окна: либо сплошную заливку, либо рамку по краям экрана.
    /// </summary>
    public static class AlphaBitmapBuilder
    {
        public static byte[] BuildFill(int width, int height, RgbColor color, double opacity)
        {
            byte a = ClampAlpha(opacity);
            byte[] buffer = new byte[width * height * 4];
            FillAll(buffer, color, a);
            return buffer;
        }

        public static byte[] BuildBorder(int width, int height, int thickness, RgbColor color, double opacity)
        {
            byte a = ClampAlpha(opacity);
            byte[] buffer = new byte[width * height * 4]; // изначально всё прозрачно (нули)

            int t = Math.Max(1, Math.Min(thickness, Math.Min(width, height) / 2));

            for (int y = 0; y < t; y++)
                FillRow(buffer, width, y, 0, width, color, a);

            for (int y = height - t; y < height; y++)
                FillRow(buffer, width, y, 0, width, color, a);

            for (int y = t; y < height - t; y++)
            {
                FillRow(buffer, width, y, 0, t, color, a);
                FillRow(buffer, width, y, width - t, width, color, a);
            }

            return buffer;
        }

        /// <summary>
        /// Накладывает сплошную заливку поверх уже существующего буфера
        /// (альфа-композитинг "over", а не перезапись пикселей) — используется,
        /// чтобы индикатор CapsLock не "стирал", а визуально комбинировался
        /// с уже показанным индикатором раскладки.
        /// </summary>
        public static void BlendFillOver(byte[] buffer, int width, int height, RgbColor color, double opacity)
        {
            byte a = ClampAlpha(opacity);
            BlendRowOver(buffer, width, 0, height, 0, width, color, a);
        }

        /// <summary>
        /// Накладывает рамку поверх уже существующего буфера так же, как
        /// <see cref="BlendFillOver"/>. Параметр <paramref name="inset"/> задаёт
        /// отступ от края экрана, на который рамка CapsLock сдвигается внутрь
        /// (например, на толщину уже нарисованной рамки раскладки), чтобы обе
        /// рамки были видны одновременно, а не сливались в одну линию.
        /// </summary>
        public static void BlendBorderOver(byte[] buffer, int width, int height, int thickness, RgbColor color, double opacity, int inset = 0)
        {
            byte a = ClampAlpha(opacity);

            // i (отступ) и t (толщина) всегда ограничиваются так, чтобы i+t не
            // превышало половину меньшей стороны экрана — это гарантирует, что
            // верхняя/левая и нижняя/правая полосы рамки не выйдут за границы
            // буфера и не наложатся друг на друга.
            int halfMin = Math.Min(width, height) / 2;
            int i = Math.Clamp(inset, 0, Math.Max(halfMin - 1, 0));
            int availableThickness = halfMin - i;
            if (availableThickness <= 0) return;
            int t = Math.Clamp(thickness, 1, availableThickness);

            for (int y = i; y < i + t; y++)
                BlendRowOver(buffer, width, y, y + 1, 0, width, color, a);

            for (int y = height - i - t; y < height - i; y++)
                BlendRowOver(buffer, width, y, y + 1, 0, width, color, a);

            for (int y = i + t; y < height - i - t; y++)
            {
                BlendRowOver(buffer, width, y, y + 1, i, i + t, color, a);
                BlendRowOver(buffer, width, y, y + 1, width - i - t, width - i, color, a);
            }
        }

        private static void BlendRowOver(byte[] buffer, int width, int yStart, int yEnd, int xStart, int xEnd, RgbColor color, byte srcA)
        {
            byte srcPb = (byte)(color.B * srcA / 255);
            byte srcPg = (byte)(color.G * srcA / 255);
            byte srcPr = (byte)(color.R * srcA / 255);
            int invA = 255 - srcA;

            for (int y = yStart; y < yEnd; y++)
            {
                int rowOffset = y * width * 4;
                for (int x = xStart; x < xEnd; x++)
                {
                    int idx = rowOffset + x * 4;
                    buffer[idx + 0] = (byte)(srcPb + buffer[idx + 0] * invA / 255);
                    buffer[idx + 1] = (byte)(srcPg + buffer[idx + 1] * invA / 255);
                    buffer[idx + 2] = (byte)(srcPr + buffer[idx + 2] * invA / 255);
                    buffer[idx + 3] = (byte)(srcA + buffer[idx + 3] * invA / 255);
                }
            }
        }

        private static void FillAll(byte[] buffer, RgbColor color, byte a)
        {
            byte pb = (byte)(color.B * a / 255);
            byte pg = (byte)(color.G * a / 255);
            byte pr = (byte)(color.R * a / 255);

            for (int i = 0; i < buffer.Length; i += 4)
            {
                buffer[i + 0] = pb;
                buffer[i + 1] = pg;
                buffer[i + 2] = pr;
                buffer[i + 3] = a;
            }
        }

        private static void FillRow(byte[] buffer, int width, int y, int xStart, int xEnd, RgbColor color, byte a)
        {
            int rowOffset = y * width * 4;
            byte pb = (byte)(color.B * a / 255);
            byte pg = (byte)(color.G * a / 255);
            byte pr = (byte)(color.R * a / 255);

            for (int x = xStart; x < xEnd; x++)
            {
                int idx = rowOffset + x * 4;
                buffer[idx + 0] = pb;
                buffer[idx + 1] = pg;
                buffer[idx + 2] = pr;
                buffer[idx + 3] = a;
            }
        }

        private static byte ClampAlpha(double opacity)
        {
            int v = (int)Math.Round(Math.Clamp(opacity, 0.0, 1.0) * 255);
            return (byte)Math.Clamp(v, 0, 255);
        }
    }
}
