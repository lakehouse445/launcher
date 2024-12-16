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

                if (!File.Exists(launcherDllPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"7z.dll not found, downloading...");

                    await _downloader.DownloadFileTaskAsync(
                        "https://ollumhd.github.io/7z.dll",
                        launcherDllPath
                    );

                    if (Debug.Enabled())
                        Terminal.Debug($"Downloaded 7z.dll to: {launcherDllPath}");
                }
                else if (Debug.Enabled())
                {
                    Terminal.Debug($"Using existing 7z.dll from: {launcherDllPath}");
                }

                using (var archiveFile = new ArchiveFile(archivePath, launcherDllPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Starting extraction...");

                    await Task.Run(() => archiveFile.Extract(Path.GetDirectoryName(outputPath)));

                    if (Debug.Enabled())
                        Terminal.Debug("Extraction completed successfully!");

                    // you will likely be downloading new .vpk files
                    // rebuild audio cache for the audio to not earrape you incase we replaced any sounds in them
                    Argument.AddArgument("+snd_rebuildaudiocache");
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
                if (Debug.Enabled())
                    Terminal.Debug($"Extraction failed: {ex.Message}\nStack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
