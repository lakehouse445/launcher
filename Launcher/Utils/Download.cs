using Downloader;
using SevenZipExtractor;
using Spectre.Console;

namespace Launcher.Utils
{
    public static class DownloadManager
    {
        private static DownloadConfiguration _settings = new()
        {
            ChunkCount = 8,
            ParallelDownload = true
        };
        private static DownloadService _downloader = new DownloadService(_settings);

        public static async Task DownloadUpdater(string path)
        {
            await _downloader.DownloadFileTaskAsync(
                $"https://github.com/ClassicCounter/updater/releases/download/updater/updater.exe",
                path
            );
        }

        public static async Task DownloadPatch(Patch patch, bool validateAll = false, Action<DownloadProgressChangedEventArgs>? onProgress = null, Action? onExtract = null)
        {
            string originalFileName = patch.File.EndsWith(".7z") ? patch.File[..^3] : patch.File;
            string downloadPath = $"{Directory.GetCurrentDirectory()}/{patch.File}";

            if (Debug.Enabled())
                Terminal.Debug($"Starting download of: {patch.File}");

            if (patch.File.EndsWith(".7z") && File.Exists(downloadPath))
            {
                try
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Found existing .7z file, trying to delete: {downloadPath}");
                    File.Delete(downloadPath);
                }
                catch (Exception ex)
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Failed to delete existing .7z file: {ex.Message}");
                }
            }

            if (onProgress != null)
            {
                _downloader.DownloadProgressChanged += (sender, e) => onProgress(e);
            }

            string baseUrl = validateAll ? "https://misc.ollum.cc/ClassicCounter" : "https://patch.classiccounter.cc";

            await _downloader.DownloadFileTaskAsync(
                $"{baseUrl}/{patch.File}",
                $"{Directory.GetCurrentDirectory()}/{patch.File}"
            );

