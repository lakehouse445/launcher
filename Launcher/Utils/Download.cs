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

            await _downloader.DownloadFileTaskAsync(
                $"https://patch.classiccounter.cc/{patch.File}",
                $"{Directory.GetCurrentDirectory()}/{patch.File}"
            );

            if (patch.File.EndsWith(".7z"))
            {
                string extractPath = $"{Directory.GetCurrentDirectory()}/{originalFileName}";
                await Extract7z(downloadPath, extractPath);
            }
        }

        private static async Task Extract7z(string archivePath, string outputPath)
        {
            using (var archiveFile = new ArchiveFile(archivePath))
            {
                await Task.Run(() => archiveFile.Extract(Path.GetDirectoryName(outputPath)));
            }
            // Delete the .7z file after extraction
            File.Delete(archivePath);
        }
    }
}
