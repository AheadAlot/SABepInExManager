using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SABepInExManager.Core.Models;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class ModApplyService
{
    private static readonly string[] PreservedPluginDirectories = ["ConfigurationManager"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public void CreateBaseline(string gameRoot)
    {
        EnsureGameRootValid(gameRoot);

        var baselineContainerRoot = GetBaselineContainerRoot(gameRoot);
        Directory.CreateDirectory(baselineContainerRoot);

        var baselineRoot = Path.Combine(
            baselineContainerRoot,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

        Directory.CreateDirectory(baselineRoot);
        foreach (var sub in PathConstants.ManagedBepInExSubDirs)
        {
            var src = Path.Combine(gameRoot, "BepInEx", sub);
            var dst = Path.Combine(baselineRoot, sub);
            if (Directory.Exists(src))
            {
                CopyDirectory(src, dst, overwrite: true);
            }
            else
            {
                Directory.CreateDirectory(dst);
            }
        }
    }

    public void RestoreBaseline(string gameRoot, string? snapshotFolderName = null)
    {
        EnsureGameRootValid(gameRoot);
        var baselineRoot = ResolveBaselineRootForRestore(gameRoot, snapshotFolderName);
        if (string.IsNullOrWhiteSpace(baselineRoot) || !Directory.Exists(baselineRoot))
        {
            throw new InvalidOperationException("未找到备份，请先创建备份。");
        }

        var preservedPlugins = BackupPreservedPluginDirectories(gameRoot);

        var gameBepInEx = Path.Combine(gameRoot, "BepInEx");
        Directory.CreateDirectory(gameBepInEx);

        foreach (var sub in PathConstants.ManagedBepInExSubDirs)
        {
            var dst = Path.Combine(gameBepInEx, sub);
            if (Directory.Exists(dst))
            {
                Directory.Delete(dst, recursive: true);
            }

            var src = Path.Combine(baselineRoot, sub);
            if (Directory.Exists(src))
            {
                CopyDirectory(src, dst, overwrite: true);
            }
            else
            {
                Directory.CreateDirectory(dst);
            }
        }

        RestorePreservedPluginDirectories(gameRoot, preservedPlugins);
    }

    public void DeleteBaselineSnapshot(string gameRoot, string snapshotFolderName)
    {
        EnsureGameRootValid(gameRoot);
        if (string.IsNullOrWhiteSpace(snapshotFolderName))
        {
            throw new InvalidOperationException("备份目录名称无效。");
        }

        var baselineContainerRoot = GetBaselineContainerRoot(gameRoot);
        var snapshotRoot = Path.Combine(baselineContainerRoot, snapshotFolderName);
        if (!Directory.Exists(snapshotRoot))
        {
            throw new InvalidOperationException("备份目录不存在。 ");
        }

        Directory.Delete(snapshotRoot, recursive: true);
    }

    public void DeleteAllBaselineSnapshots(string gameRoot)
    {
        EnsureGameRootValid(gameRoot);

        var baselineContainerRoot = GetBaselineContainerRoot(gameRoot);
        if (!Directory.Exists(baselineContainerRoot))
        {
            return;
        }

        Directory.Delete(baselineContainerRoot, recursive: true);
    }

    public void Apply(
        string gameRoot,
        IReadOnlyList<WorkshopModInfo> enabledMods)
    {
        EnsureGameRootValid(gameRoot);

        var gameBepInEx = Path.Combine(gameRoot, "BepInEx");
        foreach (var mod in enabledMods)
        {
            if (mod.StructureType == ModStructureType.Flat)
            {
                var pluginsDst = Path.Combine(gameBepInEx, "plugins");
                Directory.CreateDirectory(pluginsDst);

                foreach (var dllFile in Directory.GetFiles(mod.BepInExRootPath, "*.dll", SearchOption.AllDirectories))
                {
                    var relPath = Path.GetRelativePath(mod.BepInExRootPath, dllFile);
                    var dstPath = Path.Combine(pluginsDst, relPath);
                    
                    var dstDir = Path.GetDirectoryName(dstPath);
                    if (!string.IsNullOrEmpty(dstDir))
                    {
                        Directory.CreateDirectory(dstDir);
                    }
                    
                    File.Copy(dllFile, dstPath, overwrite: true);
                }
                continue;
            }

            foreach (var sub in PathConstants.ManagedBepInExSubDirs)
            {
                var src = Path.Combine(mod.BepInExRootPath, sub);
                if (!Directory.Exists(src))
                {
                    continue;
                }

                var dst = Path.Combine(gameBepInEx, sub);
                Directory.CreateDirectory(dst);
                CopyDirectory(src, dst, overwrite: true);
            }
        }

        SaveState(gameRoot, new AppState
        {
            EnabledModIds = enabledMods.Select(x => x.ModId).ToList(),
        });
    }

    public AppState? LoadState(string gameRoot)
    {
        MigrateLegacyStateIfNeeded(gameRoot);

        var file = GetStateFile(gameRoot);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<AppState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void SaveState(string gameRoot, AppState state)
    {
        MigrateLegacyStateIfNeeded(gameRoot);

        var file = GetStateFile(gameRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(file, json);
    }

    private static string GetStateRoot(string gameRoot)
        => Path.Combine(AppContext.BaseDirectory, PathConstants.ManagerStateFolder);

    private static string GetLegacyStateRoot(string gameRoot)
        => Path.Combine(gameRoot, PathConstants.StateRootFolder);

    private static string GetBaselineContainerRoot(string gameRoot)
        => Path.Combine(GetLegacyStateRoot(gameRoot), PathConstants.BaselineFolder);

    private static string? ResolveBaselineRootForRestore(string gameRoot, string? snapshotFolderName)
    {
        if (string.IsNullOrWhiteSpace(snapshotFolderName))
        {
            return GetLatestBaselineRoot(gameRoot);
        }

        return Path.Combine(GetBaselineContainerRoot(gameRoot), snapshotFolderName);
    }

    private static string? GetLatestBaselineRoot(string gameRoot)
    {
        var baselineContainerRoot = GetBaselineContainerRoot(gameRoot);
        if (!Directory.Exists(baselineContainerRoot))
        {
            return null;
        }

        var hasLegacyBaselineContent = PathConstants.ManagedBepInExSubDirs
            .Select(sub => Path.Combine(baselineContainerRoot, sub))
            .Any(Directory.Exists);
        if (hasLegacyBaselineContent)
        {
            return baselineContainerRoot;
        }

        var latestSnapshot = Directory
            .GetDirectories(baselineContainerRoot)
            .Select(path =>
            {
                var name = Path.GetFileName(path);
                var isValid = long.TryParse(name, out var timestamp);
                return new
                {
                    Path = path,
                    IsValid = isValid,
                    Timestamp = isValid ? timestamp : -1,
                };
            })
            .Where(x => x.IsValid)
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefault();

        return latestSnapshot?.Path;
    }

    private static string GetStateFile(string gameRoot)
        => Path.Combine(GetStateRoot(gameRoot), PathConstants.StateFileName);

    private static string GetLegacyStateFile(string gameRoot)
        => Path.Combine(GetLegacyStateRoot(gameRoot), PathConstants.StateFileName);

    private static void MigrateLegacyStateIfNeeded(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return;
        }

        var legacyRoot = GetLegacyStateRoot(gameRoot);
        var legacyStateFile = GetLegacyStateFile(gameRoot);
        if (!Directory.Exists(legacyRoot) || !File.Exists(legacyStateFile))
        {
            return;
        }

        var stateFile = GetStateFile(gameRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);
        File.Copy(legacyStateFile, stateFile, overwrite: true);
        Directory.Delete(legacyRoot, recursive: true);
    }

    private static void EnsureGameRootValid(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            throw new InvalidOperationException("游戏根目录无效。");
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite)
    {
        var sourceInfo = new DirectoryInfo(sourceDir);
        if (!sourceInfo.Exists)
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in sourceInfo.GetFiles())
        {
            var targetFile = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFile, overwrite);
        }

        foreach (var subDir in sourceInfo.GetDirectories())
        {
            var nextDst = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, nextDst, overwrite);
        }
    }

    private static Dictionary<string, string> BackupPreservedPluginDirectories(string gameRoot)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pluginsRoot = Path.Combine(gameRoot, "BepInEx", "plugins");
        if (!Directory.Exists(pluginsRoot))
        {
            return result;
        }

        foreach (var folderName in PreservedPluginDirectories)
        {
            var source = Path.Combine(pluginsRoot, folderName);
            if (!Directory.Exists(source))
            {
                continue;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "SABepInExManager", "preserved-plugins", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var tempPath = Path.Combine(tempRoot, folderName);
            CopyDirectory(source, tempPath, overwrite: true);
            result[folderName] = tempPath;
        }

        return result;
    }

    private static void RestorePreservedPluginDirectories(string gameRoot, IReadOnlyDictionary<string, string> preservedPlugins)
    {
        if (preservedPlugins.Count == 0)
        {
            return;
        }

        var pluginsRoot = Path.Combine(gameRoot, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsRoot);

        foreach (var (folderName, tempPath) in preservedPlugins)
        {
            if (!Directory.Exists(tempPath))
            {
                continue;
            }

            var destination = Path.Combine(pluginsRoot, folderName);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            CopyDirectory(tempPath, destination, overwrite: true);

            var tempRoot = Directory.GetParent(tempPath);
            if (tempRoot?.Exists == true)
            {
                Directory.Delete(tempRoot.FullName, recursive: true);
            }
        }
    }
}
