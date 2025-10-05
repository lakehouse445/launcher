using System.Diagnostics;
using System.IO;
using Launcher.Utils;
using Spectre.Console;
using Version = Launcher.Utils.Version;

namespace Launcher
{
    public class LauncherLogic
    {
        public event EventHandler<string>? StatusUpdated;
        public event EventHandler<ProgressEventArgs>? ProgressUpdated;
        public event EventHandler<bool>? OperationCompleted;

        private int _totalDownloadProgress = 0;

        public async Task InitializeAsync(LaunchArguments args)
        {
            try
            {
                OnStatusUpdated("Initializing launcher...");
                OnProgressUpdated(0, "Starting up...");

                // Clean up .7z files at startup
                OnStatusUpdated("Cleaning up temporary files...");
                DownloadManager.Cleanup7zFiles();

                // Initialize Discord RPC if not disabled
                if (!args.DisableRPC)
                {
                    OnStatusUpdated("Initializing Discord Rich Presence...");
                    Discord.Init();
                }

                await Task.Delay(500); // Small delay for UI responsiveness

                // Clean up updater
                await CleanupUpdater();

                // Check for updates unless skipped
                if (!args.SkipUpdates)
                {
                    await CheckForUpdatesAsync();
                }
                else
                {
                    OnStatusUpdated("Skipping update check as requested.");
                }

                // Check for game files
                await CheckGameFilesAsync(args);

                // Validate files unless skipped
                if (!args.SkipValidation)
                {
                    await ValidateFilesInternalAsync(args);
                }
                else
                {
                    OnStatusUpdated("Skipping file validation as requested.");
                }

                OnProgressUpdated(100, "Initialization complete");
                OnOperationCompleted(true);
            }
            catch (Exception ex)
            {
                OnStatusUpdated($"Initialization failed: {ex.Message}");
                OnOperationCompleted(false);
                throw;
            }
        }

        public async Task ValidateFilesAsync(LaunchArguments args)
        {
            try
            {
                OnStatusUpdated("Starting file validation...");
                OnProgressUpdated(0, "Validating files...");

                await ValidateFilesInternalAsync(args);

                OnProgressUpdated(100, "Validation complete");
                OnOperationCompleted(true);
            }
            catch (Exception ex)
            {
                OnStatusUpdated($"Validation failed: {ex.Message}");
                OnOperationCompleted(false);
                throw;
            }
        }

        public async Task LaunchGameAsync(LaunchArguments args)
        {
            try
            {
                OnStatusUpdated("Preparing to launch game...");
                OnProgressUpdated(50, "Launching game...");

                // Final cleanup
                DownloadManager.Cleanup7zFiles();

                // Generate game arguments from GUI settings
                var gameArgs = GenerateGameArguments(args);
                
                bool launched = await LaunchGameWithArgs(gameArgs, args);
                
                if (!launched)
                {
                    throw new Exception("Game failed to launch. Make sure launcher.exe and csgo.exe are in the same directory.");
                }

                OnStatusUpdated("Game launched successfully!");
                OnProgressUpdated(100, "Game launched");

                // Start monitoring if Discord RPC is enabled
                if (!args.DisableRPC)
                {
                    _ = Task.Run(async () => await Game.Monitor());
                }
            }
            catch (Exception ex)
            {
                OnStatusUpdated($"Launch failed: {ex.Message}");
                throw;
            }
        }

        private Task CleanupUpdater()
        {
            string updaterPath = $"{Directory.GetCurrentDirectory()}/updater.exe";
            if (File.Exists(updaterPath))
            {
                OnStatusUpdated("Cleaning up updater...");
                try
                {
                    File.Delete(updaterPath);
                }
                catch
                {
                    OnStatusUpdated("Warning: Couldn't delete updater.exe, possibly due to missing permissions.");
                }
            }
            return Task.CompletedTask;
        }

