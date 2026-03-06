using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class WorkshopService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string? AutoDetectWorkshopPath(string gameRootPath)
    {
        if (string.IsNullOrWhiteSpace(gameRootPath) || !Directory.Exists(gameRootPath))
        {
            return null;
        }

        var dir = new DirectoryInfo(Path.GetFullPath(gameRootPath));
        while (dir.Parent is not null)
        {
            if (string.Equals(dir.Name, "common", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(dir.Parent.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = Path.Combine(dir.Parent.FullName, "workshop", "content", PathConstants.WorkshopAppId);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    public List<WorkshopModInfo> ScanMods(string workshopContentPath, IReadOnlyList<string> enabledOrder)
    {
        if (!Directory.Exists(workshopContentPath))
        {
            return [];
        }

        var mods = new List<WorkshopModInfo>();
        foreach (var modDir in Directory.GetDirectories(workshopContentPath))
        {
            var modId = Path.GetFileName(modDir);
            if (string.IsNullOrWhiteSpace(modId))
            {
                continue;
            }

            var bepInExRoot = Path.Combine(modDir, "BepInEx");
            if (!Directory.Exists(bepInExRoot))
            {
                continue;
            }

            var manifest = TryReadManifest(modDir);
            var previewPath = Path.Combine(modDir, "preview.jpg");
            var item = new WorkshopModInfo
            {
                ModId = modId,
                ModRootPath = modDir,
                BepInExRootPath = bepInExRoot,
                DisplayName = string.IsNullOrWhiteSpace(manifest?.Title) ? modId : manifest!.Title!,
                Description = manifest?.Description ?? string.Empty,
                PreviewImagePath = File.Exists(previewPath) ? previewPath : null,
                IsEnabled = enabledOrder.Contains(modId, StringComparer.OrdinalIgnoreCase),
            };

            mods.Add(item);
        }

        return mods
            .OrderBy(m =>
            {
                var index = enabledOrder
                    .Select((id, i) => (id, i))
                    .FirstOrDefault(x => string.Equals(x.id, m.ModId, StringComparison.OrdinalIgnoreCase));
                return index == default ? int.MaxValue : index.i;
            })
            .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<ConflictItem> BuildConflicts(IReadOnlyList<WorkshopModInfo> enabledMods)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in enabledMods)
        {
            foreach (var rel in EnumerateManagedRelativePaths(mod.BepInExRootPath))
            {
                if (!map.TryGetValue(rel, out var modIds))
                {
                    modIds = [];
                    map[rel] = modIds;
                }

                modIds.Add(mod.ModId);
            }
        }

        return map
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => new ConflictItem
            {
                RelativePath = kv.Key,
                ModIds = kv.Value,
                WinnerModId = kv.Value.Last(),
            })
            .OrderBy(c => c.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IEnumerable<string> EnumerateManagedRelativePaths(string modBepInExRoot)
    {
        foreach (var sub in PathConstants.ManagedBepInExSubDirs)
        {
            var source = Path.Combine(modBepInExRoot, sub);
            if (!Directory.Exists(source))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relInsideSub = Path.GetRelativePath(source, file);
                yield return Path.Combine(sub, relInsideSub).Replace('\\', '/');
            }
        }
    }

    public string BuildManagedSignature(string modBepInExRoot)
    {
        if (string.IsNullOrWhiteSpace(modBepInExRoot) || !Directory.Exists(modBepInExRoot))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var rel in EnumerateManagedRelativePaths(modBepInExRoot)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.Combine(modBepInExRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath)));
            builder.Append(rel)
                .Append('|')
                .Append(fileHash)
                .Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static WorkshopManifest? TryReadManifest(string modDir)
    {
        var manifestPath = Path.Combine(modDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var text = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<WorkshopManifest>(text, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}


