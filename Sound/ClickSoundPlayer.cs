using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using KeyboardLayoutIndicator.Settings;

namespace KeyboardLayoutIndicator.Sound
{
    /// <summary>
    /// Проигрывает короткий звук-щелчок при каждом нажатии клавиши, если это
    /// включено в профиле текущей раскладки. Поддерживает как встроенный
    /// сгенерированный звук, так и пользовательский .wav из настроек.
    /// </summary>
    public sealed class ClickSoundPlayer : IDisposable
    {
        private readonly SoundPlayer _defaultPlayer;
        private readonly SoundPlayer _keepAlivePlayer;
        private readonly Dictionary<string, SoundPlayer> _customPlayers = new(StringComparer.OrdinalIgnoreCase);

        public ClickSoundPlayer()
        {
            byte[] wav = ClickSoundGenerator.GenerateClickWav();
            _defaultPlayer = new SoundPlayer(new MemoryStream(wav));
            try { _defaultPlayer.Load(); } catch { /* игнорируем, попробуем при Play() */ }

            byte[] keepAliveWav = ClickSoundGenerator.GenerateSilentKeepAliveWav();
            _keepAlivePlayer = new SoundPlayer(new MemoryStream(keepAliveWav));
            try { _keepAlivePlayer.Load(); } catch { /* игнорируем, попробуем при Play() */ }
        }

        /// <summary>
        /// Проигрывает почти неслышимый короткий сигнал, чтобы не дать
        /// звуковому устройству "уснуть" во время пауз без набора текста.
        /// Вызывается по таймеру с интервалом заметно короче тех 5-6 секунд
        /// тишины, после которых обычно начинает "теряться" первый щелчок.
        /// </summary>
        public void Warmup()
        {
            try { _keepAlivePlayer.Play(); }
            catch { /* не критично, пропускаем один цикл прогрева */ }
        }

        public void Play(LayoutProfile profile)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(profile.SoundFile) && File.Exists(profile.SoundFile))
                {
                    if (!_customPlayers.TryGetValue(profile.SoundFile, out var sp))
                    {
                        sp = new SoundPlayer(profile.SoundFile);
                        try { sp.Load(); } catch { /* попробуем всё равно Play() */ }
                        _customPlayers[profile.SoundFile] = sp;
                    }
                    sp.Play();
                }
                else
                {
                    _defaultPlayer.Play();
                }
            }
            catch
            {
                // Ошибки воспроизведения звука не должны валить хук клавиатуры.
            }
        }

        public void Dispose()
        {
            _defaultPlayer.Dispose();
            _keepAlivePlayer.Dispose();
            foreach (var sp in _customPlayers.Values)
                sp.Dispose();
            _customPlayers.Clear();
        }
    }
}
