using System;
using System.Globalization;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Keyboard
{
    /// <summary>
    /// Определяет раскладку клавиатуры активного (переднего) окна.
    /// </summary>
    public sealed class KeyboardLayoutWatcher
    {
        /// <summary>
        /// Возвращает имя культуры, соответствующее текущей раскладке активного окна,
        /// например "en-US" или "ru-RU".
        /// </summary>
        public string GetCurrentLayoutName()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
            IntPtr hkl = NativeMethods.GetKeyboardLayout(threadId);

            int langId = unchecked((int)((long)hkl & 0xFFFF));

            try
            {
                var culture = CultureInfo.GetCultureInfo(langId);
                return culture.Name;
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
