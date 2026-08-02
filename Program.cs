using System;
using System.Threading;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {

            // Не даём запустить второй экземпляр программы.
            using var mutex = new Mutex(true, "KeyboardLayoutIndicator_SingleInstance_9F3B2E11", out bool createdNew);
            if (!createdNew)
            {
                NativeMethods.MessageBox(
                    IntPtr.Zero,
                    "Индикатор раскладки клавиатуры уже запущен (см. значок в трее).",
                    "Keyboard Layout Indicator",
                    NativeMethods.MB_OK | NativeMethods.MB_ICONINFORMATION);
                return;
            }

            using var app = new TrayAppContext();
            app.Run();

            GC.KeepAlive(mutex);
        }
    }
}
