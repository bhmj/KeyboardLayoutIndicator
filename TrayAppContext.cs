using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using KeyboardLayoutIndicator.Fullscreen;
using KeyboardLayoutIndicator.Keyboard;
using KeyboardLayoutIndicator.Overlay;
using KeyboardLayoutIndicator.Settings;
using KeyboardLayoutIndicator.Sound;
using KeyboardLayoutIndicator.Tray;

namespace KeyboardLayoutIndicator
{
    /// <summary>
    /// Контекст приложения без главного окна: значок в трее + вся логика
    /// отслеживания раскладки, показа индикатора и звука щелчков.
    /// </summary>
    public sealed class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly SettingsManager _settings;
        private readonly KeyboardLayoutWatcher _layoutWatcher = new();
        private readonly LowLevelKeyboardHook _hook = new();
        private readonly OverlayManager _overlay = new();
        private readonly ClickSoundPlayer _sounds = new();
        private readonly System.Windows.Forms.Timer _pollTimer;
        private readonly System.Windows.Forms.Timer _warmupTimer;

        // Скрытый control используется только для маршалинга вызовов с фоновых
        // потоков (FileSystemWatcher) на поток интерфейса.
        private readonly Control _uiMarshal = new();

        private string _currentLayout = "";
        private bool _fullscreenActive;
        private bool _capsLockOn;

        // Держим "прогрев" звукового устройства заметно чаще, чем интервал
        // тишины (5-6 сек), после которого пропадает первый щелчок.
        private const int WarmupIntervalMs = 3000;

        public TrayAppContext()
        {
            _uiMarshal.CreateControl();

            string settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.yaml");
            _settings = new SettingsManager(settingsPath);
            _settings.SettingsReloaded += () => SafeInvoke(OnSettingsReloaded);

            _trayIcon = new NotifyIcon
            {
                Icon = TrayIconFactory.CreateIcon(),
                Visible = true,
                Text = "Индикатор раскладки клавиатуры"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть файл настроек", null, (_, __) => OpenSettingsInNotepad());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Выход", null, (_, __) => ExitApp());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (_, __) => OpenSettingsInNotepad();

            _hook.KeyDown += OnGlobalKeyDown;
            _hook.Install();

            _pollTimer = new System.Windows.Forms.Timer
            {
                Interval = ClampInterval(_settings.Options.PollIntervalMs)
            };
            _pollTimer.Tick += (_, __) => Tick();
            _pollTimer.Start();

            _warmupTimer = new System.Windows.Forms.Timer { Interval = WarmupIntervalMs };
            _warmupTimer.Tick += (_, __) => { if (!_fullscreenActive) _sounds.Warmup(); };
            _warmupTimer.Start();

            _capsLockOn = ReadCapsLockState();
            Tick();
        }

        private void SafeInvoke(Action action)
        {
            try
            {
                if (_uiMarshal.IsHandleCreated)
                    _uiMarshal.BeginInvoke(action);
            }
            catch (ObjectDisposedException)
            {
                // приложение уже завершает работу
            }
        }

        private void Tick()
        {
            _fullscreenActive = _settings.Options.DisableInFullscreen && FullscreenDetector.IsForegroundFullscreen();

            string layout = _layoutWatcher.GetCurrentLayoutName();
            bool layoutChanged = !string.Equals(layout, _currentLayout, StringComparison.OrdinalIgnoreCase);
            if (layoutChanged)
            {
                _currentLayout = layout;
                UpdateTrayText();
            }

            _capsLockOn = ReadCapsLockState();
            UpdateOverlay();
        }

        private static bool ReadCapsLockState()
            => (Interop.NativeMethods.GetKeyState(Interop.NativeMethods.VK_CAPITAL) & 1) != 0;

        private void UpdateOverlay()
        {
            var profile = _settings.GetProfile(_currentLayout);
            if (_fullscreenActive)
                _overlay.Hide();
            else
                _overlay.Apply(profile, _settings.Options, _settings.CapsLock, _capsLockOn);
        }

        private void UpdateTrayText()
        {
            var profile = _settings.GetProfile(_currentLayout);
            string modeText = profile.Mode switch
            {
                OverlayMode.Border => "рамка",
                OverlayMode.Overlay => "заливка",
                _ => "нет индикатора"
            };

            // Текст подсказки трея ограничен 127 символами в Windows.
            string text = $"Раскладка: {_currentLayout} | {modeText} | звук: {(profile.Sound ? "вкл" : "выкл")}";
            _trayIcon.Text = text.Length > 127 ? text.Substring(0, 127) : text;
        }

        private void OnGlobalKeyDown(uint vkCode)
        {
            // Обновляем индикацию CapsLock сразу по нажатию, не дожидаясь
            // следующего тика опроса (актуально, если poll-интервал увеличен
            // в настройках) — сама клавиша CapsLock тоже подпадает сюда.
            if (vkCode == Interop.NativeMethods.VK_CAPITAL)
            {
                bool capsNow = ReadCapsLockState();
                if (capsNow != _capsLockOn)
                {
                    _capsLockOn = capsNow;
                    if (!_fullscreenActive) UpdateOverlay();
                }
            }

            if (_fullscreenActive) return;
            if (!Keyboard.SymbolKeyClassifier.IsLayoutDependentKey(vkCode)) return;

            var profile = _settings.GetProfile(_currentLayout);
            if (profile.Sound)
                _sounds.Play(profile);
        }

        private void OnSettingsReloaded()
        {
            _pollTimer.Interval = ClampInterval(_settings.Options.PollIntervalMs);
            UpdateTrayText();
            UpdateOverlay();
        }

        private static int ClampInterval(int ms) => Math.Max(50, Math.Min(ms, 2000));

        private void OpenSettingsInNotepad()
        {
            try
            {
                var psi = new ProcessStartInfo("notepad.exe", $"\"{_settings.FilePath}\"")
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось открыть файл настроек:\n" + ex.Message,
                    "Keyboard Layout Indicator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ExitApp()
        {
            _trayIcon.Visible = false;
            _pollTimer.Stop();
            _warmupTimer.Stop();
            _hook.Dispose();
            _overlay.Dispose();
            _sounds.Dispose();
            _trayIcon.Dispose();
            Application.Exit();
        }
    }
}
