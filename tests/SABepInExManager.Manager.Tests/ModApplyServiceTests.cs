using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using SABepInExManager.Core.Models;
using SABepInExManager.Models;
using SABepInExManager.Services;
using SABepInExManager.Tests.Common;

namespace SABepInExManager.Manager.Tests;

public class ModApplyServiceTests
{
    [Property(MaxTest = 50)]
    public void Apply_FlatStructure_ShouldCopyOnlyDllAndSkipNonDllFiles(NonEmptyString dllSeed, NonEmptyString txtSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game");
        var modRoot = fx.EnsureDirectory("mods", "flatMod");
        var service = new ModApplyService();

        var dllName = ToSafeFileName(dllSeed.Get) + ".dll";
        var txtName = ToSafeFileName(txtSeed.Get) + ".txt";

        fx.WriteText($"mods/flatMod/{dllName}", "dll-content");
        fx.WriteText($"mods/flatMod/{txtName}", "txt-content");

        var mod = new WorkshopModInfo
        {
            ModId = "flatMod",
            ModRootPath = modRoot,
            BepInExRootPath = modRoot,
            DisplayName = "Flat Mod",
            StructureType = ModStructureType.Flat,
        };

        service.Apply(gameRoot, [mod]);

        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", dllName)).Should().BeTrue();
        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", txtName)).Should().BeFalse();
    }

    [Property(MaxTest = 40)]
    public void Apply_StandardStructure_ShouldOverwriteExistingFilesAndKeepRemovedResidualFiles(
        NonEmptyString modIdSeed,
        NonEmptyString keepNameSeed,
        NonEmptyString removedNameSeed,
        NonEmptyString firstContentSeed,
        NonEmptyString secondContentSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game");
        var modRoot = fx.EnsureDirectory("mods", "modA", "BepInEx");
        var service = new ModApplyService();

        var modId = ToSafeFileName(modIdSeed.Get);
        var keepDll = ToSafeFileName(keepNameSeed.Get) + "_keep.dll";
        var removedDll = ToSafeFileName(removedNameSeed.Get) + "_removed.dll";
        var firstContent = firstContentSeed.Get;
        var secondContent = secondContentSeed.Get;

        fx.WriteText($"mods/modA/BepInEx/plugins/{keepDll}", firstContent);
        fx.WriteText($"mods/modA/BepInEx/plugins/{removedDll}", "to-be-removed");

        service.Apply(gameRoot, [CreateStandardMod(modId, Path.Combine(fx.RootPath, "mods", "modA"), modRoot)]);

        fx.WriteText($"mods/modA/BepInEx/plugins/{keepDll}", secondContent);
        File.Delete(Path.Combine(modRoot, "plugins", removedDll));

        service.Apply(gameRoot, [CreateStandardMod(modId, Path.Combine(fx.RootPath, "mods", "modA"), modRoot)]);

        var keepPath = Path.Combine(gameRoot, "BepInEx", "plugins", keepDll);
        var removedPath = Path.Combine(gameRoot, "BepInEx", "plugins", removedDll);

        File.ReadAllText(keepPath).Should().Be(secondContent);
        File.Exists(removedPath).Should().BeTrue("当前 Apply 是覆盖式，不会清理删除项");
    }

    [Property(MaxTest = 40)]
    public void Apply_FlatStructure_ShouldPreserveNestedDllPathAndSkipNonDll(
        NonEmptyString topDllSeed,
        NonEmptyString nestedDllSeed,
        NonEmptyString txtSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game");
        var modRoot = fx.EnsureDirectory("mods", "flatMod");
        var service = new ModApplyService();

        var topDll = ToSafeFileName(topDllSeed.Get) + ".dll";
        var nestedDll = ToSafeFileName(nestedDllSeed.Get) + ".dll";
        var txt = ToSafeFileName(txtSeed.Get) + ".txt";

        fx.WriteText($"mods/flatMod/{topDll}", "A");
        fx.WriteText($"mods/flatMod/{txt}", "TXT");
        fx.WriteText($"mods/flatMod/sub/{nestedDll}", "C");

        var mod = new WorkshopModInfo
        {
            ModId = "flatMod",
            ModRootPath = modRoot,
            BepInExRootPath = modRoot,
            DisplayName = "Flat Mod",
            StructureType = ModStructureType.Flat,
        };

        service.Apply(gameRoot, [mod]);

        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", topDll)).Should().BeTrue();
        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", "sub", nestedDll)).Should().BeTrue();
        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", txt)).Should().BeFalse();
    }

    [Property(MaxTest = 40)]
    public void RestoreBaseline_ShouldRestoreManagedDirsAndKeepCurrentConfigurationManagerFiles(
        NonEmptyString baseDllV1Seed,
        NonEmptyString baseDllV2Seed,
        NonEmptyString baseCfgV1Seed,
        NonEmptyString baseCfgV2Seed,
        NonEmptyString patchV1Seed,
        NonEmptyString preservedBeforeSeed,
        NonEmptyString preservedCurrentSeed,
        NonEmptyString extraSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game");
        var service = new ModApplyService();

        var baseDllV1 = baseDllV1Seed.Get;
        var baseDllV2 = baseDllV2Seed.Get;
        var baseCfgV1 = baseCfgV1Seed.Get;
        var baseCfgV2 = baseCfgV2Seed.Get;
        var patchV1 = patchV1Seed.Get;
        var preservedBefore = preservedBeforeSeed.Get;
        var preservedCurrent = preservedCurrentSeed.Get;
        var extra = extraSeed.Get;

        fx.WriteText("game/BepInEx/plugins/base.dll", baseDllV1);
        fx.WriteText("game/BepInEx/config/base.cfg", baseCfgV1);
        fx.WriteText("game/BepInEx/patchers/base.patch", patchV1);
        fx.WriteText("game/BepInEx/plugins/ConfigurationManager/preserved.cfg", preservedBefore);

        service.CreateBaseline(gameRoot);

        fx.WriteText("game/BepInEx/plugins/base.dll", baseDllV2);
        fx.WriteText("game/BepInEx/plugins/extra.dll", extra);
        fx.WriteText("game/BepInEx/config/base.cfg", baseCfgV2);
        fx.WriteText("game/BepInEx/plugins/ConfigurationManager/preserved.cfg", preservedCurrent);

        service.RestoreBaseline(gameRoot);

        File.ReadAllText(Path.Combine(gameRoot, "BepInEx", "plugins", "base.dll")).Should().Be(baseDllV1);
        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", "extra.dll")).Should().BeFalse();
        File.ReadAllText(Path.Combine(gameRoot, "BepInEx", "config", "base.cfg")).Should().Be(baseCfgV1);
        File.ReadAllText(Path.Combine(gameRoot, "BepInEx", "plugins", "ConfigurationManager", "preserved.cfg"))
            .Should().Be(preservedCurrent);
    }

    private static WorkshopModInfo CreateStandardMod(string modId, string modRootPath, string bepinExRoot)
        => new()
        {
            ModId = modId,
            ModRootPath = modRootPath,
            BepInExRootPath = bepinExRoot,
            DisplayName = modId,
            StructureType = ModStructureType.Standard,
        };

    private static string ToSafeFileName(string seed)
    {
        var chars = seed
            .Where(c => char.IsLetterOrDigit(c))
            .Take(24)
            .ToArray();
        return chars.Length == 0 ? "x" : new string(chars);
    }
}

