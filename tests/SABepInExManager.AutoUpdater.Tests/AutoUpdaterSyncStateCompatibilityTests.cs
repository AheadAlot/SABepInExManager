using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using System.Text.Json;
using SABepInExManager.Core.Models;

namespace SABepInExManager.AutoUpdater.Tests;

public class AutoUpdaterSyncStateCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    [Property(MaxTest = 40)]
    public void Deserialize_OldStateJson_WithoutCachedFiles_ShouldSucceed(
        NonEmptyString appIdSeed,
        NonEmptyString modIdSeed,
        NonEmptyString signatureSeed,
        NonEmptyString fileSeed)
    {
        var appId = ToSafeToken(appIdSeed.Get);
        var modId = ToSafeToken(modIdSeed.Get);
        var signature = ToSafeToken(signatureSeed.Get);
        var file = ToSafeToken(fileSeed.Get) + ".dll";

        var oldJson =
            $$"""
            {
              "AppId": "{{appId}}",
              "LastRunAt": "2026-01-01T00:00:00+00:00",
              "Mods": {
                "{{modId}}": {
                  "Signature": "{{signature}}",
                  "Files": ["plugins/{{file}}"],
                  "LastSyncedAt": "2026-01-01T00:00:00+00:00"
                }
              }
            }
            """;

        var state = JsonSerializer.Deserialize<AutoUpdaterSyncState>(oldJson, JsonOptions);

        state.Should().NotBeNull();
        state!.Mods.Should().ContainKey(modId);
        state.Mods[modId].Signature.Should().Be(signature);
        state.Mods[modId].Files.Should().ContainSingle($"plugins/{file}");
        state.Mods[modId].CachedFiles.Should().NotBeNull();
        state.Mods[modId].CachedFiles.Should().BeEmpty();
    }

    [Property(MaxTest = 40)]
    public void SerializeThenDeserialize_NewStateJson_WithCachedFiles_ShouldRoundTrip(
        NonEmptyString modIdSeed,
        NonEmptyString hashSeed,
        PositiveInt lengthSeed)
    {
        var now = DateTimeOffset.UtcNow;
        var modId = ToSafeToken(modIdSeed.Get);
        var hash = ToSafeToken(hashSeed.Get);
        var length = Math.Max(1, lengthSeed.Get);

        var state = new AutoUpdaterSyncState
        {
            AppId = "480",
            LastRunAt = now,
            Mods = new Dictionary<string, AutoUpdaterModState>(StringComparer.OrdinalIgnoreCase)
            {
                [modId] = new AutoUpdaterModState
                {
                    Signature = "SIG",
                    Files = ["plugins/a.dll"],
                    LastSyncedAt = now,
                    CachedFiles = new Dictionary<string, AutoUpdaterCachedFileState>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["plugins/a.dll"] = new AutoUpdaterCachedFileState
                        {
                            SourcePath = @"D:\mods\m1\a.dll",
                            IsDirectory = false,
                            Length = length,
                            LastWriteTimeUtc = now,
                            ContentHash = hash,
                        },
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<AutoUpdaterSyncState>(json, JsonOptions);

        roundTrip.Should().NotBeNull();
        roundTrip!.Mods.Should().ContainKey(modId);

        var mod = roundTrip.Mods[modId];
        mod.Signature.Should().Be("SIG");
        mod.Files.Should().ContainSingle("plugins/a.dll");
        mod.CachedFiles.Should().ContainKey("plugins/a.dll");
        mod.CachedFiles["plugins/a.dll"].ContentHash.Should().Be(hash);
        mod.CachedFiles["plugins/a.dll"].Length.Should().Be(length);
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

