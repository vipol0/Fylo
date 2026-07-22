using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Fylo.Helpers
{
    /// <summary>
    /// Включает тёмный системный заголовок окна (title bar) на Windows 10/11
    /// через DWM API — тот же механизм, которым пользуется сам Проводник
    /// в тёмной теме. Не влияет на содержимое окна, только на рамку/шапку.
    /// </summary>
    public static class DarkTitleBarHelper
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        // До Windows 10 20H1 атрибут был под другим номером
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        /// <summary>
        /// Применяет тёмный заголовок к уже открытому окну (Window.SourceInitialized).
        /// Безопасно вызывать на любой версии Windows — на старых системах
        /// вызов DWM просто ничего не сделает, без исключений.
        /// </summary>
        public static void Apply(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDarkMode = 1;

            // Пробуем актуальный атрибут (Windows 10 20H1+ и Windows 11)
            int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

            if (result != 0)
            {
                // Если не сработало — пробуем старый атрибут (ранние сборки Windows 10)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDarkMode, sizeof(int));
            }
        }
    }
}
