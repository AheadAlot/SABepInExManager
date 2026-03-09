using FluentAssertions;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Tests.Common;
using Xunit;

namespace SABepInExManager.Core.Tests;

public class SignatureServiceTests
{
    [Fact]
    public void BuildManagedSignature_ShouldChangeWhenFileContentChanges()
    {
        using var fx = new TempDirectoryFixture();
        var service = new SignatureService();

        var filePath = fx.WriteText("mods/m1/a.dll", "v1");
        var entries = new List<ManagedFileEntry>
        {
            new()
            {
                ModId = "m1",
                SourcePath = filePath,
                TargetRelativePath = "plugins/a.dll",
            },
        };

        var sig1 = service.BuildManagedSignature(entries);
        File.WriteAllText(filePath, "v2");
        var sig2 = service.BuildManagedSignature(entries);

        sig1.Should().NotBe(sig2);
    }
}

