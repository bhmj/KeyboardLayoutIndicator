using System;
using System.IO;
using System.Text;

namespace KeyboardLayoutIndicator.Sound
{
    /// <summary>
    /// Генерирует короткий "щелчок" (16-бит PCM WAV, ~20 мс) программно,
    /// чтобы программе не требовались внешние файлы звука по умолчанию.
    /// </summary>
    public static class ClickSoundGenerator
    {
        public static byte[] GenerateClickWav()
        {
            const int sampleRate = 44100;
            const double durationSec = 0.02;
            int sampleCount = (int)(sampleRate * durationSec);
            short[] samples = new short[sampleCount];

            var rnd = new Random(12345);
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / sampleRate;
                double envelope = Math.Exp(-t * 260.0); // быстрое затухание -> звук "клика"
                double tone = Math.Sin(2 * Math.PI * 2200 * t);
                double noise = (rnd.NextDouble() * 2 - 1) * 0.3;
                double sample = (tone * 0.7 + noise) * envelope;

                samples[i] = (short)Math.Clamp(sample * short.MaxValue * 0.8, short.MinValue, short.MaxValue);
            }

            return BuildWav(samples, sampleRate);
        }

        /// <summary>
        /// Короткий (почти) беззвучный WAV — не для прослушивания, а чтобы
        /// периодически "будить" аудио-подсистему Windows. После нескольких
        /// секунд полной тишины звуковое устройство/движок нередко переходит
        /// в спящий режим, и следующий реальный звук либо запаздывает, либо
        /// обрезается в начале (из-за задержки выхода из "сна"). Периодическое
        /// проигрывание вот такого тихого сигнала не даёт устройству уснуть,
        /// и обычные щелчки после паузы начинают звучать сразу же.
        /// </summary>
        public static byte[] GenerateSilentKeepAliveWav()
        {
            const int sampleRate = 44100;
            const double durationSec = 0.03;
            int sampleCount = (int)(sampleRate * durationSec);
            short[] samples = new short[sampleCount];

            // Не полный ноль (некоторые аудио-стеки/усилители оптимизируют
            // истинную тишину и всё равно "засыпают"), а сигнал очень малой
            // амплитуды — практически неслышимый, но реально проходящий по
            // всему звуковому тракту.
            const short amplitude = 40; // ~ -58 дБ от полной шкалы
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / sampleRate;
                samples[i] = (short)Math.Round(Math.Sin(2 * Math.PI * 100 * t) * amplitude);
            }

            return BuildWav(samples, sampleRate);
        }

        private static byte[] BuildWav(short[] samples, int sampleRate)
        {
            int dataSize = samples.Length * 2;

            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataSize);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));

                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);              // размер блока fmt
                bw.Write((short)1);        // PCM
                bw.Write((short)1);        // моно
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);  // байт/сек
                bw.Write((short)2);        // выравнивание блока
                bw.Write((short)16);       // бит на сэмпл

                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(dataSize);
                foreach (var s in samples)
                    bw.Write(s);
            }

            return ms.ToArray();
        }
    }
}
