using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SABepInExManager.Core.Constants;
using SABepInExManager.Core.Models;

namespace SABepInExManager.Core.Services;

public class ManagedFileManifestService
{
    public IReadOnlyList<ManagedFileEntry> BuildEntries(DiscoveredWorkshopMod mod)
    {
        if (mod is null)
        {
            throw new ArgumentNullException(nameof(mod));
        }

        return BuildEntries(mod.ModId, mod.BepInExRootPath, mod.StructureType);
    }

    public IReadOnlyList<ManagedFileEntry> BuildEntries(string modId, string modBepInExRoot, ModStructureType structureType)
    {
        var entries = new List<ManagedFileEntry>();
        if (string.IsNullOrWhiteSpace(modBepInExRoot) || !Directory.Exists(modBepInExRoot))
        {
            return entries;
        }

        if (structureType == ModStructureType.Flat)
        {
            foreach (var sourcePath in Directory.EnumerateFiles(modBepInExRoot, "*.dll", SearchOption.AllDirectories))
            {
                var relPath = GetRelativePathCompat(modBepInExRoot, sourcePath).Replace('\\', '/');
                entries.Add(new ManagedFileEntry
                {
                    ModId = modId,
                    SourcePath = sourcePath,
                    TargetRelativePath = $"plugins/{relPath}",
                    SourceStructureType = structureType,
                });
            }

            foreach (var dirPath in Directory.EnumerateDirectories(modBepInExRoot, "*", SearchOption.AllDirectories))
            {
                if (Directory.EnumerateFileSystemEntries(dirPath).Any())
                {
                    continue;
                }

                var relPath = GetRelativePathCompat(modBepInExRoot, dirPath).Replace('\\', '/').Trim('/');
                if (string.IsNullOrWhiteSpace(relPath))
                {
                    continue;
                }

                entries.Add(new ManagedFileEntry
                {
                    ModId = modId,
                    SourcePath = dirPath,
                    TargetRelativePath = $"plugins/{relPath}",
                    SourceStructureType = structureType,
                    IsDirectory = true,
                });
            }

            return entries;
        }

        foreach (var sub in PathConstants.ManagedBepInExSubDirs)
        {
            var sourceRoot = Path.Combine(modBepInExRoot, sub);
            if (!Directory.Exists(sourceRoot))
            {
                continue;
            }

            foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relInsideSub = GetRelativePathCompat(sourceRoot, sourcePath).Replace('\\', '/');
                entries.Add(new ManagedFileEntry
                {
                    ModId = modId,
                    SourcePath = sourcePath,
                    TargetRelativePath = $"{sub}/{relInsideSub}",
                    SourceStructureType = structureType,
                });
            }

            foreach (var dirPath in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (Directory.EnumerateFileSystemEntries(dirPath).Any())
                {
                    continue;
                }

                var relInsideSub = GetRelativePathCompat(sourceRoot, dirPath).Replace('\\', '/').Trim('/');
                if (string.IsNullOrWhiteSpace(relInsideSub))
                {
                    continue;
                }

                entries.Add(new ManagedFileEntry
                {
                    ModId = modId,
                    SourcePath = dirPath,
                    TargetRelativePath = $"{sub}/{relInsideSub}",
                    SourceStructureType = structureType,
                    IsDirectory = true,
                });
            }
        }

        return entries
            .OrderBy(x => x.TargetRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetRelativePathCompat(string basePath, string fullPath)
    {
        var baseFullPath = Path.GetFullPath(basePath);
        var targetFullPath = Path.GetFullPath(fullPath);

        if (!baseFullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            baseFullPath += Path.DirectorySeparatorChar;
        }

        var baseUri = new Uri(baseFullPath);
        var targetUri = new Uri(targetFullPath);
        var relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString());
        return relative.Replace('/', Path.DirectorySeparatorChar);
    }
}

