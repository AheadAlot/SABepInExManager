using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class WorkshopService
{
    private readonly WorkshopPathResolver _workshopPathResolver = new();
    private readonly WorkshopModDiscoveryService _modDiscoveryService = new();
    private readonly ManagedFileManifestService _managedFileManifestService = new();
    private readonly SignatureService _signatureService = new();

    private static readonly Version EmptyVersion = new(0, 0, 0, 0);

    public string? AutoDetectWorkshopPath(string gameRootPath)
        => _workshopPathResolver.AutoDetectWorkshopPath(gameRootPath);

    public List<WorkshopModInfo> ScanMods(string workshopContentPath, IReadOnlyList<string> enabledOrder)
    {
        var discoveredMods = _modDiscoveryService.ScanMods(workshopContentPath, enabledOrder);
        return discoveredMods.Select(mod => new WorkshopModInfo
            {
                ModId = mod.ModId,
                ModRootPath = mod.ModRootPath,
                BepInExRootPath = mod.BepInExRootPath,
                DisplayName = mod.DisplayName,
                Description = mod.Description,
                PreviewImagePath = mod.PreviewImagePath,
                IsEnabled = enabledOrder.Contains(mod.ModId, StringComparer.OrdinalIgnoreCase),
                StructureType = mod.StructureType,
            })
            .ToList();
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
        => _managedFileManifestService
            .BuildEntries("_", modBepInExRoot, structureType)
            .Select(x => x.TargetRelativePath);

    public string BuildManagedSignature(string modBepInExRoot, ModStructureType structureType = ModStructureType.Standard)
    {
        var entries = _managedFileManifestService.BuildEntries("_", modBepInExRoot, structureType);
        return _signatureService.BuildManagedSignature(entries);
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

    private IEnumerable<(string WorkshopDllPath, string InstalledDllPath)> EnumerateManagedDllPairs(
        string gameRootPath,
        WorkshopModInfo mod)
    {
        var gameBepInExRoot = Path.Combine(gameRootPath, "BepInEx");

        foreach (var entry in _managedFileManifestService.BuildEntries(mod.ModId, mod.BepInExRootPath, mod.StructureType))
        {
            if (!entry.SourcePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var installedDllPath = Path.Combine(gameBepInExRoot, entry.TargetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            yield return (entry.SourcePath, installedDllPath);
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

}