            if (patch.File.EndsWith(".7z"))
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Download complete, starting extraction of: {patch.File}");
                onExtract?.Invoke(); // for "extracting" status
                string extractPath = $"{Directory.GetCurrentDirectory()}/{originalFileName}";
                await Extract7z(downloadPath, extractPath);
            }
        }

        // FOR DOWNLOAD STATUS
        public static int dotCount = 0;
        public static DateTime lastDotUpdate = DateTime.Now;
        public static string GetDots()
        {
            if ((DateTime.Now - lastDotUpdate).TotalMilliseconds > 500)
            {
                dotCount = (dotCount + 1) % 4;
                lastDotUpdate = DateTime.Now;
            }
            return "...".Substring(0, dotCount);
        }
        public static string GetProgressBar(double percentage)
        {
            int blocks = 16;
            int level = (int)(percentage / (100.0 / (blocks * 3)));
            string bar = "";

            for (int i = 0; i < blocks; i++)
            {
                int blockLevel = Math.Min(3, Math.Max(0, level - (i * 3)));
                bar += blockLevel switch
                {
                    0 => "░",
                    1 => "▒",
                    2 => "▓",
                    3 => "█",
                    _ => "█"
                };
            }
            return bar;
        }
        // DOWNLOAD STATUS OVER



        // moved !--skip-validating stuff into this little helper function
        public static async Task HandlePatches(Patches patches, StatusContext ctx, bool isGameFiles, int startingProgress = 0)
        {
            string fileType = isGameFiles ? "game file" : "patch";
            string fileTypePlural = isGameFiles ? "game files" : "patches";

            var allFiles = patches.Missing.Concat(patches.Outdated).ToList();
            int totalFiles = allFiles.Count;
            int completedFiles = startingProgress;
            int failedFiles = 0;

            // status update
            Action<DownloadProgressChangedEventArgs, string> updateStatus = (progress, filename) =>
            {
                var speed = progress.BytesPerSecondSpeed / (1024.0 * 1024.0);
                var progressText = $"{((float)completedFiles / totalFiles * 100):F1}% ({completedFiles}/{totalFiles})";
                var status = filename.EndsWith(".7z") && progress.ProgressPercentage >= 100 ? "Extracting" : "Downloading new";
                ctx.Status = $"{status} {fileTypePlural}{GetDots().PadRight(3)} [gray]|[/] {progressText} [gray]|[/] {GetProgressBar(progress.ProgressPercentage)} {progress.ProgressPercentage:F1}% [gray]|[/] {speed:F1} MB/s";
            };

            foreach (var patch in allFiles)
            {
                try
                {
                    await DownloadPatch(patch, isGameFiles, (progress) => updateStatus(progress, patch.File));
                    completedFiles++;
                }
                catch
                {
                    failedFiles++;
                    Terminal.Warning($"Couldn't process {fileType}: {patch.File}, possibly due to missing permissions.");
                }
            }

            if (failedFiles > 0)
                Terminal.Warning($"Couldn't download {failedFiles} {(failedFiles == 1 ? fileType : fileTypePlural)}!");
        }

        // handle files in parallel
        /*private static int GetBatchSize(bool isGameFiles, List<Patch> files)
        {
            if (!isGameFiles) return 1;  // default for patches

            // for game file dl, check first few files to determine batch size
            try
            {
                var sampleFiles = files.Take(5);
                long avgSize = 0;
                int count = 0;
                foreach (var file in sampleFiles)
                {
                    string filePath = $"{Directory.GetCurrentDirectory()}/{file.File}";
                    if (File.Exists(filePath))
                    {
                        avgSize += new FileInfo(filePath).Length;
                        count++;
                    }
                }
                if (count > 0)
                {
                    avgSize /= count;
                    if (avgSize < 10240) return 50;       // < 10KB
                    if (avgSize < 102400) return 25;      // < 100KB
                    if (avgSize < 1048576) return 10;     // < 1MB
                    return 2;                             // larger files
                }
            }
            catch
            {
                // if anything goes wrong, use default
                return 1;
            }
            return 2;  // default for game files
        }*/

        private static async Task Extract7z(string archivePath, string outputPath)
        {
            try
            {
                if (!File.Exists(archivePath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Archive file not found: {archivePath}");
                    return;
                }

                string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (launcherDir == null)
                {
                    throw new InvalidOperationException("Could not determine launcher directory");
                }

                string launcherDllPath = Path.Combine(launcherDir, "7z.dll");
                bool dllDownloaded = false;
                int retryCount = 0;
                string[] fallbackUrls = new[]
                {
                    "https://ollumhd.github.io/7z.dll",
                    "https://fastdl.classiccounter.cc/7z.dll"
                    // add more fallback URLs if needed...
                };

                while (!dllDownloaded && retryCount < 10)
                {
                    if (!File.Exists(launcherDllPath))
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"7z.dll not found, downloading... (Attempt {retryCount + 1}/10)");

                        try
                        {
                            await _downloader.DownloadFileTaskAsync(
                                fallbackUrls[retryCount % fallbackUrls.Length],
                                launcherDllPath
                            );

                            if (File.Exists(launcherDllPath))
                            {
                                dllDownloaded = true;
                                if (Debug.Enabled())
                                    Terminal.Debug($"Downloaded 7z.dll to: {launcherDllPath}");
                            }
                            else
                            {
                                Terminal.Error($"Failed to download 7z.dll! Trying again... (Attempt {retryCount + 1})");
                                retryCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (Debug.Enabled())
                                Terminal.Debug($"Failed to download 7z.dll: {ex.Message}");
                            retryCount++;
                        }

                        if (retryCount > 0)
                            await Task.Delay(1000); // wait 1 sec per retry
                    }
                    else
                    {
                        dllDownloaded = true;
                    }
                }

                if (!dllDownloaded)
                {
                    Terminal.Error("Couldn't download 7z.dll! Launcher will close in 5 seconds...");
                    await Task.Delay(5000);
                    Environment.Exit(1);
                }

                using (var archiveFile = new ArchiveFile(archivePath, launcherDllPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Starting extraction...");

                    await Task.Run(() => archiveFile.Extract(Path.GetDirectoryName(outputPath)));

                    if (Debug.Enabled())
                        Terminal.Debug("Extraction completed successfully!");

                    Argument.AddArgument("+snd_mixahead 0.1");
                }

                // delete 7z after extract
                try
                {
                    File.Delete(archivePath);
                    if (Debug.Enabled())
                        Terminal.Debug($"Deleted archive file: {archivePath}");
                }
                catch (Exception ex)
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Failed to delete archive file: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Terminal.Error($"Extraction failed: {ex.Message}\nStack trace: {ex.StackTrace}");
                throw;
            }
        }

        public static void Cleanup7zFiles()
        {
            try
            {
                string directory = Directory.GetCurrentDirectory();
                var files = Directory.GetFiles(directory, "*.7z", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    try
                    {
                        File.Delete(file);
                        if (Debug.Enabled())
                            Terminal.Debug($"Deleted .7z file: {file}");
                    }
                    catch (Exception ex)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Failed to delete .7z file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Failed to perform .7z cleanup: {ex.Message}");
            }
        }
    }
}
