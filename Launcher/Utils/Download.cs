using Downloader;
using SevenZipExtractor;

namespace Launcher.Utils
{
    public static class DownloadManager
    {
        private static DownloadConfiguration _settings = new()
        {
            ChunkCount = 4,
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

        public static async Task DownloadPatch(Patch patch)
        {
            string originalFileName = patch.File.EndsWith(".7z") ? patch.File[..^3] : patch.File;
            string downloadPath = $"{Directory.GetCurrentDirectory()}/{patch.File}";

            if (Debug.Enabled())
                Terminal.Debug($"Starting download of: {patch.File}");

            // if you found a compressed 7z of the file that ur trying to download already, delete it (probably cancelled partial download, its junk)
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

            await _downloader.DownloadFileTaskAsync(
                $"https://patch.classiccounter.cc/{patch.File}",
                $"{Directory.GetCurrentDirectory()}/{patch.File}"
            );

            if (patch.File.EndsWith(".7z"))
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Download complete, starting extraction of: {patch.File}");
                string extractPath = $"{Directory.GetCurrentDirectory()}/{originalFileName}";
                await Extract7z(downloadPath, extractPath);
            }
        }

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
    }
}
