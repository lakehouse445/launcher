using Downloader;

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
            await _downloader.DownloadFileTaskAsync(
                $"https://patch.classiccounter.cc/{patch.File}",
                $"{Directory.GetCurrentDirectory()}/{patch.File}"
            );
        }
    }
}
