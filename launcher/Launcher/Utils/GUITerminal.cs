using System;

namespace Launcher.Utils
{
    public static class GUITerminal
    {
        public static event EventHandler<TerminalMessageEventArgs>? MessageLogged;

        public static void Init()
        {
            Log("Launcher maintained by Ollum", MessageType.Info);
            Log("Coded with love by heapy <3", MessageType.Info);
            Log("https://github.com/ClassicCounter", MessageType.Info);
            Log($"Version: {Version.Current}", MessageType.Info);
        }

        public static void Log(object? message, MessageType type = MessageType.Info)
        {
            var args = new TerminalMessageEventArgs
            {
                Message = message?.ToString() ?? string.Empty,
                Type = type,
                Timestamp = DateTime.Now
            };
            
            MessageLogged?.Invoke(null, args);
        }

        public static void Print(object? message)
            => Log(message, MessageType.Info);

        public static void Success(object? message)
            => Log(message, MessageType.Success);

        public static void Warning(object? message)
            => Log(message, MessageType.Warning);

        public static void Error(object? message)
            => Log(message, MessageType.Error);

        public static void Debug(object? message)
            => Log(message, MessageType.Debug);
    }

    public class TerminalMessageEventArgs : EventArgs
    {
        public string Message { get; set; } = "";
        public MessageType Type { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum MessageType
    {
        Info,
        Success,
        Warning,
        Error,
        Debug
    }
}
