using System;
using System.Threading;
using System.Windows.Forms;

namespace KeyboardLayoutIndicator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Не даём запустить второй экземпляр программы.
            using var mutex = new Mutex(true, "KeyboardLayoutIndicator_SingleInstance_9F3B2E11", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "Индикатор раскладки клавиатуры уже запущен (см. значок в трее).",
                    "Keyboard Layout Indicator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Application.Run(new TrayAppContext());

            GC.KeepAlive(mutex);
        }
    }
}
