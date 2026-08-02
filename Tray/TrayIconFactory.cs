using System;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Tray
{
    /// <summary>
    /// Загружает иконку для значка в трее. Ничего не рисует во время
    /// выполнения (в отличие от прежней версии на GDI+/System.Drawing) —
    /// сама иконка "зашита" в exe компилятором через &lt;ApplicationIcon&gt;
    /// (см. Resources/app.ico и .csproj). Достаём её через ExtractIconEx по
    /// собственному пути exe — это не требует знания точного числового ID
    /// ресурса (который тулчейн присваивает недокументированным образом),
    /// в отличие от LoadImage(hInstance, id, ...).
    /// </summary>
    public static class TrayIconFactory
    {
        public static IntPtr LoadTrayIcon()
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return IntPtr.Zero;

            var largeIcons = new IntPtr[1];
            var smallIcons = new IntPtr[1];

            uint extracted = NativeMethods.ExtractIconEx(exePath, 0, largeIcons, smallIcons, 1);
            if (extracted == 0)
                return IntPtr.Zero;

            // Для трея нужен маленький вариант; если он почему-то не вернулся —
            // используем большой. Второй (неиспользуемый) хендл сразу освобождаем,
            // иначе он "утечёт" на весь срок жизни процесса.
            bool useSmall = smallIcons[0] != IntPtr.Zero;
            IntPtr used = useSmall ? smallIcons[0] : largeIcons[0];
            IntPtr unused = useSmall ? largeIcons[0] : smallIcons[0];
            if (unused != IntPtr.Zero) NativeMethods.DestroyIcon(unused);

            return used;
        }
    }
}
