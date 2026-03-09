using FluentAssertions;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Tests.Common;
using Xunit;

namespace SABepInExManager.Patcher.Tests;

public class PatcherFileSyncEngineTests
{
    [Fact]
    public void SyncSingleMod_ShouldDeleteOldRemovedFile_AndCleanupEmptyDirectories()
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var sourceRoot = fx.EnsureDirectory("mods", "m1");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/plugins/self.dll", "self");

        var oldFile = fx.WriteText("game/BepInEx/plugins/Old/removed.dll", "old");
        oldFile.Should().NotBeNullOrWhiteSpace();

        var sourceNew = fx.WriteText("mods/m1/new.dll", "new-content");
        var entries = new List<ManagedFileEntry>
        {
            new()
            {
                ModId = "m1",
                SourcePath = sourceNew,
                TargetRelativePath = "plugins/new.dll",
            },
        };

        var oldState = new PatcherModState
        {
            Signature = "old-sig",
            Files = new List<string> { "plugins/Old/removed.dll" },
        };

        var newState = PatcherFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries,
            oldState,
            DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "plugins", "Old", "removed.dll")).Should().BeFalse();
        Directory.Exists(Path.Combine(gameRoot, "plugins", "Old")).Should().BeFalse();
        File.ReadAllText(Path.Combine(gameRoot, "plugins", "new.dll")).Should().Be("new-content");
        newState.Files.Should().ContainSingle().Which.Should().Be("plugins/new.dll");
    }

    [Fact]
    public void SyncSingleMod_ShouldNotDeleteProtectedConfigurationManagerFiles()
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        var protectedPath = fx.WriteText("game/BepInEx/plugins/ConfigurationManager/keep.cfg", "keep");
        protectedPath.Should().NotBeNullOrWhiteSpace();

        var oldState = new PatcherModState
        {
            Signature = "old-sig",
            Files = new List<string> { "plugins/ConfigurationManager/keep.cfg" },
        };

        _ = PatcherFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries: [],
            oldModState: oldState,
            now: DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "plugins", "ConfigurationManager", "keep.cfg")).Should().BeTrue();
    }

    [Fact]
    public void SyncSingleMod_ShouldIgnoreNonWhitelistedOldFiles()
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        fx.WriteText("game/BepInEx/core/not-managed.dll", "core");

        var oldState = new PatcherModState
        {
            Signature = "old-sig",
            Files = new List<string> { "core/not-managed.dll" },
        };

        _ = PatcherFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries: [],
            oldModState: oldState,
            now: DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "core", "not-managed.dll")).Should().BeTrue();
    }
}

