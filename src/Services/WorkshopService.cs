using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class WorkshopService
{
    private static readonly Version EmptyVersion = new(0, 0, 0, 0);

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

            var structureType = DetectModStructureType(modDir);
            if (structureType == null)
            {
                continue;
            }

            var bepInExRoot = structureType == ModStructureType.Standard
                ? Path.Combine(modDir, "BepInEx")
                : modDir;

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
                StructureType = structureType.Value,
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

    private static ModStructureType? DetectModStructureType(string modDir)
    {
        if (Directory.Exists(Path.Combine(modDir, "BepInEx")))
        {
            return ModStructureType.Standard;
        }

        if (Directory.GetFiles(modDir, "*.dll", SearchOption.AllDirectories).Length > 0)
        {
            return ModStructureType.Flat;
        }

        return null;
    }

    public List<ConflictItem> BuildConflicts(IReadOnlyList<WorkshopModInfo> enabledMods)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in enabledMods)
        {
            foreach (var rel in EnumerateManagedRelativePaths(mod.BepInExRootPath, mod.StructureType))
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

    public IEnumerable<string> EnumerateManagedRelativePaths(string modBepInExRoot, ModStructureType structureType = ModStructureType.Standard)
    {
        if (structureType == ModStructureType.Flat)
        {
            foreach (var file in Directory.EnumerateFiles(modBepInExRoot, "*.dll", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(modBepInExRoot, file).Replace('\\', '/');
                yield return $"plugins/{relPath}";
            }
            yield break;
        }

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

    public string BuildManagedSignature(string modBepInExRoot, ModStructureType structureType = ModStructureType.Standard)
    {
        if (string.IsNullOrWhiteSpace(modBepInExRoot) || !Directory.Exists(modBepInExRoot))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var rel in EnumerateManagedRelativePaths(modBepInExRoot, structureType)
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

    public bool HasAssemblyVersionUpdate(string gameRootPath, WorkshopModInfo mod)
    {
        if (string.IsNullOrWhiteSpace(gameRootPath) || !Directory.Exists(gameRootPath))
        {
            return false;
        }

        foreach (var (workshopDllPath, installedDllPath) in EnumerateManagedDllPairs(gameRootPath, mod))
        {
            if (!File.Exists(workshopDllPath))
            {
                continue;
            }

            if (!File.Exists(installedDllPath))
            {
                return true;
            }

            var workshopVersion = GetAssemblyVersion(workshopDllPath);
            var installedVersion = GetAssemblyVersion(installedDllPath);
            if (workshopVersion > installedVersion)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string WorkshopDllPath, string InstalledDllPath)> EnumerateManagedDllPairs(
        string gameRootPath,
        WorkshopModInfo mod)
    {
        var gameBepInExRoot = Path.Combine(gameRootPath, "BepInEx");

        if (mod.StructureType == ModStructureType.Flat)
        {
            foreach (var workshopDllPath in Directory.EnumerateFiles(mod.BepInExRootPath, "*.dll", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(mod.BepInExRootPath, workshopDllPath);
                var installedDllPath = Path.Combine(gameBepInExRoot, "plugins", relPath);
                yield return (workshopDllPath, installedDllPath);
            }

            yield break;
        }

        foreach (var sub in PathConstants.ManagedBepInExSubDirs)
        {
            var workshopSubPath = Path.Combine(mod.BepInExRootPath, sub);
            if (!Directory.Exists(workshopSubPath))
            {
                continue;
            }

            foreach (var workshopDllPath in Directory.EnumerateFiles(workshopSubPath, "*.dll", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(workshopSubPath, workshopDllPath);
                var installedDllPath = Path.Combine(gameBepInExRoot, sub, relPath);
                yield return (workshopDllPath, installedDllPath);
            }
        }
    }

    private static Version GetAssemblyVersion(string dllPath)
    {
        try
        {
            return AssemblyName.GetAssemblyName(dllPath).Version ?? EmptyVersion;
        }
        catch
        {
            return EmptyVersion;
        }
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