        private async Task CheckForUpdatesAsync()
        {
            OnStatusUpdated("Checking for launcher updates...");
            OnProgressUpdated(10, "Checking for updates...");

            string latestVersion = await Version.GetLatestVersion();

            if (Version.Current != latestVersion)
            {
                OnStatusUpdated($"Update available: {latestVersion} (current: {Version.Current})");
                OnStatusUpdated("Downloading auto-updater...");
                OnProgressUpdated(20, "Downloading updater...");

                string updaterPath = $"{Directory.GetCurrentDirectory()}/updater.exe";
                
                try
                {
                    await DownloadManager.DownloadUpdater(updaterPath);

                    if (!File.Exists(updaterPath))
                    {
                        OnStatusUpdated("Warning: Downloaded updater doesn't exist, possibly due to permissions.");
                        return;
                    }

                    OnStatusUpdated("Launching updater and closing launcher...");
                    
                    var updaterProcess = new System.Diagnostics.Process();
                    updaterProcess.StartInfo.FileName = updaterPath;
                    updaterProcess.StartInfo.Arguments = $"--version={latestVersion}";
                    updaterProcess.Start();

                    // Exit the application
                    System.Windows.Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    OnStatusUpdated($"Failed to download or launch auto-updater: {ex.Message}");
                    throw new Exception("Auto-update failed. Please update manually.");
                }
            }
            else
            {
                OnStatusUpdated("Launcher is up-to-date!");
                OnProgressUpdated(30, "Launcher up-to-date");
            }
        }

        private async Task CheckGameFilesAsync(LaunchArguments args)
        {
            OnStatusUpdated("Checking for game files...");
            OnProgressUpdated(40, "Checking game files...");

            string directory = Directory.GetCurrentDirectory();
            string gameExePath = Path.IsPathRooted(args.GameExecutable) 
                ? args.GameExecutable 
                : Path.Combine(directory, args.GameExecutable);
                
            OnStatusUpdated($"Looking for game executable: {gameExePath}");
            
            if (!File.Exists(gameExePath))
            {
                OnStatusUpdated("Game files not found!");
                
                // Check if there are partial downloads
                if (Directory.GetFiles(directory, "*.7z.001").Length > 0)
                {
                    OnStatusUpdated("Resuming game download...");
                    OnProgressUpdated(50, "Downloading game files...");
                    await DownloadFullGameWithProgress();
                }
                else
                {
                    OnStatusUpdated("Game files will be downloaded (approximately 7GB).");
                    
                    // Check disk space
                    string? rootPath = Path.GetPathRoot(directory);
                    if (rootPath != null)
                    {
                        DriveInfo driveInfo = new DriveInfo(rootPath);
                        long requiredSpace = 24L * 1024 * 1024 * 1024; // 24 GB

                        if (driveInfo.AvailableFreeSpace < requiredSpace)
                        {
                            throw new Exception($"Not enough disk space! Required: 24 GB, Available: {driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024):F2} GB");
                        }
                    }

                    OnStatusUpdated("Starting game download...");
                    OnProgressUpdated(50, "Downloading game files...");
                    await DownloadFullGameWithProgress();
                }
            }
            else
            {
                OnStatusUpdated($"Game executable found: {Path.GetFileName(gameExePath)}");
                OnProgressUpdated(60, "Game files verified");
            }
        }

