using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using SABepInExManager.Models;
using SABepInExManager.Services;

namespace SABepInExManager.ViewModels;

public class ShellWindowViewModel : ViewModelBase
{
    private readonly HomePageViewModel _homePageViewModel = new();
    private readonly HomeDashboardViewModel _homeDashboardViewModel;
    private readonly ModsPageViewModel _modsPageViewModel;
    private readonly ConflictsPageViewModel _conflictsPageViewModel;
    private readonly BackupPageViewModel _backupPageViewModel;
    private readonly DiagnosticsPageViewModel _diagnosticsPageViewModel;
    private readonly LogsPageViewModel _logsPageViewModel;
    private readonly SettingsPageViewModel _settingsPageViewModel;
    private readonly AboutPageViewModel _aboutPageViewModel;
    private readonly IAppUpdateService _appUpdateService = new GitHubReleaseUpdateService();
    private readonly IAppUpdateStateStore _appUpdateStateStore = new AppConfigUpdateStateStore();
    private readonly NavigationItemViewModel _settingsNavigationItem;

    private NavigationItemViewModel? _selectedNavigationItem;
    private ViewModelBase? _currentPageViewModel;
    private string _currentPageTitle = "设置";
    private string _globalStatusText = "就绪。";
    private bool _isCheckingForAppUpdate;

    public ShellWindowViewModel()
    {
        _homeDashboardViewModel = new HomeDashboardViewModel(_homePageViewModel);
        _modsPageViewModel = new ModsPageViewModel(_homePageViewModel);
        _conflictsPageViewModel = new ConflictsPageViewModel(_homePageViewModel);
        _backupPageViewModel = new BackupPageViewModel(_homePageViewModel);
        _diagnosticsPageViewModel = new DiagnosticsPageViewModel(_homePageViewModel);
        _logsPageViewModel = new LogsPageViewModel(_homePageViewModel);
        _settingsPageViewModel = new SettingsPageViewModel(_homePageViewModel);
        _aboutPageViewModel = new AboutPageViewModel();
        _settingsNavigationItem = new NavigationItemViewModel("settings", "设置", Symbol.Settings, _settingsPageViewModel);

        NavigationItems =
        [
            new NavigationItemViewModel("home", "主页", Symbol.Home, _homeDashboardViewModel),
            new NavigationItemViewModel("mods", "Mod 管理", Symbol.PuzzlePiece, _modsPageViewModel),
            new NavigationItemViewModel("conflicts", "冲突预览", Symbol.Warning, _conflictsPageViewModel),
            new NavigationItemViewModel("backup", "备份与恢复", Symbol.FolderOpen, _backupPageViewModel),
            new NavigationItemViewModel("diagnostics", "BepInEx 诊断", Symbol.Search, _diagnosticsPageViewModel),
            new NavigationItemViewModel("logs", "日志", Symbol.Notebook, _logsPageViewModel),
            _settingsNavigationItem,
            new NavigationItemViewModel("about", "关于", Symbol.Info, _aboutPageViewModel),
        ];

        SelectedNavigationItem = NavigationItems[0];
        _homePageViewModel.PropertyChanged += OnHomePagePropertyChanged;
        _settingsPageViewModel.ConfigureUpdateChecker(CheckForAppUpdatesAsync);

        SelectNavigationItemCommand = new RelayCommand<NavigationItemViewModel?>(SelectNavigationItem);
    }

    public string AppName => AppMetadata.Name;
    public string AppDescription => AppMetadata.Description;

    public HomePageViewModel HomePage => _homePageViewModel;

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public NavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (SetProperty(ref _selectedNavigationItem, value) && value is not null)
            {
                CurrentPageTitle = value.Title;
                CurrentPageViewModel = value.PageViewModel;
            }
        }
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => SetProperty(ref _currentPageTitle, value);
    }

    public ViewModelBase? CurrentPageViewModel
    {
        get => _currentPageViewModel;
        private set => SetProperty(ref _currentPageViewModel, value);
    }

    public string GlobalStatusText
    {
        get => _globalStatusText;
        private set => SetProperty(ref _globalStatusText, value);
    }

    public IRelayCommand<NavigationItemViewModel?> SelectNavigationItemCommand { get; }

    public async Task InitializeAsync()
    {
        await _homePageViewModel.InitializeAsync();
        GlobalStatusText = _homePageViewModel.LatestLogMessage;

        await InitializeUpdateStateAsync();
        _ = CheckForAppUpdatesAsync(manualTrigger: false);
    }

    public async Task SaveConfigAsync()
    {
        await _homePageViewModel.SaveConfigAsync();
    }

    private void OnHomePagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(HomePageViewModel.LatestLogMessage), System.StringComparison.Ordinal))
        {
            GlobalStatusText = _homePageViewModel.LatestLogMessage;
        }
    }

    private void SelectNavigationItem(NavigationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedNavigationItem = item;
    }

    private async Task InitializeUpdateStateAsync()
    {
        var cached = await _appUpdateStateStore.LoadLastStateAsync();
        if (cached is null)
        {
            return;
        }

        ApplyAppUpdateResult(cached);
    }

    private async Task CheckForAppUpdatesAsync(bool manualTrigger)
    {
        if (_isCheckingForAppUpdate)
        {
            return;
        }

        _isCheckingForAppUpdate = true;
        _settingsPageViewModel.SetCheckingState(true);

        try
        {
            var result = await _appUpdateService.CheckForUpdatesAsync(manualTrigger);
            await _appUpdateStateStore.SaveLastStateAsync(result);

            var effectiveResult = await _appUpdateStateStore.LoadLastStateAsync() ?? result;
            ApplyAppUpdateResult(effectiveResult);
        }
        catch
        {
            _settingsPageViewModel.SetCheckingState(false);
        }
        finally
        {
            _isCheckingForAppUpdate = false;
        }
    }

    private void ApplyAppUpdateResult(AppUpdateCheckResult result)
    {
        _settingsPageViewModel.ApplyUpdateResult(result);
        _settingsNavigationItem.HasNotificationDot = result.UpdateInfo?.IsUpdateAvailable == true;

        if (!result.Succeeded)
        {
            GlobalStatusText = "更新检查失败（详见设置页）。";
            return;
        }

        GlobalStatusText = result.Status == AppUpdateStatus.UpdateAvailable
            ? $"发现新版本：{result.UpdateInfo?.LatestVersion ?? "未知"}"
            : "当前已是最新版本。";
    }
}


