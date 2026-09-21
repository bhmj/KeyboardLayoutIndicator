using System;
using System.Runtime.InteropServices;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Keyboard
{
    /// <summary>
    /// Глобальный хук WH_KEYBOARD_LL — сообщает о каждом нажатии клавиши в системе,
    /// независимо от того, какое окно активно (кроме окон, запущенных с более высокими
    /// правами, чем у текущего процесса — таково ограничение UIPI Windows).
    /// </summary>
    public sealed class LowLevelKeyboardHook : IDisposable
    {
        // Делегат хранится в поле, чтобы GC не собрал его, пока хук установлен.
        private readonly NativeMethods.LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        /// <summary>
        /// Срабатывает на потоке, установившем хук (обычно — поток UI).
        /// Аргумент — виртуальный код нажатой клавиши (vkCode), чтобы вызывающий
        /// код мог решить, символьная это клавиша (буква/цифра/OEM-знак,
        /// значение которой зависит от раскладки) или служебная (стрелки,
        /// Shift, Alt и т.п.).
        /// </summary>
        public event Action<uint>? KeyDown;

        public LowLevelKeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Install()
        {
            if (_hookId != IntPtr.Zero) return;

            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;

            IntPtr hMod = curModule != null
                ? NativeMethods.GetModuleHandle(curModule.ModuleName)
                : IntPtr.Zero;

            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, hMod, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if ((data.flags & NativeMethods.LLKHF_INJECTED) == 0)
                {
                    KeyDown?.Invoke(data.vkCode);
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }
}
