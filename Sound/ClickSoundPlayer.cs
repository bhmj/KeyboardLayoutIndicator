using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using KeyboardLayoutIndicator.Interop;
using KeyboardLayoutIndicator.Settings;

namespace KeyboardLayoutIndicator.Sound
{
    /// <summary>
    /// Проигрывает короткий звук-щелчок при каждом нажатии клавиши, если это
    /// включено в профиле текущей раскладки. Поддерживает как встроенный
    /// сгенерированный звук, так и пользовательский .wav из настроек.
    /// Использует winmm.dll напрямую (без System.Media.SoundPlayer/WinForms).
    /// </summary>
    public sealed class ClickSoundPlayer : IDisposable
    {
        // Встроенные звуки (щелчок и "прогрев") воспроизводятся асинхронно
        // из памяти (SND_MEMORY|SND_ASYNC), поэтому их буферы должны быть
        // закреплены в памяти (GCHandle, Pinned) на весь срок жизни плеера —
        // Windows может ещё читать их уже после возврата из PlaySound().
        private readonly GCHandle _clickHandle;
        private readonly GCHandle _keepAliveHandle;

        public ClickSoundPlayer()
        {
            _clickHandle = GCHandle.Alloc(ClickSoundGenerator.GenerateClickWav(), GCHandleType.Pinned);
            _keepAliveHandle = GCHandle.Alloc(ClickSoundGenerator.GenerateSilentKeepAliveWav(), GCHandleType.Pinned);
        }

        /// <summary>
        /// Проигрывает почти неслышимый короткий сигнал, чтобы не дать
        /// звуковому устройству "уснуть" во время пауз без набора текста.
        /// Вызывается по таймеру с интервалом заметно короче тех 5-6 секунд
        /// тишины, после которых обычно начинает "теряться" первый щелчок.
        /// </summary>
        public void Warmup()
        {
            try
            {
                NativeMethods.PlaySound(_keepAliveHandle.AddrOfPinnedObject(), IntPtr.Zero,
                    NativeMethods.SND_MEMORY | NativeMethods.SND_ASYNC | NativeMethods.SND_NODEFAULT);
            }
            catch { /* не критично, пропускаем один цикл прогрева */ }
        }

        public void Play(LayoutProfile profile)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(profile.SoundFile) && File.Exists(profile.SoundFile))
                {
                    // SND_FILENAME: winmm сам открывает и декодирует файл асинхронно,
                    // управляемая память тут ни при чём — пиннинг не нужен.
                    NativeMethods.PlaySound(profile.SoundFile, IntPtr.Zero,
                        NativeMethods.SND_FILENAME | NativeMethods.SND_ASYNC | NativeMethods.SND_NODEFAULT);
                }
                else
                {
                    NativeMethods.PlaySound(_clickHandle.AddrOfPinnedObject(), IntPtr.Zero,
                        NativeMethods.SND_MEMORY | NativeMethods.SND_ASYNC | NativeMethods.SND_NODEFAULT);
                }
            }
            catch
            {
                // Ошибки воспроизведения звука не должны валить хук клавиатуры.
            }
        }

        public void Dispose()
        {
            if (_clickHandle.IsAllocated) _clickHandle.Free();
            if (_keepAliveHandle.IsAllocated) _keepAliveHandle.Free();
        }
    }
}
