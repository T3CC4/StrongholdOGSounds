using BepInEx;
using BepInEx.Logging;
using Microsoft.Win32;
using System.IO;
using System.Security.Cryptography;
using Application = UnityEngine.Application;

namespace SCDEOGSounds
{
    [BepInPlugin("de.tecca.ogdesoundmod", "OG Crusader Sounds", "1.0.0")]
    public class OGSounds : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("Sound Replacement Mod");

            var oldGameDir = FindStrongholdDirectory();
            if (oldGameDir == null)
            {
                Logger.LogError("Stronghold Crusader not found");
                return;
            }

            var sourceDir = Path.Combine(oldGameDir, "fx");
            var targetDir = Path.Combine(Application.dataPath, "Assets", "GUI");

            if (!Directory.Exists(sourceDir) || !Directory.Exists(targetDir))
            {
                Logger.LogError($"Required directories not found: {sourceDir} or {targetDir}");
                return;
            }

            ReplaceFiles(sourceDir, targetDir);
        }

        private void ReplaceFiles(string sourceDir, string targetDir)
        {
            var sourceFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
            Logger.LogInfo($"Found {sourceFiles.Length} files in source directory");

            var replaced = 0;
            var skipped = 0;

            foreach (var file in sourceFiles)
            {
                var fileName = Path.GetFileName(file);

                if (fileName.ToLower().Contains("tutorial"))
                {
                    Logger.LogInfo($"Skipped tutorial file: {fileName}");
                    continue;
                }

                var targets = Directory.GetFiles(targetDir, fileName, SearchOption.AllDirectories);

                if (targets.Length == 0)
                {
                    Logger.LogWarning($"Target not found: {fileName}");
                    continue;
                }

                var target = targets[0];
                var sourceHash = GetFileHash(file);
                var targetHash = GetFileHash(target);

                if (sourceHash == targetHash)
                {
                    Logger.LogInfo($"Already replaced: {fileName}");
                    skipped++;
                }
                else
                {
                    File.Copy(file, target, true);
                    Logger.LogInfo($"Replaced: {fileName}");
                    replaced++;
                }
            }

            Logger.LogInfo($"Summary: {replaced} replaced, {skipped} skipped, {sourceFiles.Length - replaced - skipped} not found");
        }

        private string GetFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
                return System.Convert.ToBase64String(md5.ComputeHash(stream));
        }

        private string FindStrongholdDirectory()
        {
            return FindSteamInstall() ?? FindRegistryInstall();
        }

        private string FindSteamInstall()
        {
            var steamPath = GetRegistryValue(@"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") ??
                           GetRegistryValue(@"SOFTWARE\Valve\Steam", "InstallPath");

            if (steamPath == null) return null;

            foreach (var library in GetSteamLibraries(steamPath))
            {
                var paths = new[] {
                    Path.Combine(library, "steamapps", "common", "Stronghold Crusader Extreme"),
                    Path.Combine(library, "steamapps", "common", "Stronghold Crusader"),
                    Path.Combine(library, "steamapps", "common", "Stronghold Crusader HD")
                };

                foreach (var path in paths)
                    if (Directory.Exists(path)) return path;
            }

            return null;
        }

        private string FindRegistryInstall()
        {
            var keys = new[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Stronghold Crusader",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Stronghold Crusader Extreme",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Stronghold Crusader",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Stronghold Crusader Extreme"
            };

            foreach (var key in keys)
            {
                var path = GetRegistryValue(key, "InstallLocation") ?? GetRegistryValue(key, "InstallDir");
                if (path != null && Directory.Exists(path)) return path;
            }

            return null;
        }

        private string[] GetSteamLibraries(string steamPath)
        {
            var libraries = new System.Collections.Generic.List<string> { steamPath };
            var configPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(configPath)) return libraries.ToArray();

            try
            {
                foreach (var line in File.ReadAllLines(configPath))
                {
                    if (!line.Contains("\"path\"")) continue;

                    var start = line.IndexOf("\"", line.IndexOf("\"path\"") + 6) + 1;
                    var end = line.IndexOf("\"", start);

                    if (start > 0 && end > start)
                    {
                        var path = line.Substring(start, end - start).Replace("\\\\", "\\");
                        if (Directory.Exists(path)) libraries.Add(path);
                    }
                }
            }
            catch { }

            return libraries.ToArray();
        }

        private string GetRegistryValue(string keyPath, string valueName)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    return key?.GetValue(valueName) as string;
            }
            catch { return null; }
        }
    }
}
