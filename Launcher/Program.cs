using Launcher.Utils;
using Spectre.Console;
using System.Diagnostics;

using Debug = Launcher.Utils.Debug;
using Version = Launcher.Utils.Version;

Console.Clear();

if (!Argument.Exists("--disable-rpc"))
    Discord.Init();

Terminal.Init();

await Task.Delay(1000);

string updaterPath = $"{Directory.GetCurrentDirectory()}/updater.exe";
if (File.Exists(updaterPath))
{
    if (Debug.Enabled())
        Terminal.Debug("Found and deleting: updater.exe");

    try
    {
        File.Delete(updaterPath);
    }
    catch
    {
        if (Debug.Enabled())
            Terminal.Debug("Couldn't delete updater.exe, possibly due to missing permissions.");
    }
}

if (!Argument.Exists("--skip-updates"))
{
    string latestVersion = await Version.GetLatestVersion();

    if (Version.Current != latestVersion)
    {
        Terminal.Print("You're using an outdated launcher - updating...");
        await AnsiConsole
            .Status()
            .SpinnerStyle(Style.Parse("gray"))
            .StartAsync("Downloading auto-updater.", async ctx =>
        {
            try
            {
                await DownloadManager.DownloadUpdater(updaterPath);

                if (!File.Exists(updaterPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug("Updater.exe that was just downloaded doesn't exist, possibly due to missing permissions.");

                    return;
                }

                Process updaterProcess = new Process();
                updaterProcess.StartInfo.FileName = updaterPath;
                updaterProcess.StartInfo.Arguments = $"--version={latestVersion} {string.Join(" ", Argument.GenerateGameArguments(true))}";
                updaterProcess.Start();
            }
            catch
            {
                Terminal.Error("Couldn't download or launch auto-updater. Closing launcher in 5 seconds.");
                await Task.Delay(5000);
            }

            Environment.Exit(1);
        });
    }
    else
        Terminal.Success("Launcher is up-to-date!");
}

if (!Argument.Exists("--skip-validating"))
{
    await AnsiConsole
    .Status()
    .SpinnerStyle(Style.Parse("gray"))
    .StartAsync("Validating patches.", async ctx =>
    {
        Patches patches = await PatchManager.ValidatePatches();

        if (patches.Success)
            Terminal.Print("Finished validating patches.");
        else
            Terminal.Error("Couldn't validate patches.");

        if (patches.Missing.Count == 0 && patches.Outdated.Count == 0)
        {
            Terminal.Success("Patches are up-to-date!");
            return;
        }

        if (patches.Missing.Count > 0)
            Terminal.Warning($"Found {patches.Missing.Count} missing {(patches.Missing.Count == 1 ? "patch" : "patches")}.");

        if (patches.Outdated.Count > 0)
            Terminal.Warning($"Found {patches.Outdated.Count} outdated {(patches.Outdated.Count == 1 ? "patch" : "patches")}.");

        Terminal.Print("If you're stuck at downloading patches - reopen the launcher.");

        if (patches.Missing.Count > 0)
        {
            int patchCount = patches.Missing.Count;
            int downloaded = 0;
            int notDownloaded = 0;

            foreach (Patch patch in patches.Missing)
            {
                ctx.Status = $"Downloading missing patches. | {downloaded} / {patchCount}";

                try
                {
                    await DownloadManager.DownloadPatch(patch);
                    downloaded++;
                }
                catch
                {
                    patchCount--;
                    notDownloaded++;

                    if (Debug.Enabled())
                        Terminal.Debug($"Couldn't download missing patch: {patch.File}, possibly due to missing permissions.");
                }

                await Task.Delay(250);
            }

            if (notDownloaded > 0)
                Terminal.Warning($"Couldn't download {notDownloaded} missing patches.");
        }

        if (patches.Outdated.Count > 0)
        {
            int patchCount = patches.Outdated.Count;
            int downloaded = 0;
            int notDownloaded = 0;

            foreach (Patch patch in patches.Outdated)
            {
                ctx.Status = $"Downloading outdated patches. | {downloaded} / {patchCount}";

                try
                {
                    await DownloadManager.DownloadPatch(patch);
                    downloaded++;
                }
                catch
                {
                    patchCount--;
                    notDownloaded++;

                    if (Debug.Enabled())
                        Terminal.Debug($"Couldn't download outdated patch: {patch.File}, possibly due to missing permissions.");
                }

                await Task.Delay(250);
            }

            if (notDownloaded > 0)
                Terminal.Warning($"Couldn't download {notDownloaded} outdated patches.");
        }
    });
}

bool launched = await Game.Launch();
if (!launched)
{
    Terminal.Error("ClassicCounter didn't launch properly. Make sure launcher.exe and csgo.exe are in the same directory. Closing launcher in 10 seconds.");
    await Task.Delay(10000);
}
else if (Argument.Exists("--disable-rpc"))
{
    Terminal.Success("Launched ClassicCounter! Closing launcher in 5 seconds.");
    await Task.Delay(5000);
}
else
{ 
    Terminal.Success("Launched ClassicCounter! Launcher will minimize in 5 seconds to manage Discord RPC.");
    await Task.Delay(5000);

    ConsoleManager.HideConsole();
    Discord.SetDetails("In Main Menu");
    Discord.Update();
    await Game.Monitor();
}