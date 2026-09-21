using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Settings
{
    /// <summary>
    /// Загружает settings.yaml, следит за его изменением на диске (например,
    /// после сохранения в Блокноте) и перезагружает настройки на лету.
    /// </summary>
    public sealed class SettingsManager
    {
        public string FilePath { get; }

        public AppOptions Options { get; private set; } = new();
        public CapsLockProfile CapsLock { get; private set; } = new();

        private Dictionary<string, LayoutProfile> _layouts = new(StringComparer.OrdinalIgnoreCase);
        private readonly LayoutProfile _defaultProfile = new() { Mode = OverlayMode.None, Sound = false };
        private const string CapsLockSectionKey = "capslock";

        /// <summary>Событие вызывается на фоновом потоке (не UI) при успешной перезагрузке настроек.</summary>
        public event Action? SettingsReloaded;

        private readonly object _lock = new();
        private FileSystemWatcher? _watcher;
        private System.Threading.Timer? _debounceTimer;

        public SettingsManager(string filePath)
        {
            FilePath = filePath;
            EnsureFileExists();
            Load();
            StartWatcher();
        }

        private void EnsureFileExists()
        {
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, DefaultYaml.Content, new UTF8Encoding(false));
            }
        }

        public void Load()
        {
            try
            {
                string text = File.ReadAllText(FilePath, Encoding.UTF8);
                var raw = YamlLite.Parse(text);

                var options = new AppOptions();
                if (raw.TryGetValue("options", out var opt))
                {
                    options.PollIntervalMs = GetInt(opt, "pollIntervalMs", options.PollIntervalMs);
                    options.DisableInFullscreen = GetBool(opt, "disableInFullscreen", options.DisableInFullscreen);
                    options.BorderOpacity = GetDouble(opt, "borderOpacity", options.BorderOpacity);
                    options.OverlayOpacity = GetDouble(opt, "overlayOpacity", options.OverlayOpacity);
                }

                var capsLock = new CapsLockProfile();
                if (raw.TryGetValue(CapsLockSectionKey, out var capsSection))
                {
                    capsLock.Enabled = GetBool(capsSection, "enabled", capsLock.Enabled);
                    capsLock.Mode = ParseMode(GetString(capsSection, "mode", "border"));
                    capsLock.Color = ParseColor(GetString(capsSection, "color", "255,255,255"));
                    capsLock.Thickness = GetInt(capsSection, "thickness", 6);
                }

                var layouts = new Dictionary<string, LayoutProfile>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in raw)
                {
                    if (string.Equals(kv.Key, "options", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.Equals(kv.Key, CapsLockSectionKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var section = kv.Value;
                    var profile = new LayoutProfile
                    {
                        Mode = ParseMode(GetString(section, "mode", "none")),
                        Color = ParseColor(GetString(section, "color", "255,0,0")),
                        Thickness = GetInt(section, "thickness", 12),
                        Sound = GetBool(section, "sound", false),
                        SoundFile = GetString(section, "soundFile", "")
                    };

                    layouts[kv.Key] = profile;
                }

                lock (_lock)
                {
                    Options = options;
                    CapsLock = capsLock;
                    _layouts = layouts;
                }
            }
            catch
            {
                // Файл повреждён или недоступен на чтение (например, ещё не сохранён) —
                // оставляем предыдущие настройки в силе, ничего не роняем.
            }

            SettingsReloaded?.Invoke();
        }

        public LayoutProfile GetProfile(string layoutName)
        {
            lock (_lock)
            {
                if (_layouts.TryGetValue(layoutName, out var p))
                    return p;
                return _defaultProfile;
            }
        }

        private void StartWatcher()
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(FilePath)) ?? ".";
            string file = Path.GetFileName(FilePath);

            _watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
            };
            _watcher.Changed += (_, __) => DebouncedReload();
            _watcher.Created += (_, __) => DebouncedReload();
            _watcher.Renamed += (_, __) => DebouncedReload();
            _watcher.EnableRaisingEvents = true;
        }

        private void DebouncedReload()
        {
            // Блокнот и другие редакторы могут вызвать несколько событий подряд
            // при одном сохранении файла — небольшая задержка сглаживает это.
            _debounceTimer ??= new System.Threading.Timer(_ => Load(), null, Timeout.Infinite, Timeout.Infinite);
            _debounceTimer.Change(300, Timeout.Infinite);
        }

        private static OverlayMode ParseMode(string s) => s.Trim().ToLowerInvariant() switch
        {
            "border" => OverlayMode.Border,
            "рамка" => OverlayMode.Border,
            "overlay" => OverlayMode.Overlay,
            "filter" => OverlayMode.Overlay,
            "фильтр" => OverlayMode.Overlay,
            _ => OverlayMode.None
        };

        private static RgbColor ParseColor(string s)
        {
            try
            {
                var parts = s.Split(',');
                if (parts.Length >= 3)
                {
                    int r = int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                    int g = int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                    int b = int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                    return new RgbColor(
                        (byte)Math.Clamp(r, 0, 255),
                        (byte)Math.Clamp(g, 0, 255),
                        (byte)Math.Clamp(b, 0, 255));
                }
            }
            catch
            {
                // используем цвет по умолчанию
            }
            return RgbColor.Red;
        }

        private static string GetString(Dictionary<string, string> d, string key, string def)
            => d.TryGetValue(key, out var v) ? v : def;

        private static int GetInt(Dictionary<string, string> d, string key, int def)
            => d.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : def;

        private static bool GetBool(Dictionary<string, string> d, string key, bool def)
            => d.TryGetValue(key, out var v) && bool.TryParse(v, out var r) ? r : def;

        private static double GetDouble(Dictionary<string, string> d, string key, double def)
            => d.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : def;
    }
}
