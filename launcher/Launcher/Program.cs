﻿using System;
using System.IO;
using System.Threading.Tasks;
using Launcher.Utils;
using Spectre.Console;
using Version = Launcher.Utils.Version;

namespace Launcher
{
    public static class ConsoleProgram
    {
        public static async Task RunConsoleMode(string[] args)
        {
            try
            {
                Console.Clear();
                
                AnsiConsole.MarkupLine("[orange1]Classic[/][blue]Counter[/] [grey50]|[/] [grey82]Console Mode[/]");
                AnsiConsole.MarkupLine("[grey82]GUI by lakehouse[/]");
                AnsiConsole.WriteLine();

                if (Debug.Enabled())
                    Terminal.Debug("Cleaning up any .7z files at startup...");
                DownloadManager.Cleanup7zFiles();

                if (!Argument.Exists("--disable-rpc"))
                    Discord.Init();

                Terminal.Init();

                await Task.Delay(1000);

                int totalDownloadProgress = 0;

                // Clean up updater
                string updaterPath = $"{Directory.GetCurrentDirectory()}/updater.exe";
                if (File.Exists(updaterPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug("Found and deleting: updater.exe");

                    try
                    {
                        File.Delete(updaterPath);
                    }
                    catch (Exception ex)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Couldn't delete updater.exe: {ex.Message}");
                    }
                }

                // Check for updates
                if (!Argument.Exists("--skip-updates"))
                {
                    try
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
                                            Terminal.Error("Downloaded updater doesn't exist, possibly due to missing permissions.");
                                            return;
                                        }

                                        var updaterProcess = new System.Diagnostics.Process();
                                        updaterProcess.StartInfo.FileName = updaterPath;
                                        updaterProcess.StartInfo.Arguments = $"--version={latestVersion} {string.Join(" ", Argument.GenerateGameArguments(true))}";
                                        updaterProcess.Start();
                                        
                                        Environment.Exit(1);
                                    }
                                    catch (Exception ex)
                                    {
                                        Terminal.Error($"Couldn't download or launch auto-updater: {ex.Message}");
                                        await Task.Delay(5000);
                                    }
                                });
                        }
                        else
                        {
                            Terminal.Success("Launcher is up-to-date!");
                        }
                    }
                    catch (Exception ex)
                    {
                        Terminal.Error($"Failed to check for updates: {ex.Message}");
                        Terminal.Warning("Continuing without update check...");
                    }
                }

                // Check for game files
                string directory = Directory.GetCurrentDirectory();
                if (!File.Exists($"{directory}/csgo.exe"))
                {
                    Terminal.Error("(!) csgo.exe not found in the current directory!");
                    Terminal.Warning($"Game files will be installed to: {directory}");
                    Terminal.Warning("This will download approximately 7GB of data. Make sure you have enough disk space.");
                    
                    AnsiConsole.Markup($"[orange1]Classic[/][blue]Counter[/] [grey50]|[/] [grey82]Would you like to download the full game? (y/n): [/]");
                    var response = Console.ReadKey(true);
                    Console.WriteLine(response.KeyChar);
                    Console.WriteLine();

                    if (char.ToLower(response.KeyChar) == 'y')
                    {
                        try
                        {
                            // Check disk space
                            string? rootPath = Path.GetPathRoot(directory);
                            if (rootPath != null)
                            {
                                DriveInfo driveInfo = new DriveInfo(rootPath);
                                long requiredSpace = 24L * 1024 * 1024 * 1024; // 24 GB

                                if (driveInfo.AvailableFreeSpace < requiredSpace)
                                {
                                    Terminal.Error("(!) Not enough disk space available!");
                                    Terminal.Error($"Required: 24 GB, Available: {driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024):F2} GB");
                                    Terminal.Error("Please free up some disk space and try again. Closing launcher in 10 seconds...");
                                    await Task.Delay(10000);
                                    Environment.Exit(1);
                                    return;
                                }
                            }

                            await AnsiConsole
                                .Status()
                                .SpinnerStyle(Style.Parse("gray"))
                                .StartAsync("Downloading full game...", async ctx =>
                                {
                                    await DownloadManager.DownloadFullGame(ctx);
                                });
                        }
                        catch (Exception ex)
                        {
                            Terminal.Error($"Failed to download game files: {ex.Message}");
                            Terminal.Error("Closing launcher in 10 seconds...");
                            await Task.Delay(10000);
                            Environment.Exit(1);
                        }
                    }
                    else
                    {
                        Terminal.Error("Game files are required to run ClassicCounter. Closing launcher in 10 seconds...");
                        await Task.Delay(10000);
                        Environment.Exit(1);
                    }
                }

                // File validation
                if (!Argument.Exists("--skip-validating"))
                {
                    try
                    {
                        await AnsiConsole
                            .Status()
                            .SpinnerStyle(Style.Parse("gray"))
                            .StartAsync("Validating files...", async ctx =>
                            {
                                await ValidateFilesAsync(ctx, totalDownloadProgress);
                            });
                    }
                    catch (Exception ex)
                    {
                        Terminal.Error($"File validation failed: {ex.Message}");
                        if (!Argument.Exists("--patch-only"))
                        {
                            Terminal.Warning("Launching ClassicCounter anyways...");
                        }
                    }
                }

                // Patch-only mode
                if (Argument.Exists("--patch-only"))
                {
                    Terminal.Success("Finished patch validation and downloads! Closing launcher.");
                    await Task.Delay(3000);
                    Environment.Exit(0);
                    return;
                }

                // Final cleanup
                if (Debug.Enabled())
                    Terminal.Debug("Cleaning up any .7z files...");
                DownloadManager.Cleanup7zFiles();

                // Launch game
                try
                {
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
                }
                catch (Exception ex)
                {
                    Terminal.Error($"Failed to launch game: {ex.Message}");
                    Terminal.Error("Closing launcher in 10 seconds...");
                    await Task.Delay(10000);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Fatal error: {ex.Message}[/]");
                AnsiConsole.MarkupLine($"[grey]Stack trace: {ex.StackTrace}[/]");
                AnsiConsole.MarkupLine("[yellow]Press any key to exit...[/]");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        private static async Task ValidateFilesAsync(StatusContext ctx, int totalDownloadProgress)
        {
            bool validateAll = Argument.Exists("--validate-all");

            if (validateAll)
            {
                ctx.Status = "Validating game files...";
                Patches gameFiles = await PatchManager.ValidatePatches(true);
                if (gameFiles.Success)
                {
                    Terminal.Print("Finished validating game files!");
                    if (gameFiles.Missing.Count > 0 || gameFiles.Outdated.Count > 0)
                    {
                        if (gameFiles.Missing.Count > 0)
                            Terminal.Warning($"Found {gameFiles.Missing.Count} missing {(gameFiles.Missing.Count == 1 ? "game file" : "game files")}!");

                        if (gameFiles.Outdated.Count > 0)
                            Terminal.Warning($"Found {gameFiles.Outdated.Count} outdated {(gameFiles.Outdated.Count == 1 ? "game file" : "game files")}!");

                        Terminal.Print("If you're stuck at downloading - reopen the launcher.");

                        await DownloadManager.HandlePatches(gameFiles, ctx, true, totalDownloadProgress);
                        totalDownloadProgress += gameFiles.Missing.Count + gameFiles.Outdated.Count;
                    }
                    else
                    {
                        Terminal.Success("Game files are up-to-date!");
                    }
                }
                else
                {
                    throw new Exception("Couldn't validate game files! Is your ISP blocking CloudFlare? Check your DNS settings.");
                }

                ctx.Status = "Validating patches...";
                Terminal.Print("\nNow checking for new patches...");
            }

            // Regular patch validation
            Patches patches = await PatchManager.ValidatePatches(false);
            if (patches.Success)
            {
                Terminal.Print("Finished validating patches!");
                if (patches.Missing.Count == 0 && patches.Outdated.Count == 0)
                {
                    Terminal.Success("Patches are up-to-date!");
                    return;
                }
            }
            else
            {
                Terminal.Error("(!) Couldn't validate patches!");
                Terminal.Error("(!) Is your ISP blocking CloudFlare? Check your DNS settings.");
                if (!Argument.Exists("--patch-only"))
                {
                    Terminal.Warning("Launching ClassicCounter anyways...");
                }
                return;
            }

            if (patches.Missing.Count > 0)
                Terminal.Warning($"Found {patches.Missing.Count} missing {(patches.Missing.Count == 1 ? "patch" : "patches")}!");

            if (patches.Outdated.Count > 0)
                Terminal.Warning($"Found {patches.Outdated.Count} outdated {(patches.Outdated.Count == 1 ? "patch" : "patches")}!");

            Terminal.Print("If you're stuck at downloading patches - reopen the launcher.");

            await DownloadManager.HandlePatches(patches, ctx, false, totalDownloadProgress);
            totalDownloadProgress += patches.Missing.Count + patches.Outdated.Count;

            // Cleanup temporary files
            if (Debug.Enabled())
                Terminal.Debug("Cleaning up temporary files...");

            try
            {
                string launcherDllPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "7z.dll");
                if (File.Exists(launcherDllPath))
                {
                    File.Delete(launcherDllPath);
                    if (Debug.Enabled())
                        Terminal.Debug($"Deleted 7z.dll: {launcherDllPath}");
                }
            }
            catch (Exception ex)
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Cleanup failed: {ex.Message}");
            }
        }
    }
}