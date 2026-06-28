using System;
using System.IO;
using System.Reflection;

namespace NarratorSvc
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string modFolder = ResolveModFolder(args);
            string gameFolder = ResolveGameFolder(args, modFolder);
            if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
            {
                Console.Error.WriteLine("Usage: NarratorSvc.exe [--game \"path\\to\\game\"] [--mod-folder BG2Narrator|PSTNarrator]");
                Console.Error.WriteLine("When launched from {GameFolder}\\BG2Narrator or \\PSTNarrator, paths are inferred automatically.");
                return 1;
            }

            const string logPrefix = "[NarratorSvc]";
            Console.WriteLine(logPrefix + " Game folder: " + gameFolder);
            Console.WriteLine(logPrefix + " Mod folder: " + modFolder);
            using (var context = new ServiceContext(gameFolder, modFolder))
            {
                Console.WriteLine(logPrefix + " Watching " + context.EventsPath);
                if (!string.IsNullOrWhiteSpace(context.BaldurLuaPath))
                {
                    Console.WriteLine(logPrefix + " Watching " + context.BaldurLuaPath + " (Baldur.lua IPC)");
                }
                Console.WriteLine(logPrefix + " Settings " + context.SettingsPath);
                Console.WriteLine(logPrefix + " Press Ctrl+C to exit.");

                var exit = new System.Threading.ManualResetEvent(false);
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    exit.Set();
                };

                exit.WaitOne();
            }

            return 0;
        }

        private static string ResolveGameFolder(string[] args, string modFolder)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--game", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            string env = Environment.GetEnvironmentVariable("BGNARRATOR_GAME");
            if (string.IsNullOrWhiteSpace(env))
            {
                env = Environment.GetEnvironmentVariable("PSTNARRATOR_GAME");
            }

            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }

            string inferredGameFolder;
            if (TryInferPathsFromExeLocation(out inferredGameFolder, out _))
            {
                return inferredGameFolder;
            }

            if (string.Equals(modFolder, "PSTNarrator", StringComparison.OrdinalIgnoreCase))
            {
                return @"D:\SteamLibrary\steamapps\common\Project P";
            }

            return @"D:\SteamLibrary\steamapps\common\Baldur's Gate II Enhanced Edition";
        }

        private static string ResolveModFolder(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--mod-folder", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            string env = Environment.GetEnvironmentVariable("BGNARRATOR_MOD_FOLDER");
            if (string.IsNullOrWhiteSpace(env))
            {
                env = Environment.GetEnvironmentVariable("PSTNARRATOR_MOD_FOLDER");
            }

            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }

            string inferredModFolder;
            if (TryInferPathsFromExeLocation(out _, out inferredModFolder))
            {
                return inferredModFolder;
            }

            return "BG2Narrator";
        }

        private static bool TryInferPathsFromExeLocation(out string gameFolder, out string modFolder)
        {
            gameFolder = null;
            modFolder = null;

            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(exeDir))
            {
                return false;
            }

            string folderName = Path.GetFileName(exeDir);
            if (!string.Equals(folderName, "BG2Narrator", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(folderName, "PSTNarrator", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            DirectoryInfo parent = Directory.GetParent(exeDir);
            if (parent == null || !Directory.Exists(parent.FullName))
            {
                return false;
            }

            modFolder = folderName;
            gameFolder = parent.FullName;
            return true;
        }
    }
}

