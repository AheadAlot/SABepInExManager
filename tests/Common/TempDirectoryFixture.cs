using System;
using System.IO;

namespace SABepInExManager.Tests.Common;

public sealed class TempDirectoryFixture : IDisposable
{
    public string RootPath { get; }

    public TempDirectoryFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "SABepInExManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string PathOf(params string[] segments)
    {
        var current = RootPath;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
        }

        return current;
    }

    public string EnsureDirectory(params string[] segments)
    {
        var path = PathOf(segments);
        Directory.CreateDirectory(path);
        return path;
    }

    public string WriteText(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup failures in temp scope
        }
    }
}

