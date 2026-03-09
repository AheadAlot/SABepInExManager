using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Tests.Common;

namespace SABepInExManager.Core.Tests;

public class SignatureServiceTests
{
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

