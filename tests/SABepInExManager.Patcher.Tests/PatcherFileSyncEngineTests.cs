using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Tests.Common;

namespace SABepInExManager.Patcher.Tests;

public class PatcherFileSyncEngineTests
{
    [Property(MaxTest = 40)]
    public void SyncSingleMod_ShouldDeleteRemovedWhitelistedFile(NonEmptyString nameSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        var fileName = ToSafeFileName(nameSeed.Get) + ".dll";
        fx.WriteText($"game/BepInEx/plugins/old/{fileName}", "old-content");

        var oldState = new PatcherModState
        {
            Signature = "old-sig",
            Files = new List<string> { $"plugins/old/{fileName}" },
        };

        _ = PatcherFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries: [],
            oldModState: oldState,
            now: DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "plugins", "old", fileName)).Should().BeFalse();
    }

    [Property(MaxTest = 40)]
    public void SyncSingleMod_ShouldDeleteOldRemovedFileAndCleanupEmptyDirectories(
        NonEmptyString oldFileSeed,
        NonEmptyString newFileSeed,
        NonEmptyString newContentSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var sourceRoot = fx.EnsureDirectory("mods", "m1");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/plugins/self.dll", "self");

        var oldFileName = ToSafeFileName(oldFileSeed.Get) + ".dll";
        var newFileName = ToSafeFileName(newFileSeed.Get) + ".dll";
        var newContent = newContentSeed.Get;

        var oldFile = fx.WriteText($"game/BepInEx/plugins/Old/{oldFileName}", "old");
        oldFile.Should().NotBeNullOrWhiteSpace();

        var sourceNew = fx.WriteText($"mods/m1/{newFileName}", newContent);
        var entries = new List<ManagedFileEntry>
        {
            new()
            {
                ModId = "m1",
                SourcePath = sourceNew,
                TargetRelativePath = $"plugins/{newFileName}",
            },
        };

        var oldState = new PatcherModState
        {
            Signature = "old-sig",
            Files = new List<string> { $"plugins/Old/{oldFileName}" },
        };

        var newState = PatcherFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries,
            oldState,
            DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "plugins", "Old", oldFileName)).Should().BeFalse();
        Directory.Exists(Path.Combine(gameRoot, "plugins", "Old")).Should().BeFalse();
        File.ReadAllText(Path.Combine(gameRoot, "plugins", newFileName)).Should().Be(newContent);
        newState.Files.Should().ContainSingle().Which.Should().Be($"plugins/{newFileName}");
    }

    [Property(MaxTest = 40)]
    public void SyncSingleMod_ShouldNotDeleteProtectedConfigurationManagerFiles(
        NonEmptyString protectedFileSeed,
        NonEmptyString contentSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        var protectedFile = ToSafeFileName(protectedFileSeed.Get) + ".cfg";
        var content = contentSeed.Get;

        var protectedPath = fx.WriteText($"game/BepInEx/plugins/ConfigurationManager/{protectedFile}", content);
        protectedPath.Should().NotBeNullOrWhiteSpace();

        var oldState = new PatcherModState
        {
            Signature = "old-sig",
            Files = new List<string> { $"plugins/ConfigurationManager/{protectedFile}" },
        };

        _ = PatcherFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries: [],
            oldModState: oldState,
            now: DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "plugins", "ConfigurationManager", protectedFile)).Should().BeTrue();
    }

    [Property(MaxTest = 40)]
    public void SyncSingleMod_ShouldIgnoreNonWhitelistedOldFiles(NonEmptyString coreFileSeed, NonEmptyString contentSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        var coreFile = ToSafeFileName(coreFileSeed.Get) + ".dll";
        var content = contentSeed.Get;

        fx.WriteText($"game/BepInEx/core/{coreFile}", content);

        var oldState = new PatcherModState
        {
            Signature = "old-sig",
            Files = new List<string> { $"core/{coreFile}" },
        };

        _ = PatcherFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries: [],
            oldModState: oldState,
            now: DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "core", coreFile)).Should().BeTrue();
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

