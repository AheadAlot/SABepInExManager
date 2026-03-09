using FluentAssertions;
using SABepInExManager.Core.Models;
using SABepInExManager.Models;
using SABepInExManager.Services;
using SABepInExManager.Tests.Common;
using Xunit;

namespace SABepInExManager.Manager.Tests;

public class ModApplyServiceTests
{
    [Fact]
    public void Apply_ShouldOverwriteExistingFiles_AndKeepRemovedFilesAsResidual()
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game");
        var modRoot = fx.EnsureDirectory("mods", "modA", "BepInEx");
        var service = new ModApplyService();

        fx.WriteText("mods/modA/BepInEx/plugins/keep.dll", "v1");
        fx.WriteText("mods/modA/BepInEx/plugins/removed.dll", "to-be-removed");

        service.Apply(gameRoot, [CreateStandardMod("modA", Path.Combine(fx.RootPath, "mods", "modA"), modRoot)]);

        fx.WriteText("mods/modA/BepInEx/plugins/keep.dll", "v2");
        File.Delete(Path.Combine(modRoot, "plugins", "removed.dll"));

        service.Apply(gameRoot, [CreateStandardMod("modA", Path.Combine(fx.RootPath, "mods", "modA"), modRoot)]);

        var keepPath = Path.Combine(gameRoot, "BepInEx", "plugins", "keep.dll");
        var removedPath = Path.Combine(gameRoot, "BepInEx", "plugins", "removed.dll");

        File.ReadAllText(keepPath).Should().Be("v2");
        File.Exists(removedPath).Should().BeTrue("当前 Apply 是覆盖式，不会清理删除项");
    }

    [Fact]
    public void Apply_FlatStructure_ShouldCopyOnlyDllFilesToPlugins()
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game");
        var modRoot = fx.EnsureDirectory("mods", "flatMod");
        var service = new ModApplyService();

        fx.WriteText("mods/flatMod/A.dll", "A");
        fx.WriteText("mods/flatMod/B.txt", "TXT");
        fx.WriteText("mods/flatMod/sub/C.dll", "C");

        var mod = new WorkshopModInfo
        {
            ModId = "flatMod",
            ModRootPath = modRoot,
            BepInExRootPath = modRoot,
            DisplayName = "Flat Mod",
            StructureType = ModStructureType.Flat,
        };

        service.Apply(gameRoot, [mod]);

        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", "A.dll")).Should().BeTrue();
        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", "sub", "C.dll")).Should().BeTrue();
        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", "B.txt")).Should().BeFalse();
    }

    [Fact]
    public void RestoreBaseline_ShouldRestoreManagedDirs_AndPreserveConfigurationManager()
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game");
        var service = new ModApplyService();

        fx.WriteText("game/BepInEx/plugins/base.dll", "base-v1");
        fx.WriteText("game/BepInEx/config/base.cfg", "cfg-v1");
        fx.WriteText("game/BepInEx/patchers/base.patch", "patch-v1");
        fx.WriteText("game/BepInEx/plugins/ConfigurationManager/preserved.cfg", "preserved-before-baseline");

        service.CreateOrUpdateBaseline(gameRoot);

        fx.WriteText("game/BepInEx/plugins/base.dll", "base-v2");
        fx.WriteText("game/BepInEx/plugins/extra.dll", "extra");
        fx.WriteText("game/BepInEx/config/base.cfg", "cfg-v2");
        fx.WriteText("game/BepInEx/plugins/ConfigurationManager/preserved.cfg", "preserved-current");

        service.RestoreBaseline(gameRoot);

        File.ReadAllText(Path.Combine(gameRoot, "BepInEx", "plugins", "base.dll")).Should().Be("base-v1");
        File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", "extra.dll")).Should().BeFalse();
        File.ReadAllText(Path.Combine(gameRoot, "BepInEx", "config", "base.cfg")).Should().Be("cfg-v1");
        File.ReadAllText(Path.Combine(gameRoot, "BepInEx", "plugins", "ConfigurationManager", "preserved.cfg"))
            .Should().Be("preserved-current");
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
}

