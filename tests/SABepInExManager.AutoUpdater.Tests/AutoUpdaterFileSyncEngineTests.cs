using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Tests.Common;

namespace SABepInExManager.AutoUpdater.Tests;

public class PatcherFileSyncEngineTests
{
    [Property(MaxTest = 40)]
    public void CleanupDisabledMods_ShouldDeleteFilesOfDisabledMod_AndKeepEnabledModState(
        NonEmptyString enabledSeed,
        NonEmptyString disabledSeed,
        NonEmptyString enabledFileSeed,
        NonEmptyString disabledFileSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        var enabledModId = "enabled_" + ToSafeFileName(enabledSeed.Get);
        var disabledModId = "disabled_" + ToSafeFileName(disabledSeed.Get);
        if (string.Equals(enabledModId, disabledModId, StringComparison.OrdinalIgnoreCase))
        {
            disabledModId += "_x";
        }

        var enabledFile = ToSafeFileName(enabledFileSeed.Get) + ".dll";
        var disabledFile = ToSafeFileName(disabledFileSeed.Get) + ".dll";

        fx.WriteText($"game/BepInEx/plugins/{enabledModId}/{enabledFile}", "A");
        fx.WriteText($"game/BepInEx/plugins/{disabledModId}/{disabledFile}", "B");

        var stateMods = new Dictionary<string, AutoUpdaterModState>(StringComparer.OrdinalIgnoreCase)
        {
            [enabledModId] = new AutoUpdaterModState
            {
                Signature = "sig-A",
                Files = new List<string> { $"plugins/{enabledModId}/{enabledFile}" },
            },
            [disabledModId] = new AutoUpdaterModState
            {
                Signature = "sig-B",
                Files = new List<string> { $"plugins/{disabledModId}/{disabledFile}" },
            },
        };

        var removed = AutoUpdaterFileSyncEngine.CleanupDisabledMods(
            gameRoot,
            selfAssemblyPath,
            stateMods,
            enabledModIds: new[] { enabledModId },
            now: DateTimeOffset.UtcNow);

        removed.Should().Be(1);
        File.Exists(Path.Combine(gameRoot, "plugins", disabledModId, disabledFile)).Should().BeFalse();
        File.Exists(Path.Combine(gameRoot, "plugins", enabledModId, enabledFile)).Should().BeTrue();
        stateMods.ContainsKey(enabledModId).Should().BeTrue();
        stateMods.ContainsKey(disabledModId).Should().BeFalse();
    }

    [Property(MaxTest = 40)]
    public void CleanupDisabledMods_WhenEnabledIsEmpty_ShouldRemoveAllStateModsAndTheirFiles(
        NonEmptyString modSeed,
        NonEmptyString fileSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        var modId = "mod_" + ToSafeFileName(modSeed.Get);
        var fileName = ToSafeFileName(fileSeed.Get) + ".dll";
        fx.WriteText($"game/BepInEx/plugins/{modId}/{fileName}", "A");

        var stateMods = new Dictionary<string, AutoUpdaterModState>(StringComparer.OrdinalIgnoreCase)
        {
            [modId] = new AutoUpdaterModState
            {
                Signature = "sig-A",
                Files = new List<string> { $"plugins/{modId}/{fileName}" },
            },
        };

        var removed = AutoUpdaterFileSyncEngine.CleanupDisabledMods(
            gameRoot,
            selfAssemblyPath,
            stateMods,
            enabledModIds: Array.Empty<string>(),
            now: DateTimeOffset.UtcNow);

        removed.Should().Be(1);
        File.Exists(Path.Combine(gameRoot, "plugins", modId, fileName)).Should().BeFalse();
        stateMods.Should().BeEmpty();
    }

    [Property(MaxTest = 40)]
    public void SyncSingleMod_ShouldDeleteRemovedWhitelistedFile(NonEmptyString nameSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        var fileName = ToSafeFileName(nameSeed.Get) + ".dll";
        fx.WriteText($"game/BepInEx/plugins/old/{fileName}", "old-content");

        var oldState = new AutoUpdaterModState
        {
            Signature = "old-sig",
            Files = new List<string> { $"plugins/old/{fileName}" },
        };

        _ = AutoUpdaterFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries: [],
            oldModState: oldState,
            now: DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "plugins", "old", fileName)).Should().BeFalse();
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

        var oldState = new AutoUpdaterModState
        {
            Signature = "old-sig",
            Files = new List<string> { $"plugins/ConfigurationManager/{protectedFile}" },
        };

        _ = AutoUpdaterFileSyncEngine.SyncSingleMod(
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

        var oldState = new AutoUpdaterModState
        {
            Signature = "old-sig",
            Files = new List<string> { $"core/{coreFile}" },
        };

        _ = AutoUpdaterFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries: [],
            oldModState: oldState,
            now: DateTimeOffset.UtcNow);

        File.Exists(Path.Combine(gameRoot, "core", coreFile)).Should().BeTrue();
    }

    [Property(MaxTest = 40)]
    public void SyncSingleMod_ShouldCreateDirectoryForDirectoryEntry(NonEmptyString dirSeed)
    {
        using var fx = new TempDirectoryFixture();
        var gameRoot = fx.EnsureDirectory("game", "BepInEx");
        var selfAssemblyPath = fx.WriteText("game/BepInEx/patchers/self.dll", "self");

        var dirName = ToSafeFileName(dirSeed.Get);
        var entries = new List<ManagedFileEntry>
        {
            new()
            {
                ModId = "m1",
                SourcePath = fx.PathOf("mods", "m1", dirName),
                TargetRelativePath = $"plugins/{dirName}",
                IsDirectory = true,
            },
        };

        _ = AutoUpdaterFileSyncEngine.SyncSingleMod(
            gameRoot,
            selfAssemblyPath,
            "m1",
            entries,
            oldModState: null,
            now: DateTimeOffset.UtcNow);

        Directory.Exists(Path.Combine(gameRoot, "plugins", dirName)).Should().BeTrue();
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

