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
