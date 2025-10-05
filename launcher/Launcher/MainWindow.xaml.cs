using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Win32;
using Launcher.Utils;
using Version = Launcher.Utils.Version;

namespace Launcher
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<StatusMessage> StatusMessages { get; set; } = new();
        private bool _isOperationInProgress = false;
        private readonly LauncherLogic _launcherLogic = new LauncherLogic();

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                this.DataContext = this;
                StatusMessagesControl.ItemsSource = StatusMessages;
                _launcherLogic.StatusUpdated += OnStatusUpdated;
                _launcherLogic.ProgressUpdated += OnProgressUpdated;
                _launcherLogic.OperationCompleted += OnOperationCompleted;
                
                InitializeUI();
                
                // Add initial status message
                AddStatusMessage("GUI Launcher initialized successfully!", "#90EE90");
                
                // Initialize launcher logic in background - use Dispatcher to avoid threading issues
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await InitializeLauncher();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AddStatusMessage($"Background initialization failed: {ex.Message}", "#FF6B6B");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize launcher: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeUI()
        {
            VersionText.Text = $"Version {Version.Current}";
            GameDirectoryText.Text = Directory.GetCurrentDirectory();
            
            // Load saved settings
            LoadSettings();
        }

        private async Task InitializeLauncher()
        {
            try
            {
                // Get launch arguments on the main thread to avoid cross-thread issues
                var launchArgs = await Dispatcher.InvokeAsync(() => GetLaunchArguments());
                
                Dispatcher.Invoke(() => AddStatusMessage("Starting launcher initialization...", "#CCCCCC"));
                await _launcherLogic.InitializeAsync(launchArgs);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    AddStatusMessage($"Initialization failed: {ex.Message}", "#FF6B6B");
                    StatusFooterText.Text = "Error";
                    
                    // Still enable some basic functionality
                    LaunchButton.IsEnabled = true;
                    ValidateButton.IsEnabled = true;
                });
            }
        }

        private void OnStatusUpdated(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AddStatusMessage(message, "#CCCCCC");
            });
        }

        private void OnProgressUpdated(object? sender, ProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                MainProgressBar.Value = e.Percentage;
                ProgressText.Text = e.Status;
                StatusFooterText.Text = e.Status;
                ProgressPercentage.Text = $"{e.Percentage:F0}%";
                
                // Update status indicator color
                if (e.Percentage == 100)
                {
                    StatusIndicator.Foreground = new SolidColorBrush(Colors.LightGreen);
                }
                else if (e.Percentage > 0)
                {
                    StatusIndicator.Foreground = FindResource("ClassicCounterOrange") as SolidColorBrush;
                }
            });
        }

        private void OnOperationCompleted(object? sender, bool success)
        {
            Dispatcher.Invoke(() =>
            {
                _isOperationInProgress = false;
                LaunchButton.IsEnabled = success;
                ValidateButton.IsEnabled = true;
                
                if (success)
                {
                    MainProgressBar.Value = 100;
                    ProgressText.Text = "Ready to launch";
                    StatusFooterText.Text = "Ready";
                    AddStatusMessage("All operations completed successfully!", "#90EE90");
                }
                else
                {
                    MainProgressBar.Value = 0;
                    ProgressText.Text = "Operation failed";
                    StatusFooterText.Text = "Error";
                    AddStatusMessage("Operation failed. Check the logs above.", "#FF6B6B");
                }
            });
        }

        private void AddStatusMessage(string message, string color)
        {
            var statusMessage = new StatusMessage
            {
                Message = $"[{DateTime.Now:HH:mm:ss}] {message}",
                Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))
            };
            
            StatusMessages.Add(statusMessage);
            
            // Keep only the last 100 messages
            while (StatusMessages.Count > 100)
            {
                StatusMessages.RemoveAt(0);
            }
            
            // Auto-scroll to bottom
            if (StatusMessages.Count > 0)
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(this);
                scrollViewer?.ScrollToEnd();
            }
            
            // Update progress percentage display
            if (MainProgressBar.Value > 0)
            {
                ProgressPercentage.Text = $"{MainProgressBar.Value:F0}%";
            }
        }

        private LaunchArguments GetLaunchArguments()
        {
            // Ensure this method is called from the UI thread
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(() => GetLaunchArguments());
            }

            return new LaunchArguments
            {
                SkipUpdates = SkipUpdatesCheckBox.IsChecked ?? false,
                SkipValidation = SkipValidationCheckBox.IsChecked ?? false,
                ValidateAll = ValidateAllCheckBox.IsChecked ?? false,
                DisableRPC = DisableRPCCheckBox.IsChecked ?? false,
                DebugMode = DebugModeCheckBox.IsChecked ?? false,
                CustomArguments = CustomArgumentsTextBox.Text?.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
                GameExecutable = GameExecutableTextBox.Text?.Trim() ?? "csgo.exe"
            };
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isOperationInProgress) return;
            
            _isOperationInProgress = true;
            LaunchButton.IsEnabled = false;
            ValidateButton.IsEnabled = false;
            
            SaveSettings();
            
            try
            {
                await _launcherLogic.LaunchGameAsync(GetLaunchArguments());
                
                // If we reach here, the game launched successfully
                if (!(DisableRPCCheckBox.IsChecked ?? false))
                {
                    // Minimize to system tray for Discord RPC
                    this.WindowState = WindowState.Minimized;
                    AddStatusMessage("Game launched! Launcher minimized for Discord RPC.", "#90EE90");
                }
                else
                {
                    // Close the launcher
                    AddStatusMessage("Game launched successfully! Closing launcher in 5 seconds.", "#90EE90");
                    await Task.Delay(5000);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                AddStatusMessage($"Failed to launch game: {ex.Message}", "#FF6B6B");
            }
            finally
            {
                _isOperationInProgress = false;
                LaunchButton.IsEnabled = true;
                ValidateButton.IsEnabled = true;
            }
        }

        private async void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isOperationInProgress) return;
            
            _isOperationInProgress = true;
            LaunchButton.IsEnabled = false;
            ValidateButton.IsEnabled = false;
            
            try
            {
                await _launcherLogic.ValidateFilesAsync(GetLaunchArguments());
            }
            catch (Exception ex)
            {
                AddStatusMessage($"Validation failed: {ex.Message}", "#FF6B6B");
            }
        }

        private void LoadSettings()
        {
            // Load settings from a simple config file or registry
            // For now, we'll use default values
        }

        private void SaveSettings()
        {
            // Save current settings
            // This could be implemented to persist user preferences
        }

        private void BrowseGameExecutable_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Game Executable",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                InitialDirectory = Directory.GetCurrentDirectory(),
                FileName = GameExecutableTextBox.Text
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Store relative path if it's in the current directory or subdirectory
                string currentDir = Directory.GetCurrentDirectory();
                string selectedPath = openFileDialog.FileName;
                
                if (selectedPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase))
                {
                    GameExecutableTextBox.Text = Path.GetRelativePath(currentDir, selectedPath);
                }
                else
                {
                    GameExecutableTextBox.Text = selectedPath;
                }
                
                AddStatusMessage($"Game executable set to: {GameExecutableTextBox.Text}", "#90EE90");
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.ToString(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AddStatusMessage($"Failed to open link: {ex.Message}", "#FF6B6B");
            }
        }



        private void BrowseExecutableButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Game Executable",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                InitialDirectory = Directory.GetCurrentDirectory()
            };

            if (openFileDialog.ShowDialog() == true)
            {
                GameExecutableTextBox.Text = openFileDialog.FileName;
                AddStatusMessage($"Game executable set to: {openFileDialog.FileName}", "#90EE90");
            }
        }

        public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T correctlyTyped)
                    return correctlyTyped;

                var foundChild = FindVisualChild<T>(child);
                if (foundChild != null)
                    return foundChild;
            }
            return null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class StatusMessage
    {
        public string Message { get; set; } = "";
        public SolidColorBrush Color { get; set; } = Brushes.White;
    }

    public class LaunchArguments
    {
        public bool SkipUpdates { get; set; }
        public bool SkipValidation { get; set; }
        public bool ValidateAll { get; set; }
        public bool DisableRPC { get; set; }
        public bool DebugMode { get; set; }
        public string[] CustomArguments { get; set; } = Array.Empty<string>();
        public string GameExecutable { get; set; } = "csgo.exe";
    }

    public class ProgressEventArgs : EventArgs
    {
        public double Percentage { get; set; }
        public string Status { get; set; } = "";
    }
}
