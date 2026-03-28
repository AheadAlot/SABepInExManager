using FluentAssertions;
using SABepInExManager.Models;
using SABepInExManager.ViewModels;
using Xunit;

namespace SABepInExManager.Manager.Tests;

public class SettingsPageViewModelUpdateTests
{
    [Fact]
    public void ApplyUpdateResult_WhenUpdateAvailable_ShouldSetExpectedStatus()
    {
        var vm = new SettingsPageViewModel(new HomePageViewModel());
        var result = new AppUpdateCheckResult
        {
            Succeeded = true,
            Status = AppUpdateStatus.UpdateAvailable,
            CheckedAt = DateTimeOffset.Now,
            UpdateInfo = new AppUpdateInfo
            {
                LatestVersion = "1.2.3",
                IsUpdateAvailable = true,
            },
        };

        vm.ApplyUpdateResult(result);

        vm.HasAvailableUpdate.Should().BeTrue();
        vm.LatestVersionText.Should().Be("1.2.3");
        vm.UpdateStatusText.Should().Contain("发现新版本");
        vm.LastCheckedText.Should().NotBe("未检查");
    }

    [Fact]
    public void ApplyUpdateResult_WhenFailed_ShouldSetFailureText()
    {
        var vm = new SettingsPageViewModel(new HomePageViewModel());
        var result = new AppUpdateCheckResult
        {
            Succeeded = false,
            Status = AppUpdateStatus.Failed,
            CheckedAt = DateTimeOffset.Now,
            ErrorMessage = "网络异常",
        };

        vm.ApplyUpdateResult(result);

        vm.UpdateStatusText.Should().Contain("检查更新失败");
        vm.UpdateStatusText.Should().Contain("网络异常");
        vm.CanCheckForUpdates.Should().BeTrue();
    }
}