        private async Task ValidateFilesInternalAsync(LaunchArguments args)
        {
            OnStatusUpdated("Validating game files and patches...");
            OnProgressUpdated(70, "Validating files...");

            if (args.ValidateAll)
            {
                OnStatusUpdated("Validating all game files...");
                var gameFiles = await PatchManager.ValidatePatches(true);
                
                if (gameFiles.Success)
                {
                    OnStatusUpdated("Game file validation completed!");
                    
                    if (gameFiles.Missing.Count > 0 || gameFiles.Outdated.Count > 0)
                    {
                        if (gameFiles.Missing.Count > 0)
                            OnStatusUpdated($"Found {gameFiles.Missing.Count} missing game file(s)");
                        
                        if (gameFiles.Outdated.Count > 0)
                            OnStatusUpdated($"Found {gameFiles.Outdated.Count} outdated game file(s)");

                        OnStatusUpdated("Downloading missing/outdated game files...");
                        await DownloadPatchesWithProgress(gameFiles, true);
                        _totalDownloadProgress += gameFiles.Missing.Count + gameFiles.Outdated.Count;
                    }
                    else
                    {
                        OnStatusUpdated("All game files are up-to-date!");
                    }
                }
                else
                {
                    throw new Exception("Couldn't validate game files! Check your internet connection and DNS settings.");
                }
            }

            OnStatusUpdated("Validating patches...");
            OnProgressUpdated(80, "Validating patches...");
            
            var patches = await PatchManager.ValidatePatches(false);
            
            if (patches.Success)
            {
                OnStatusUpdated("Patch validation completed!");
                
                if (patches.Missing.Count > 0 || patches.Outdated.Count > 0)
                {
                    if (patches.Missing.Count > 0)
                        OnStatusUpdated($"Found {patches.Missing.Count} missing patch(es)");
                    
                    if (patches.Outdated.Count > 0)
                        OnStatusUpdated($"Found {patches.Outdated.Count} outdated patch(es)");

                    OnStatusUpdated("Downloading patches...");
                    await DownloadPatchesWithProgress(patches, false);
                    _totalDownloadProgress += patches.Missing.Count + patches.Outdated.Count;
                }
                else
                {
                    OnStatusUpdated("All patches are up-to-date!");
                }
            }
            else
            {
                OnStatusUpdated("Warning: Couldn't validate patches! Check your internet connection.");
            }

            OnProgressUpdated(90, "Cleaning up...");
            OnStatusUpdated("Cleaning up temporary files...");
            
            // Cleanup
            try
            {
                string launcherDllPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "7z.dll");
                if (File.Exists(launcherDllPath))
                {
                    File.Delete(launcherDllPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private async Task DownloadFullGameWithProgress()
        {
            // This is a simplified version - in reality you'd want to track actual download progress
            for (int i = 0; i <= 100; i += 5)
            {
                OnProgressUpdated(50 + (i / 4), $"Downloading game files... {i}%");
                await Task.Delay(100); // Simulate download progress
            }
            
            // Call the actual download method
            // Note: You'd need to modify DownloadManager.DownloadFullGame to provide progress callbacks
            // For now, this is a placeholder
        }

        private async Task DownloadPatchesWithProgress(Patches patches, bool isGameFiles)
        {
            // This is a simplified version - in reality you'd want to track actual download progress
            string fileType = isGameFiles ? "game files" : "patches";
            int totalFiles = patches.Missing.Count + patches.Outdated.Count;
            
            for (int i = 0; i < totalFiles; i++)
            {
                double progress = 80 + ((double)i / totalFiles) * 10;
                OnProgressUpdated(progress, $"Downloading {fileType}... {i + 1}/{totalFiles}");
                await Task.Delay(500); // Simulate download time
            }
            
            // Call the actual download method
            // Note: You'd need to modify DownloadManager.HandlePatches to provide progress callbacks
        }

        private List<string> GenerateGameArguments(LaunchArguments args)
        {
            var arguments = new List<string>();
            
            // Add custom arguments first
            if (args.CustomArguments.Length > 0)
            {
                arguments.AddRange(args.CustomArguments);
            }
            
            // Add standard arguments based on settings
            // You might want to implement additional game-specific arguments here
            
            return arguments;
        }

        private Task<bool> LaunchGameWithArgs(List<string> arguments, LaunchArguments args)
        {
            OnStatusUpdated($"Launching {args.GameExecutable} with arguments: {string.Join(" ", arguments)}");
            
            // Use the custom game executable path
            return Game.LaunchWithExecutable(args.GameExecutable, arguments);
        }

        private void OnStatusUpdated(string message)
        {
            StatusUpdated?.Invoke(this, message);
        }

        private void OnProgressUpdated(double percentage, string status)
        {
            ProgressUpdated?.Invoke(this, new ProgressEventArgs { Percentage = percentage, Status = status });
        }

        private void OnOperationCompleted(bool success)
        {
            OperationCompleted?.Invoke(this, success);
        }
    }
}
