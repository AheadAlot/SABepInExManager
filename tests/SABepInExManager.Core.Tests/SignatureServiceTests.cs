using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using System.Security.Cryptography;
using System.Text;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Tests.Common;

namespace SABepInExManager.Core.Tests;

public class SignatureServiceTests
{
    [Property(MaxTest = 40)]
    public void ComputeFileSha256Hex_ShouldMatchKnownValue(NonEmptyString contentSeed)
    {
        using var fx = new TempDirectoryFixture();
        var service = new SignatureService();

        var content = contentSeed.Get;
        var path = fx.WriteText("mods/m1/a.dll", content);
        var actual = service.ComputeFileSha256Hex(path);

        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        actual.Should().Be(expected);
    }

    [Property(MaxTest = 40)]
    public void ComputeUtf8Sha256Hex_ShouldMatchBytesVersion(NonEmptyString textSeed)
    {
        var service = new SignatureService();
        var text = textSeed.Get;

        var fromText = service.ComputeUtf8Sha256Hex(text);
        var fromBytes = service.ComputeBytesSha256Hex(Encoding.UTF8.GetBytes(text));

        fromText.Should().Be(fromBytes);
    }

    [Property(MaxTest = 40)]
    public void BuildManagedSignature_ShouldChangeWhenManagedFileContentChanges(
        NonEmptyString fileNameSeed,
        NonEmptyString firstContentSeed,
        NonEmptyString secondContentSeed)
    {
        using var fx = new TempDirectoryFixture();
        var service = new SignatureService();

        var fileName = ToSafeFileName(fileNameSeed.Get) + ".dll";
        var firstContent = firstContentSeed.Get;
        var secondContent = secondContentSeed.Get + "_changed";

        var filePath = fx.WriteText($"mods/m1/{fileName}", firstContent);
        var entries = new List<ManagedFileEntry>
        {
            new()
            {
                ModId = "m1",
                SourcePath = filePath,
                TargetRelativePath = $"plugins/{fileName}",
            },
        };

        var sig1 = service.BuildManagedSignature(entries);
        File.WriteAllText(filePath, secondContent);
        var sig2 = service.BuildManagedSignature(entries);

        sig1.Should().NotBe(sig2);
    }

    private static string ToSafeFileName(string seed)
    {
        var chars = seed
            .Where(c => char.IsLetterOrDigit(c))
            .Take(24)
            .ToArray();
        return chars.Length == 0 ? "x" : new string(chars);
    }
}

