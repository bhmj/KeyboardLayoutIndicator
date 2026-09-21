using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using KeyboardLayoutIndicator.Fullscreen;
using KeyboardLayoutIndicator.Interop;
using KeyboardLayoutIndicator.Keyboard;
using KeyboardLayoutIndicator.Overlay;
using KeyboardLayoutIndicator.Settings;
using KeyboardLayoutIndicator.Sound;
using KeyboardLayoutIndicator.Tray;

namespace KeyboardLayoutIndicator
{
    /// <summary>
    /// Приложение без видимого окна: значок в трее + вся логика отслеживания
    /// раскладки, показа индикатора и звука щелчков. Раньше основывалось на
    /// System.Windows.Forms.ApplicationContext — теперь это обычное скрытое
    /// (никогда не показываемое) Win32-окно с собственным циклом сообщений,
    /// чтобы не тянуть в бинарник WinForms.
    /// </summary>
    public sealed class TrayAppContext : IDisposable
    {
        private const string ClassName = "KLI_MainWindowClass";

        // Идентификаторы таймеров (SetTimer/WM_TIMER) и пунктов меню (WM_COMMAND).
        private static readonly IntPtr PollTimerId = (IntPtr)1;
        private static readonly IntPtr WarmupTimerId = (IntPtr)2;
        private const int CmdOpenSettings = 1;
        private const int CmdExit = 2;

        // Держим "прогрев" звукового устройства заметно чаще, чем интервал
        // тишины (5-6 сек), после которого пропадает первый щелчок.
        private const uint WarmupIntervalMs = 3000;

        // Собственное сообщение: SettingsManager вызывает событие на фоновом
        // потоке (FileSystemWatcher) — постим это сообщение в очередь нашего
        // окна, чтобы обработать перезагрузку настроек на потоке цикла сообщений.
        private const int WM_SETTINGS_RELOADED = NativeMethods.WM_APP + 2;

        // Делегат WndProc хранится в поле, чтобы GC не собрал его, пока
        // класс окна зарегистрирован. Экземпляр статический, т.к. приложение
        // изначально однооконное и однопроцессное (см. проверку мьютекса в Main).
        private static readonly NativeMethods.WndProc s_wndProc = WndProc;
        private static TrayAppContext? s_instance;

        private readonly IntPtr _hwnd;
        private readonly IntPtr _trayIconHandle;

        private readonly SettingsManager _settings;
        private readonly KeyboardLayoutWatcher _layoutWatcher = new();
        private readonly LowLevelKeyboardHook _hook = new();
        private readonly OverlayManager _overlay = new();
        private readonly ClickSoundPlayer _sounds = new();

        private string _currentLayout = "";
        private bool _fullscreenActive;
        private bool _capsLockOn;
        private bool _disposed;

        public TrayAppContext()
        {
            s_instance = this;

            string settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.yaml");
            _settings = new SettingsManager(settingsPath);
            _settings.SettingsReloaded += () => NativeMethods.PostMessage(_hwnd, WM_SETTINGS_RELOADED, IntPtr.Zero, IntPtr.Zero);

            EnsureClassRegistered();
            IntPtr hInstance = NativeMethods.GetModuleHandle(null);
            // Обычное скрытое окно верхнего уровня (просто никогда не вызываем
            // ShowWindow для него) — стандартный приём для приложений "только
            // значок в трее". Не message-only (HWND_MESSAGE), т.к. такие окна
            // не всегда корректно работают с SetForegroundWindow/TrackPopupMenuEx.
            _hwnd = NativeMethods.CreateWindowEx(
                0, ClassName, string.Empty, NativeMethods.WS_OVERLAPPED,
                0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

            _trayIconHandle = TrayIconFactory.LoadTrayIcon();
            AddTrayIcon();

            _hook.KeyDown += OnGlobalKeyDown;
            _hook.Install();

            NativeMethods.SetTimer(_hwnd, PollTimerId, (uint)ClampInterval(_settings.Options.PollIntervalMs), IntPtr.Zero);
            NativeMethods.SetTimer(_hwnd, WarmupTimerId, WarmupIntervalMs, IntPtr.Zero);

            _capsLockOn = ReadCapsLockState();
            Tick();
        }

        private static void EnsureClassRegistered()
        {
            var wc = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = s_wndProc,
                hInstance = NativeMethods.GetModuleHandle(null),
                lpszClassName = ClassName
            };
            NativeMethods.RegisterClassEx(ref wc);
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var self = s_instance;
            if (self == null)
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);

