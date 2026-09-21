using System;
using System.Text;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Keyboard
{
    /// <summary>
    /// Определяет раскладку клавиатуры активного (переднего) окна.
    /// </summary>
    public sealed class KeyboardLayoutWatcher
    {
        /// <summary>
        /// Возвращает имя локали, соответствующее текущей раскладке активного окна,
        /// например "en-US" или "ru-RU". Получаем его напрямую через Win32
        /// LCIDToLocaleName, а не через System.Globalization.CultureInfo — это
        /// не тянет за собой данные ICU (что критично для маленького self-contained
        /// бинарника при InvariantGlobalization).
        /// </summary>
        public string GetCurrentLayoutName()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
            IntPtr hkl = NativeMethods.GetKeyboardLayout(threadId);

            uint lcid = unchecked((uint)((long)hkl & 0xFFFF));

            var sb = new StringBuilder(85); // LOCALE_NAME_MAX_LENGTH
            int len = NativeMethods.LCIDToLocaleName(lcid, sb, sb.Capacity, 0);
            return len > 0 ? sb.ToString() : "unknown";
        }
    }
}
