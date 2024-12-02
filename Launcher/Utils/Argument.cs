namespace Launcher.Utils
{
    public static class Argument
    {
        private static List<string> _launcherArguments = new()
        {
            "--debug-mode",
            "--skip-updates",
            "--skip-validating",
        };

        public static bool Exists(string argument)
        {
            IEnumerable<string> arguments = Environment.GetCommandLineArgs();

            foreach (string arg in arguments)
                if (arg.ToLowerInvariant() == argument) return true;

            return false;
        }

        public static List<string> GenerateGameArguments(bool passLauncherArguments = false)
        {
            IEnumerable<string> launcherArguments = Environment.GetCommandLineArgs();
            List<string> gameArguments = new();

            foreach (string arg in launcherArguments)
                if ((passLauncherArguments || !_launcherArguments.Contains(arg.ToLowerInvariant()))
                    && !arg.EndsWith(".exe"))
                    gameArguments.Add(arg.ToLowerInvariant());

            return gameArguments;
        }
    }
}
