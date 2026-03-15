using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Tests.Common;

namespace SABepInExManager.Core.Tests;

public class WorkshopModDiscoveryServiceTests
{
    [Property(MaxTest = 40)]
    public void DetectModStructureType_ShouldReturnStandard_WhenBepInExDirectoryExists(
        NonEmptyString exeSeed,
        NonEmptyString dllSeed)
    {
        using var fx = new TempDirectoryFixture();
        var modDir = fx.EnsureDirectory("mods", "m1");
        fx.EnsureDirectory("mods", "m1", "BepInEx");
        fx.WriteText("mods/m1/tool.exe", exeSeed.Get);
        fx.WriteText("mods/m1/sub/plugin.dll", dllSeed.Get);

        var service = new WorkshopModDiscoveryService();

        var type = service.DetectModStructureType(modDir);

        type.Should().Be(ModStructureType.Standard);
    }

    [Property(MaxTest = 40)]
    public void DetectModStructureType_ShouldReturnFlat_WhenHasDllAndNoExe(NonEmptyString dllSeed)
    {
        using var fx = new TempDirectoryFixture();
        var modDir = fx.EnsureDirectory("mods", "m2");
        fx.WriteText("mods/m2/sub/plugin.dll", dllSeed.Get);

        var service = new WorkshopModDiscoveryService();

        var type = service.DetectModStructureType(modDir);

        type.Should().Be(ModStructureType.Flat);
    }

    [Property(MaxTest = 40)]
    public void DetectModStructureType_ShouldReturnNull_WhenHasDllAndExe(
        NonEmptyString dllSeed,
        NonEmptyString exeSeed)
    {
        using var fx = new TempDirectoryFixture();
        var modDir = fx.EnsureDirectory("mods", "m3");
        fx.WriteText("mods/m3/sub/plugin.dll", dllSeed.Get);
        fx.WriteText("mods/m3/tool.exe", exeSeed.Get);

        var service = new WorkshopModDiscoveryService();

        var type = service.DetectModStructureType(modDir);

        type.Should().BeNull();
    }

    [Property(MaxTest = 40)]
    public void DetectModStructureType_ShouldReturnNull_WhenNoDll(NonEmptyString txtSeed)
    {
        using var fx = new TempDirectoryFixture();
        var modDir = fx.EnsureDirectory("mods", "m4");
        fx.WriteText("mods/m4/readme.txt", txtSeed.Get);

        var service = new WorkshopModDiscoveryService();

        var type = service.DetectModStructureType(modDir);

        type.Should().BeNull();
    }
}

