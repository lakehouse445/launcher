using System.Windows;

namespace Launcher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Check if we need to run in console mode for certain operations
                if (e.Args.Contains("--console") || e.Args.Contains("--patch-only"))
                {
                    // Run console mode
                    this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    _ = Task.Run(async () => 
                    {
                        await ConsoleProgram.RunConsoleMode(e.Args);
                        this.Dispatcher.Invoke(() => this.Shutdown());
                    });
                    return;
                }
                
                // Parse command line arguments for GUI mode
                foreach (string arg in e.Args)
                {
                    Environment.SetEnvironmentVariable($"LAUNCHER_ARG_{arg.Replace("-", "").ToUpper()}", "true");
                }

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup failed: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                               "Launcher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Shutdown(1);
            }
        }
    }
}
