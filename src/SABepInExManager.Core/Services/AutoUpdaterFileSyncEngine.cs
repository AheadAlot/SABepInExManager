using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SABepInExManager.Core.Constants;
using SABepInExManager.Core.Models;

namespace SABepInExManager.Core.Services;

public static class AutoUpdaterFileSyncEngine
{
    private const string PreservedPluginDirectory = "ConfigurationManager";

    public static AutoUpdaterModState SyncSingleMod(
        string gameBepInExRoot,
        string selfAssemblyPath,
        string modId,
        IReadOnlyList<ManagedFileEntry> entries,
        AutoUpdaterModState? oldModState,
        DateTimeOffset now,
        Action<string>? logInfo = null,
        Action<string>? logWarning = null)
    {
        var targetSet = new HashSet<string>(entries.Select(x => NormalizeRelativePath(x.TargetRelativePath)), StringComparer.OrdinalIgnoreCase);
        var oldFiles = oldModState?.Files ?? [];

        foreach (var oldFile in oldFiles)
        {
            var normalized = NormalizeRelativePath(oldFile);
            if (targetSet.Contains(normalized))
            {
                continue;
            }

            if (!TryResolveManagedTargetPath(gameBepInExRoot, normalized, out var deletePath))
            {
                continue;
            }

            if (IsProtectedPath(deletePath, selfAssemblyPath))
            {
                continue;
            }

            try
            {
                if (File.Exists(deletePath))
                {
                    File.Delete(deletePath);
                    logInfo?.Invoke($"[AutoUpdater] [{modId}] 删除旧文件: {normalized}");
                }
            }
            catch (Exception ex)
            {
                logWarning?.Invoke($"[AutoUpdater] [{modId}] 删除失败: {normalized} | {ex.Message}");
            }
        }

        foreach (var entry in entries)
        {
            var normalized = NormalizeRelativePath(entry.TargetRelativePath);
            if (!TryResolveManagedTargetPath(gameBepInExRoot, normalized, out var targetPath))
            {
                continue;
            }

            if (IsProtectedPath(targetPath, selfAssemblyPath))
            {
                continue;
            }

            try
            {
                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(entry.SourcePath, targetPath, overwrite: true);
            }
            catch (Exception ex)
            {
                logWarning?.Invoke($"[AutoUpdater] [{modId}] 覆盖失败: {normalized} | {ex.Message}");
            }
        }

        return new AutoUpdaterModState
        {
            Signature = oldModState?.Signature ?? string.Empty,
            Files = targetSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            LastSyncedAt = now,
        };
    }

    private static bool TryResolveManagedTargetPath(string gameBepInExRoot, string targetRelativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(targetRelativePath))
        {
            return false;
        }

        var normalized = NormalizeRelativePath(targetRelativePath);
        if (!IsWhitelisted(normalized))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(gameBepInExRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(gameBepInExRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static bool IsWhitelisted(string targetRelativePath)
    {
        var normalized = NormalizeRelativePath(targetRelativePath);
        return PathConstants.ManagedBepInExSubDirs.Any(sub =>
            normalized.Equals(sub, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(sub + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtectedPath(string fullPath, string selfAssemblyPath)
    {
        if (string.Equals(Path.GetFullPath(fullPath), selfAssemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = fullPath.Replace('\\', '/');
        var preservePrefix = "/BepInEx/plugins/" + PreservedPluginDirectory + "/";
        return normalized.IndexOf(preservePrefix, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').TrimStart('/');
}

