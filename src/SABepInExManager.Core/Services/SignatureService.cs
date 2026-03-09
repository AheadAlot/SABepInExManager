using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SABepInExManager.Core.Models;

namespace SABepInExManager.Core.Services;

public class SignatureService
{
    public string BuildManagedSignature(IEnumerable<ManagedFileEntry> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries
                     .OrderBy(x => x.TargetRelativePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(entry.SourcePath))
            {
                continue;
            }

            var fileHash = ComputeSha256Hex(File.ReadAllBytes(entry.SourcePath));
            builder.Append(entry.TargetRelativePath)
                .Append('|')
                .Append(fileHash)
                .Append(';');
        }

        return ComputeSha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string ComputeSha256Hex(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("X2"));
        }

        return sb.ToString();
    }
}

