using FluentAssertions;
using SABepInExManager.Services;
using SABepInExManager.Tests.Common;
using Xunit;

namespace SABepInExManager.Manager.Tests;

public class BepInExServiceDiagnosticsTests
{
    [Fact]
    public void RunDiagnostics_ShouldReturnInvalidRootIssue_WhenGameRootIsInvalid()
    {
        var service = new BepInExService();

        var result = service.RunDiagnostics(string.Empty, referencePayloadRoot: null);

        result.IsInstalled.Should().BeFalse();
        result.Issues.Should().ContainSingle(x => x.Key == "invalid_game_root");
    }

    [Fact]
    public void RunDiagnostics_ShouldPass_WhenFilesMatchReferenceAndVersionExists()
    {
        using var fx = new TempDirectoryFixture();
        var referenceRoot = CreateReferencePayload(fx, "reference", includeDoorstopVersion: true);
        var gameRoot = CreateReferencePayload(fx, "game", includeDoorstopVersion: true);
        var service = new BepInExService();

        var result = service.RunDiagnostics(gameRoot, referenceRoot);

        result.ExpectedFileCount.Should().BeGreaterThan(0);
        result.MatchedFileCount.Should().Be(result.ExpectedFileCount);
        result.Issues.Should().BeEmpty();
        result.IsInstalled.Should().BeTrue();
        result.LocalVersionText.Should().Be("4.0.0");
    }

    [Fact]
    public void RunDiagnostics_ShouldReportMissingFilesAndInstallFailure_WhenPreloaderMissing()
    {
        using var fx = new TempDirectoryFixture();
        var referenceRoot = CreateReferencePayload(fx, "reference", includeDoorstopVersion: true);
        var gameRoot = CreateReferencePayload(fx, "game", includeDoorstopVersion: true);
        var service = new BepInExService();

        File.Delete(Path.Combine(gameRoot, "BepInEx", "core", "BepInEx.Preloader.dll"));

        var result = service.RunDiagnostics(gameRoot, referenceRoot);

        result.IsInstalled.Should().BeFalse();
        result.Issues.Should().Contain(x => x.Key == "missing_files");
        result.Issues.Should().Contain(x => x.Key == "install_check_failed");
    }

    [Fact]
    public void RunDiagnostics_ShouldReportUnknownVersion_WhenVersionSourcesAreMissing()
    {
        using var fx = new TempDirectoryFixture();
        var referenceRoot = CreateReferencePayload(fx, "reference", includeDoorstopVersion: false);
        var gameRoot = CreateReferencePayload(fx, "game", includeDoorstopVersion: false);
        var service = new BepInExService();

        File.Delete(Path.Combine(gameRoot, "changelog.txt"));

        var result = service.RunDiagnostics(gameRoot, referenceRoot);

        result.Issues.Should().Contain(x => x.Key == "version_unknown");
        result.LocalVersionText.Should().Be("未知");
    }

    private static string CreateReferencePayload(TempDirectoryFixture fx, string rootName, bool includeDoorstopVersion)
    {
        var root = fx.EnsureDirectory(rootName);
        fx.WriteText($"{rootName}/doorstop_config.ini", "target_assembly=BepInEx/core/BepInEx.Preloader.dll");
        fx.WriteText($"{rootName}/changelog.txt", "BepInEx 4.0.0");
        fx.WriteText($"{rootName}/BepInEx/core/BepInEx.Preloader.dll", "preloader");
        fx.WriteText($"{rootName}/BepInEx/core/BepInEx.Harmony.dll", "harmony");
        fx.WriteText($"{rootName}/BepInEx/plugins/Sample.dll", "plugin");

        if (includeDoorstopVersion)
        {
            fx.WriteText($"{rootName}/.doorstop_version", "4.0.0");
        }

        return root;
    }
}

