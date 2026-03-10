using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class SteamLocatorService
{
    public GameRootDetectionResult TryDetectGameRoot(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return new GameRootDetectionResult
            {
                Success = false,
                Message = "AppID 不能为空。",
            };
        }

        var candidates = new List<GameRootCandidate>();
        var steamRoots = GetSteamRootCandidates().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var steamRoot in steamRoots)
        {
            foreach (var libraryPath in GetLibraryFolders(steamRoot))
            {
                var manifestPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{appId}.acf");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                var installDir = TryReadInstallDirFromAppManifest(manifestPath);
                if (string.IsNullOrWhiteSpace(installDir))
                {
                    continue;
                }

                var gameRootPath = Path.Combine(libraryPath, "steamapps", "common", installDir);
                if (!Directory.Exists(gameRootPath))
                {
                    continue;
                }

                candidates.Add(new GameRootCandidate
                {
                    SteamRootPath = steamRoot,
                    LibraryPath = libraryPath,
                    ManifestPath = manifestPath,
                    InstallDir = installDir,
                    GameRootPath = gameRootPath,
                    ManifestLastWriteTime = File.GetLastWriteTimeUtc(manifestPath),
                });
            }
        }

        if (candidates.Count == 0)
        {
            return new GameRootDetectionResult
            {
                Success = false,
                Message = $"未找到可用的 appmanifest_{appId}.acf 或对应游戏目录不存在。",
                Candidates = candidates,
            };
        }

        var selected = candidates
            .OrderByDescending(c => c.ManifestLastWriteTime)
            .ThenBy(c => c.GameRootPath, StringComparer.OrdinalIgnoreCase)
            .First();

        return new GameRootDetectionResult
        {
            Success = true,
            GameRootPath = selected.GameRootPath,
            Message = "已通过 Steam appmanifest 自动探测到游戏根目录。",
            Candidates = candidates,
        };
    }

    private IEnumerable<string> GetSteamRootCandidates()
    {
        var candidates = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registrySteamPath = TryGetSteamPathFromWindowsRegistry();
            if (!string.IsNullOrWhiteSpace(registrySteamPath))
            {
                candidates.Add(registrySteamPath);
            }

            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "Steam"));
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            candidates.Add(Path.Combine(home, ".steam", "steam"));
            candidates.Add(Path.Combine(home, ".local", "share", "Steam"));
            candidates.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
        }

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    private static string? TryGetSteamPathFromWindowsRegistry()
    {
        try
        {
            var steamPath = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Valve\Steam",
                "SteamPath",
                null) as string;

            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                return steamPath;
            }

            return Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Valve\Steam",
                "InstallPath",
                null) as string;
        }
        catch
        {
            return null;
        }
    }

    private IEnumerable<string> GetLibraryFolders(string steamRoot)
    {
        var libraries = new List<string>();

        if (!string.IsNullOrWhiteSpace(steamRoot) && Directory.Exists(steamRoot))
        {
            libraries.Add(Path.GetFullPath(steamRoot));
        }

        var libraryFoldersVdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersVdfPath))
        {
            return libraries.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var text = File.ReadAllText(libraryFoldersVdfPath);
            foreach (var path in ParseLibraryFolders(text))
            {
                if (Directory.Exists(path))
                {
                    libraries.Add(Path.GetFullPath(path));
                }
            }
        }
        catch
        {
            // ignore parse errors and keep current libraries
        }

        return libraries.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ParseLibraryFolders(string text)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 新格式："path" "D:\\SteamLibrary"
        foreach (Match match in Regex.Matches(text, "\"path\"\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
        {
            var value = UnescapeVdfString(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                paths.Add(value);
            }
        }

        // 旧格式："1" "D:\\SteamLibrary"
        foreach (Match match in Regex.Matches(text, "\"(?<index>\\d+)\"\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
        {
            var value = UnescapeVdfString(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                paths.Add(value);
            }
        }

        return paths;
    }

    private static string? TryReadInstallDirFromAppManifest(string manifestPath)
    {
        try
        {
            var text = File.ReadAllText(manifestPath);
            var match = Regex.Match(text, "\"installdir\"\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return UnescapeVdfString(match.Groups["value"].Value);
        }
        catch
        {
            return null;
        }
    }

    private static string UnescapeVdfString(string text)
    {
        return text
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Trim();
    }
}


