using Spectre.Console;

namespace Launcher.Utils
{
    public static class Terminal
    {
        private static string _prefix = "[orange1]Classic[/][blue]Counter[/]";
        private static string _grey = "grey82";
        private static string _seperator = "[grey50]|[/]";

        public static void Init()
        {
            AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]Launcher maintained by heapy[/]");
            AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]https://github.com/ClassicCounter [/]");
            AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]Version: {Version.Current}[/]");
        }

        public static void Print(object? message)
            => AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]{message}[/]");

        public static void Success(object? message)
            => AnsiConsole.MarkupLine($"{_prefix} {_seperator} [green1]{message}[/]");

        public static void Warning(object? message)
            => AnsiConsole.MarkupLine($"{_prefix} {_seperator} [yellow]{message}[/]");

        public static void Error(object? message)
            => AnsiConsole.MarkupLine($"{_prefix} {_seperator} [red]{message}[/]");

        public static void Debug(object? message)
            => AnsiConsole.MarkupLine($"[purple]{message}[/]");

        private static string Date()
            => $"[{_grey}]{DateTime.Now.ToString("HH:mm:ss")}[/]";
    }
}
