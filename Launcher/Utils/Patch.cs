using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace Launcher.Utils
{
    public class Patch
    {
        [JsonProperty(PropertyName = "file")]
        public required string File { get; set; }

        [JsonProperty(PropertyName = "hash")]
        public required string Hash { get; set; }
    };

    public class Patches(bool success, List<Patch> missing, List<Patch> outdated)
    {
        public bool Success = success;
        public List<Patch> Missing = missing;
        public List<Patch> Outdated = outdated;
    }

    public static class PatchManager
    {
        private static async Task<List<Patch>> GetPatches()
        {
            List<Patch> patches = new List<Patch>();

            try
            {
                string responseString = await Api.ClassicCounter.GetPatches();
                JObject responseJson = JObject.Parse(responseString);

                if (responseJson["files"] != null)
                    patches = responseJson["files"]!.ToObject<Patch[]>()!.ToList();
            }
            catch
            {
                if (Debug.Enabled())
                    Terminal.Debug("Couldn't get patches.");
            }

            return patches;
        }

        private static async Task<string> GetHash(string filePath)
        {
            MD5 md5 = MD5.Create();

            byte[] buffer = await File.ReadAllBytesAsync(filePath);
            byte[] hash = md5.ComputeHash(buffer, 0, buffer.Length);

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static async Task<Patches> ValidatePatches()
        {
            List<Patch> patches = await GetPatches();

            List<Patch> missing = new();
            List<Patch> outdated = new();

            foreach (Patch patch in patches)
            {
                string path = $"{Directory.GetCurrentDirectory()}/{patch.File}";
                string serverHash = patch.Hash;

                if (!File.Exists(path))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Missing file: {patch.File}");

                    missing.Add(patch);
                    continue;
                }
                else
                {
                    try
                    {
                        string clientHash = await GetHash(path);
                        if (clientHash != serverHash)
                        {
                            if (Debug.Enabled())
                                Terminal.Debug($"Outdated file: {patch.File}");

                            outdated.Add(patch);
                        }
                    }
                    catch { continue; }
                }
            }

            return new Patches(patches.Count > 0, missing, outdated);
        }
    }
}
