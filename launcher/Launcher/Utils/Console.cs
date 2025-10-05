using System.Runtime.InteropServices;

namespace Launcher.Utils
{
    public static class ConsoleManager
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;

        private static IntPtr ConsoleHandle = GetConsoleWindow();

        public static void HideConsole()
        {
            ShowWindow(ConsoleHandle, SW_HIDE);
        }
    }
}
