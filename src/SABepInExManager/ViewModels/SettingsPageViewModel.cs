using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SABepInExManager.Models;

namespace SABepInExManager.ViewModels;

public class SettingsPageViewModel : ViewModelBase
{
    private Func<bool, Task>? _checkForUpdatesAction;
    private bool _isCheckingForUpdates;
    private bool _hasAvailableUpdate;
    private string _latestVersionText = "未知";
    private string _lastCheckedText = "未检查";
    private string _updateStatusText = "尚未检查更新。";

    public SettingsPageViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => CanCheckForUpdates);
    }

    public HomePageViewModel HomePage { get; }

    public string CurrentVersionText => AppMetadata.Version;

    public string LatestVersionText
    {
        get => _latestVersionText;
        private set => SetProperty(ref _latestVersionText, value);
    }

    public string LastCheckedText
    {
        get => _lastCheckedText;
        private set => SetProperty(ref _lastCheckedText, value);
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (!SetProperty(ref _isCheckingForUpdates, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanCheckForUpdates));
            CheckForUpdatesCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasAvailableUpdate
    {
        get => _hasAvailableUpdate;
        private set => SetProperty(ref _hasAvailableUpdate, value);
    }

    public bool CanCheckForUpdates => !IsCheckingForUpdates;

    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    public void ConfigureUpdateChecker(Func<bool, Task> updateChecker)
    {
        _checkForUpdatesAction = updateChecker;
    }

    public void SetCheckingState(bool isChecking)
    {
        IsCheckingForUpdates = isChecking;
        if (isChecking)
        {
            UpdateStatusText = "正在检查更新...";
        }
    }

    public void ApplyUpdateResult(AppUpdateCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        IsCheckingForUpdates = false;
        LastCheckedText = result.CheckedAt == DateTimeOffset.MinValue
            ? "未检查"
            : result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss");

        if (result.UpdateInfo is not null)
        {
            LatestVersionText = string.IsNullOrWhiteSpace(result.UpdateInfo.LatestVersion)
                ? "未知"
                : result.UpdateInfo.LatestVersion;
            HasAvailableUpdate = result.UpdateInfo.IsUpdateAvailable;
        }

        if (!result.Succeeded)
        {
            UpdateStatusText = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "检查更新失败。"
                : $"检查更新失败：{result.ErrorMessage}";
            return;
        }

        UpdateStatusText = result.Status switch
        {
            AppUpdateStatus.UpdateAvailable => $"发现新版本：{LatestVersionText}",
            AppUpdateStatus.UpToDate => "当前已是最新版本。",
            _ => "更新状态已刷新。",
        };
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_checkForUpdatesAction is null || IsCheckingForUpdates)
        {
            return;
        }

        await _checkForUpdatesAction(true);
    }
}

