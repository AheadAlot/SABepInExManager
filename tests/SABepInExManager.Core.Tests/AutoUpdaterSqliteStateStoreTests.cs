using FsCheck;
using FsCheck.Xunit;
using FluentAssertions;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Tests.Common;

namespace SABepInExManager.Core.Tests;

public class AutoUpdaterSqliteStateStoreTests
{
    [Property(MaxTest = 40)]
    public void SaveThenLoad_ShouldRoundTripState(
        NonEmptyString modIdSeed,
        NonEmptyString hashSeed,
        PositiveInt lengthSeed)
    {
        using var fx = new TempDirectoryFixture();
        var dbPath = fx.PathOf("state", "auto_updater_state.db");
        var store = new AutoUpdaterSqliteStateStore();

        var now = DateTimeOffset.UtcNow;
        var modId = ToSafeToken(modIdSeed.Get);
        var hash = ToSafeToken(hashSeed.Get);
        var length = Math.Max(1, lengthSeed.Get);
        var fileA = $"plugins/{modId}_a.dll";
        var fileB = $"plugins/{modId}_b.dll";

        var state = new AutoUpdaterSyncState
        {
            AppId = "1991040",
            LastRunAt = now,
            Mods = new Dictionary<string, AutoUpdaterModState>(StringComparer.OrdinalIgnoreCase)
            {
                [modId] = new AutoUpdaterModState
                {
                    Signature = "sig-" + modId,
                    Files = [fileA, fileB],
                    LastSyncedAt = now,
                    CachedFiles = new Dictionary<string, AutoUpdaterCachedFileState>(StringComparer.OrdinalIgnoreCase)
                    {
                        [fileA] = new AutoUpdaterCachedFileState
                        {
                            SourcePath = fx.PathOf("mods", modId, "a.dll"),
                            IsDirectory = false,
                            Length = length,
                            LastWriteTimeUtc = now,
                            ContentHash = hash,
                        },
                        [fileB] = new AutoUpdaterCachedFileState
                        {
                            SourcePath = fx.PathOf("mods", modId, "b"),
                            IsDirectory = true,
                            Length = 0,
                            LastWriteTimeUtc = DateTimeOffset.MinValue,
                            ContentHash = string.Empty,
                        },
                    },
                },
            },
        };

        store.Save(dbPath, state);
        var loaded = store.Load(dbPath);

        loaded.AppId.Should().Be(state.AppId);
        loaded.Mods.Should().ContainKey(modId);
        loaded.Mods[modId].Signature.Should().Be("sig-" + modId);
        loaded.Mods[modId].Files.Should().ContainInOrder(fileA, fileB);
        loaded.Mods[modId].CachedFiles.Should().ContainKey(fileA);
        loaded.Mods[modId].CachedFiles[fileA].ContentHash.Should().Be(hash);
        loaded.Mods[modId].CachedFiles[fileA].Length.Should().Be(length);
    }

    [Property(MaxTest = 40)]
    public void Save_ShouldReplaceRemovedMods(
        NonEmptyString firstModSeed,
        NonEmptyString secondModSeed,
        NonEmptyString updatedSigSeed)
    {
        using var fx = new TempDirectoryFixture();
        var dbPath = fx.PathOf("state", "auto_updater_state.db");
        var store = new AutoUpdaterSqliteStateStore();
        var firstModId = ToSafeToken(firstModSeed.Get) + "_1";
        var secondModId = ToSafeToken(secondModSeed.Get) + "_2";
        var updatedSig = ToSafeToken(updatedSigSeed.Get);

        var first = new AutoUpdaterSyncState
        {
            AppId = "1991040",
            LastRunAt = DateTimeOffset.UtcNow,
            Mods = new Dictionary<string, AutoUpdaterModState>(StringComparer.OrdinalIgnoreCase)
            {
                [firstModId] = new AutoUpdaterModState { Signature = "sig-" + firstModId },
                [secondModId] = new AutoUpdaterModState { Signature = "sig-" + secondModId },
            },
        };

        store.Save(dbPath, first);

        var second = new AutoUpdaterSyncState
        {
            AppId = "1991040",
            LastRunAt = DateTimeOffset.UtcNow,
            Mods = new Dictionary<string, AutoUpdaterModState>(StringComparer.OrdinalIgnoreCase)
            {
                [secondModId] = new AutoUpdaterModState { Signature = updatedSig },
            },
        };

        store.Save(dbPath, second);
        var loaded = store.Load(dbPath);

        loaded.Mods.Should().NotContainKey(firstModId);
        loaded.Mods.Should().ContainKey(secondModId);
        loaded.Mods[secondModId].Signature.Should().Be(updatedSig);
    }

    private static string ToSafeToken(string seed)
    {
        var chars = seed
            .Where(char.IsLetterOrDigit)
            .Take(16)
            .ToArray();
        return chars.Length == 0 ? "x" : new string(chars);
    }
}
