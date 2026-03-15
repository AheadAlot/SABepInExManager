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
            if (entry.IsDirectory)
            {
                builder.Append(entry.TargetRelativePath)
                    .Append("|DIR;");
                continue;
            }

            if (!File.Exists(entry.SourcePath))
            {
                continue;
            }

            var fileHash = ComputeFileSha256Hex(entry.SourcePath);
            builder.Append(entry.TargetRelativePath)
                .Append('|')
                .Append(fileHash)
                .Append(';');
        }

        return ComputeUtf8Sha256Hex(builder.ToString());
    }

    public string ComputeFileSha256Hex(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空。", nameof(filePath));
        }

        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return ToHexUpper(hash);
    }

    public string ComputeUtf8Sha256Hex(string text)
        => ComputeBytesSha256Hex(Encoding.UTF8.GetBytes(text ?? string.Empty));

    public string ComputeBytesSha256Hex(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return ToHexUpper(hash);
    }

    private static string ToHexUpper(byte[] hash)
    {
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("X2"));
        }

        return sb.ToString();
    }
}

