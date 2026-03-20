using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SABepInExManager.Core.Models;

namespace SABepInExManager.Core.Services;

public class WorkshopModDiscoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public List<DiscoveredWorkshopMod> ScanMods(string workshopContentPath, IReadOnlyList<string> enabledOrder)
    {
        if (!Directory.Exists(workshopContentPath))
        {
            return [];
        }

        var mods = new List<DiscoveredWorkshopMod>();
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
            mods.Add(new DiscoveredWorkshopMod
            {
                ModId = modId,
                ModRootPath = modDir,
                BepInExRootPath = bepInExRoot,
                DisplayName = string.IsNullOrWhiteSpace(manifest?.Title) ? modId : manifest!.Title!,
                Description = manifest?.Description ?? string.Empty,
                PreviewImagePath = File.Exists(previewPath) ? previewPath : null,
                StructureType = structureType.Value,
            });
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

    public ModStructureType? DetectModStructureType(string modDir)
    {
        if (Directory.Exists(Path.Combine(modDir, "BepInEx")))
        {
            return ModStructureType.Standard;
        }

        var hasDll = Directory.GetFiles(modDir, "*.dll", SearchOption.AllDirectories).Length > 0;
        if (!hasDll)
        {
            return null;
        }

        var hasExe = Directory.GetFiles(modDir, "*.exe", SearchOption.AllDirectories).Length > 0;
        if (!hasExe)
        {
            return ModStructureType.Flat;
        }

        return null;
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

