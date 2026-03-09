using System;
using System.IO;
using SABepInExManager.Core.Constants;

namespace SABepInExManager.Core.Services;

public class WorkshopPathResolver
{
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
}