            switch (msg)
            {
                case NativeMethods.WM_TIMER:
                    if (wParam == PollTimerId) self.Tick();
                    else if (wParam == WarmupTimerId && !self._fullscreenActive) self._sounds.Warmup();
                    return IntPtr.Zero;

                case NativeMethods.WM_TRAYICON:
                    int mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);
                    if (mouseMsg == NativeMethods.WM_RBUTTONUP)
                        self.ShowContextMenu();
                    else if (mouseMsg == NativeMethods.WM_LBUTTONDBLCLK)
                        self.OpenSettingsInNotepad();
                    return IntPtr.Zero;

                case NativeMethods.WM_COMMAND:
                    int cmdId = (int)(wParam.ToInt64() & 0xFFFF);
                    if (cmdId == CmdOpenSettings) self.OpenSettingsInNotepad();
                    else if (cmdId == CmdExit) self.ExitApp();
                    return IntPtr.Zero;

                case WM_SETTINGS_RELOADED:
                    self.OnSettingsReloaded();
                    return IntPtr.Zero;

                case NativeMethods.WM_DESTROY:
                    NativeMethods.PostQuitMessage(0);
                    return IntPtr.Zero;

                default:
                    return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }

        /// <summary>Цикл сообщений — работает, пока не будет вызван PostQuitMessage (см. ExitApp/WM_DESTROY).</summary>
        public void Run()
        {
            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        private void AddTrayIcon()
        {
            var data = BuildNotifyIconData();
            data.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
        }

        private NativeMethods.NOTIFYICONDATA BuildNotifyIconData() => new()
        {
            cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = _trayIconHandle,
            szTip = "Keyboard Layout Indicator",
            // ByValTStr-поля не переносят null при маршалинге — явно задаём "".
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };

        private void ShowContextMenu()
        {
            NativeMethods.GetCursorPos(out var pt);

            IntPtr hMenu = NativeMethods.CreatePopupMenu();
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (IntPtr)CmdOpenSettings, "Open settings");
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, string.Empty);
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (IntPtr)CmdExit, "Quit");

            // По правилам Win32 меню трея не закрывается само при клике мимо,
            // если окно-владелец не активировано первым — стандартный обходной путь.
            NativeMethods.SetForegroundWindow(_hwnd);
            NativeMethods.TrackPopupMenuEx(hMenu, NativeMethods.TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);
            NativeMethods.PostMessage(_hwnd, 0, IntPtr.Zero, IntPtr.Zero);

            NativeMethods.DestroyMenu(hMenu);
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
            => (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 1) != 0;

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
                OverlayMode.Border => "border",
                OverlayMode.Overlay => "overlay",
                _ => "none"
            };

            // Текст подсказки трея ограничен 127 символами в Windows.
            string text = $"Layout: {_currentLayout} | {modeText} | sound: {(profile.Sound ? "on" : "off")}";
            if (text.Length > 127) text = text.Substring(0, 127);

            var data = BuildNotifyIconData();
            data.uFlags = NativeMethods.NIF_TIP;
            data.szTip = text;
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
        }

        private void OnGlobalKeyDown(uint vkCode)
        {
            // Обновляем индикацию CapsLock сразу по нажатию, не дожидаясь
            // следующего тика опроса (актуально, если poll-интервал увеличен
            // в настройках) — сама клавиша CapsLock тоже подпадает сюда.
            if (vkCode == NativeMethods.VK_CAPITAL)
            {
                bool capsNow = ReadCapsLockState();
                if (capsNow != _capsLockOn)
                {
                    _capsLockOn = capsNow;
                    if (!_fullscreenActive) UpdateOverlay();
                }
            }

            if (_fullscreenActive) return;
            if (!SymbolKeyClassifier.IsLayoutDependentKey(vkCode)) return;

            var profile = _settings.GetProfile(_currentLayout);
            if (profile.Sound)
                _sounds.Play(profile);
        }

        private void OnSettingsReloaded()
        {
            NativeMethods.KillTimer(_hwnd, PollTimerId);
            NativeMethods.SetTimer(_hwnd, PollTimerId, (uint)ClampInterval(_settings.Options.PollIntervalMs), IntPtr.Zero);
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
                NativeMethods.MessageBox(
                    _hwnd,
                    "Could not open settings file:\n" + ex.Message,
                    "Keyboard Layout Indicator",
                    NativeMethods.MB_OK | NativeMethods.MB_ICONERROR);
            }
        }

        private void ExitApp()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            NativeMethods.KillTimer(_hwnd, PollTimerId);
            NativeMethods.KillTimer(_hwnd, WarmupTimerId);

            var data = BuildNotifyIconData();
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);

            _hook.Dispose();
            _overlay.Dispose();
            _sounds.Dispose();

            if (_trayIconHandle != IntPtr.Zero)
                NativeMethods.DestroyIcon(_trayIconHandle);

            NativeMethods.DestroyWindow(_hwnd); // WM_DESTROY -> PostQuitMessage -> Run() завершится
        }
    }
}
