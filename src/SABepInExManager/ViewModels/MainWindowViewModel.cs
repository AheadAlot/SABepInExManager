using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SABepInExManager.Models;
using SABepInExManager.Services;

namespace SABepInExManager.ViewModels;

public class HomePageViewModel : ViewModelBase
{
    private const int MaxLogEntries = 5000;

    private readonly ConfigService _configService = new();
    private readonly WorkshopService _workshopService = new();
    private readonly ModApplyService _modApplyService = new();
    private readonly BepInExService _bepInExService = new();
    private readonly SteamLocatorService _steamLocatorService = new();

    private string _gameRootPath = string.Empty;
    private string _workshopContentPath = string.Empty;
    private WorkshopModInfo? _selectedMod;
    private bool _isBepInExInstalled;
    private bool _isRefreshingMods;
    private int _conflictedModCount;
    private DateTimeOffset? _lastScannedAt;

    public HomePageViewModel()
    {
        RefreshModsCommand = new AsyncRelayCommand(RefreshModsByUserAsync);
        DetectGameRootPathCommand = new AsyncRelayCommand(DetectGameRootPathAsync);
        DetectWorkshopPathCommand = new RelayCommand(DetectWorkshopPath);
        CreateBaselineCommand = new AsyncRelayCommand(CreateBaselineAsync);
        RestoreBaselineCommand = new AsyncRelayCommand(RestoreBaselineAsync);
        PreviewConflictsCommand = new RelayCommand(PreviewConflicts);
        MoveSelectedUpCommand = new RelayCommand(MoveSelectedUp);
        MoveSelectedDownCommand = new RelayCommand(MoveSelectedDown);
        InstallBepInExCommand = new AsyncRelayCommand(InstallBepInExAsync);
        OpenSelectedModFolderCommand = new RelayCommand(OpenSelectedModFolder);
        ClearLogsCommand = new RelayCommand(ClearLogs);

        Mods.CollectionChanged += OnModsCollectionChanged;
        Logs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(LatestLogMessage));
            SyncRecentLogs();
        };
        SyncRecentLogs();
    }

    public ObservableCollection<WorkshopModInfo> Mods { get; } = new();
    public ObservableCollection<LogEntry> Logs { get; } =
    [
        new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Message = "就绪。",
        },
    ];

    public ObservableCollection<LogEntry> RecentLogs { get; } = new();

    public string GameRootPath
    {
        get => _gameRootPath;
        set
        {
            if (SetProperty(ref _gameRootPath, value))
            {
                OnPropertyChanged(nameof(DisplayGameRootPath));
                OnPropertyChanged(nameof(BaselineDirectoryPath));
                CheckBepInExStatus();
            }
        }
    }

    public string WorkshopContentPath
    {
        get => _workshopContentPath;
        set
        {
            if (SetProperty(ref _workshopContentPath, value))
            {
                OnPropertyChanged(nameof(DisplayWorkshopContentPath));
            }
        }
    }

    public WorkshopModInfo? SelectedMod
    {
        get => _selectedMod;
        set => SetProperty(ref _selectedMod, value);
    }

    public bool IsBepInExInstalled
    {
        get => _isBepInExInstalled;
        private set
        {
            if (!SetProperty(ref _isBepInExInstalled, value))
            {
                return;
            }

            OnPropertyChanged(nameof(BepInExStatusText));
            OnPropertyChanged(nameof(InstallBepInExButtonText));
            NotifyDashboardStatusChanged();
        }
    }

    public string BepInExStatusText => IsBepInExInstalled ? "已安装" : "未安装";
    public string EnvironmentAvailabilityText => IsBepInExInstalled ? "可用" : "不可用";
    public string InstallBepInExButtonText => IsBepInExInstalled ? "重装" : "安装";
    public string LatestLogMessage => Logs.Count > 0 ? Logs[^1].Message : "就绪。";
    public int ManagedModCount => Mods.Count;
    public int EnabledModCount => Mods.Count(m => m.IsEnabled);
    public int DisabledModCount => Math.Max(0, ManagedModCount - EnabledModCount);
    public bool HasConflicts => ConflictedModCount > 0;
    public string LastScanTimeText => _lastScannedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未扫描";
    public string DisplayGameRootPath => string.IsNullOrWhiteSpace(GameRootPath) ? "未配置" : GameRootPath;
    public string DisplayWorkshopContentPath => string.IsNullOrWhiteSpace(WorkshopContentPath) ? "未配置" : WorkshopContentPath;
    public string BaselineDirectoryPath => string.IsNullOrWhiteSpace(GameRootPath)
        ? "未配置"
        : Path.Combine(GameRootPath, PathConstants.StateRootFolder, PathConstants.BaselineFolder);
    public string CurrentConfigName => "默认配置";
    public string DashboardPrimaryStatusText
    {
        get
        {
            if (!IsBepInExInstalled)
            {
                return "BepInEx 未安装，当前环境不可用";
            }

            if (HasConflicts)
            {
                return $"BepInEx 已安装，检测到 {ConflictedModCount} 个冲突路径";
            }

            return "BepInEx 已安装";
        }
    }

    public string DashboardSecondaryStatusText
    {
        get
        {
            if (!IsBepInExInstalled)
            {
                return "请先安装或修复 BepInEx，随后重新扫描 Mod。";
            }

            if (HasConflicts)
            {
                return $"已检测 {ManagedModCount} 个 Mod，其中 {EnabledModCount} 个已启用。当前存在冲突，建议先处理。";
            }

            return $"已检测 {ManagedModCount} 个 Mod，其中 {EnabledModCount} 个已启用，未发现异常。";
        }
    }

    public int ConflictedModCount
    {
        get => _conflictedModCount;
        private set
        {
            if (!SetProperty(ref _conflictedModCount, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasConflicts));
            NotifyDashboardStatusChanged();
        }
    }

    public IAsyncRelayCommand RefreshModsCommand { get; }
    public IAsyncRelayCommand DetectGameRootPathCommand { get; }
    public IRelayCommand DetectWorkshopPathCommand { get; }
    public IAsyncRelayCommand CreateBaselineCommand { get; }
    public IAsyncRelayCommand RestoreBaselineCommand { get; }
    public IRelayCommand PreviewConflictsCommand { get; }
    public IRelayCommand MoveSelectedUpCommand { get; }
    public IRelayCommand MoveSelectedDownCommand { get; }
    public IAsyncRelayCommand InstallBepInExCommand { get; }
    public IRelayCommand OpenSelectedModFolderCommand { get; }
    public IRelayCommand ClearLogsCommand { get; }

    public async Task InitializeAsync()
    {
        var config = await _configService.LoadAsync();
        GameRootPath = config.GameRootPath ?? string.Empty;
        WorkshopContentPath = config.WorkshopContentPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(GameRootPath))
        {
            await DetectGameRootPathAsync(triggeredByUser: false);
        }

        if (!string.IsNullOrWhiteSpace(GameRootPath) && string.IsNullOrWhiteSpace(WorkshopContentPath))
        {
            WorkshopContentPath = _workshopService.AutoDetectWorkshopPath(GameRootPath) ?? string.Empty;
        }

        // 启动时强制重新检测一次，避免仅依赖属性变更触发导致状态不刷新。
        CheckBepInExStatus();

        await RefreshModsAsync();
    }

    public async Task SaveConfigAsync()
    {
        await _configService.SaveAsync(new AppConfig
        {
            GameRootPath = GameRootPath,
            WorkshopContentPath = WorkshopContentPath,
        });
    }

    public void CheckBepInExStatus()
    {
        IsBepInExInstalled = _bepInExService.IsInstalled(GameRootPath);
    }

    private bool EnsureBepInExInstalledForAction(string actionName)
    {
        CheckBepInExStatus();
        if (IsBepInExInstalled)
        {
            return true;
        }

        AppendLog($"{actionName}失败：未检测到有效的 BepInEx 安装，请先安装或修复。", reset: true);
        return false;
    }

    private async Task RefreshModsByUserAsync()
    {
        try
        {
            ValidateGameRootForActionsOrThrow();
            if (!EnsureBepInExInstalledForAction("刷新列表"))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"刷新列表失败：{ex.Message}", reset: true);
            return;
        }

        await RefreshModsAsync();
    }

    private async Task RefreshModsAsync()
    {
        _isRefreshingMods = true;
        UnsubscribeAllModPropertyChanged();
        Mods.Clear();

        try
        {
            if (!Directory.Exists(WorkshopContentPath))
            {
                AppendLog("工坊目录不存在，跳过扫描。", reset: true);
                return;
            }

            var state = _modApplyService.LoadState(GameRootPath);
            var enabledOrder = state?.EnabledModIds ?? new List<string>();
            var mods = _workshopService.ScanMods(WorkshopContentPath, enabledOrder);

            foreach (var mod in mods)
            {
                Mods.Add(mod);
                mod.PropertyChanged += OnModPropertyChanged;
            }
        }
        finally
        {
            _isRefreshingMods = false;
        }

        SelectedMod = Mods.FirstOrDefault();
        _lastScannedAt = DateTimeOffset.Now;
        OnPropertyChanged(nameof(LastScanTimeText));
        AppendLog($"扫描完成：找到 {Mods.Count} 个可管理 mod。", reset: true);
        UpdateModSummary();
        PersistEnabledState();
        await SaveConfigAsync();
    }

    private void DetectWorkshopPath()
    {
        WorkshopContentPath = _workshopService.AutoDetectWorkshopPath(GameRootPath) ?? WorkshopContentPath;
        AppendLog(string.IsNullOrWhiteSpace(WorkshopContentPath)
            ? "未自动探测到工坊目录，请手动选择。"
            : $"已自动探测工坊目录：{WorkshopContentPath}");
    }

    private async Task DetectGameRootPathAsync()
    {
        await DetectGameRootPathAsync(triggeredByUser: true);
    }

    private async Task DetectGameRootPathAsync(bool triggeredByUser)
    {
        try
        {
            var result = _steamLocatorService.TryDetectGameRoot(PathConstants.WorkshopAppId);
            if (!result.Success || string.IsNullOrWhiteSpace(result.GameRootPath))
            {
                if (triggeredByUser)
                {
                    AppendLog($"自动探测游戏根目录失败：{result.Message}", reset: true);
                }

                return;
            }

            GameRootPath = result.GameRootPath;
            AppendLog($"已自动探测游戏根目录：{GameRootPath}", reset: triggeredByUser);

            if (result.Candidates.Count > 1)
            {
                AppendLog($"检测到 {result.Candidates.Count} 个候选路径，已按 manifest 最新时间选择：");
                foreach (var candidate in result.Candidates
                             .OrderByDescending(c => c.ManifestLastWriteTime)
                             .Take(5))
                {
                    AppendLog($"- {candidate.GameRootPath} | manifest: {candidate.ManifestPath}");
                }
            }

            if (string.IsNullOrWhiteSpace(WorkshopContentPath))
            {
                var autoWorkshopPath = _workshopService.AutoDetectWorkshopPath(GameRootPath);
                if (!string.IsNullOrWhiteSpace(autoWorkshopPath))
                {
                    WorkshopContentPath = autoWorkshopPath;
                    AppendLog($"已联动自动探测工坊目录：{WorkshopContentPath}");
                }
            }

            await SaveConfigAsync();
        }
        catch (Exception ex)
        {
            if (triggeredByUser)
            {
                AppendLog($"自动探测游戏根目录异常：{ex.Message}", reset: true);
            }
        }
    }

    private async Task CreateBaselineAsync()
    {
        try
        {
            ValidateGameRootForActionsOrThrow();
            if (!EnsureBepInExInstalledForAction("创建备份"))
            {
                return;
            }

            _modApplyService.CreateOrUpdateBaseline(GameRootPath);
            AppendLog("Baseline 已创建/更新。", reset: true);
        }
        catch (Exception ex)
        {
            AppendLog($"创建备份失败：{ex.Message}", reset: true);
        }

        await SaveConfigAsync();
    }

    private async Task RestoreBaselineAsync()
    {
        try
        {
            ValidateGameRootForActionsOrThrow();
            if (!EnsureBepInExInstalledForAction("恢复备份"))
            {
                return;
            }

            _modApplyService.RestoreBaseline(GameRootPath);
            AppendLog("已恢复备份。", reset: true);
        }
        catch (Exception ex)
        {
            AppendLog($"恢复备份失败：{ex.Message}", reset: true);
        }

        await SaveConfigAsync();
    }

    private void PreviewConflicts()
    {
        try
        {
            ValidateGameRootForActionsOrThrow();
            if (!EnsureBepInExInstalledForAction("冲突预览"))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"冲突预览失败：{ex.Message}", reset: true);
            return;
        }

        var enabled = GetEnabledModsInOrder();
        var conflicts = _workshopService.BuildConflicts(enabled);

        if (conflicts.Count == 0)
        {
            AppendLog("未发现冲突文件。", reset: true);
            return;
        }

        AppendLog($"检测到 {conflicts.Count} 个冲突路径（仅展示前 30 条）：", reset: true);
        foreach (var item in conflicts.Take(30))
        {
            AppendLog($"- {item.RelativePath} | 冲突: {string.Join(", ", item.ModIds)} | 最终生效: {item.WinnerModId}");
        }
    }

    private void MoveSelectedUp()
    {
        if (SelectedMod is null)
        {
            return;
        }

        var index = Mods.IndexOf(SelectedMod);
        if (index <= 0)
        {
            return;
        }

        Mods.Move(index, index - 1);
    }

    private void MoveSelectedDown()
    {
        if (SelectedMod is null)
        {
            return;
        }

        var index = Mods.IndexOf(SelectedMod);
        if (index < 0 || index >= Mods.Count - 1)
        {
            return;
        }

        Mods.Move(index, index + 1);
    }

    private void OnModsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<WorkshopModInfo>())
            {
                item.PropertyChanged -= OnModPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<WorkshopModInfo>())
            {
                item.PropertyChanged -= OnModPropertyChanged;
                item.PropertyChanged += OnModPropertyChanged;
            }
        }

        if (_isRefreshingMods)
        {
            return;
        }

        OnPropertyChanged(nameof(ManagedModCount));
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(DisabledModCount));
        UpdateModSummary();
        PersistEnabledState();
    }

    private void OnModPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRefreshingMods)
        {
            return;
        }

        if (!string.Equals(e.PropertyName, nameof(WorkshopModInfo.IsEnabled), StringComparison.Ordinal))
        {
            return;
        }

        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(DisabledModCount));
        UpdateModSummary();
        PersistEnabledState();
    }

    private void UnsubscribeAllModPropertyChanged()
    {
        foreach (var mod in Mods)
        {
            mod.PropertyChanged -= OnModPropertyChanged;
        }
    }

    private void PersistEnabledState()
    {
        if (string.IsNullOrWhiteSpace(GameRootPath) || !Directory.Exists(GameRootPath))
        {
            return;
        }

        var existingState = _modApplyService.LoadState(GameRootPath) ?? new AppState();
        existingState.EnabledModIds = GetEnabledModsInOrder().Select(m => m.ModId).ToList();
        existingState.WorkshopContentPath = WorkshopContentPath;
        _modApplyService.SaveState(GameRootPath, existingState);
    }

    private async Task InstallBepInExAsync()
    {
        await InstallBepInExInternalAsync(forceReinstall: false);
    }

    public async Task ReinstallBepInExAsync()
    {
        await InstallBepInExInternalAsync(forceReinstall: true);
    }

    private async Task InstallBepInExInternalAsync(bool forceReinstall)
    {
        try
        {
            ValidateGameRootOrThrow();
            AppendLog(
                forceReinstall
                    ? "开始重装 BepInEx（先清理旧文件，再重新安装）..."
                    : "开始安装 BepInEx（优先 GitHub，网络异常时自动回退 third_party/BepInEx）...",
                reset: true);

            var result = forceReinstall
                ? await _bepInExService.ReinstallAsync(GameRootPath, message => AppendLog(message))
                : await _bepInExService.InstallFromGitHubAsync(GameRootPath, message => AppendLog(message));
            AppendLog(result.Message);
        }
        catch (Exception ex)
        {
            AppendLog($"{(forceReinstall ? "重装" : "安装")}失败：{ex.Message}");
        }

        CheckBepInExStatus();
        await SaveConfigAsync();
    }

    private void OpenSelectedModFolder()
    {
        if (SelectedMod is null)
        {
            return;
        }

        if (!Directory.Exists(SelectedMod.ModRootPath))
        {
            AppendLog($"目录不存在：{SelectedMod.ModRootPath}", reset: true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedMod.ModRootPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppendLog($"打开目录失败：{ex.Message}", reset: true);
        }
    }

    private void ValidateGameRootOrThrow()
    {
        if (string.IsNullOrWhiteSpace(GameRootPath) || !Directory.Exists(GameRootPath))
        {
            throw new InvalidOperationException("请先选择有效的游戏根目录。 ");
        }
    }

    private void ValidateGameRootForActionsOrThrow()
    {
        ValidateGameRootOrThrow();

        var exeFiles = Directory
            .EnumerateFiles(GameRootPath, "*.exe", SearchOption.TopDirectoryOnly)
            .ToList();
        if (exeFiles.Count == 0)
        {
            throw new InvalidOperationException("所选目录下未找到游戏可执行文件（.exe），请确认是否为真正的游戏根目录。");
        }

        var dataDirs = exeFiles
            .Select(exe => Path.Combine(GameRootPath, $"{Path.GetFileNameWithoutExtension(exe)}_Data"))
            .Where(Directory.Exists)
            .ToList();

        var hasUnityPlayer = File.Exists(Path.Combine(GameRootPath, "UnityPlayer.dll"));
        var hasGameAssembly = File.Exists(Path.Combine(GameRootPath, "GameAssembly.dll"));
        var hasSteamApi = File.Exists(Path.Combine(GameRootPath, "steam_api64.dll"))
                          || File.Exists(Path.Combine(GameRootPath, "steam_api.dll"));
        var hasUnityDataSignature = dataDirs.Any(dir =>
            File.Exists(Path.Combine(dir, "globalgamemanagers"))
            || File.Exists(Path.Combine(dir, "globalgamemanagers.assets"))
            || Directory.Exists(Path.Combine(dir, "Managed")));
        var hasBepInExOrDoorstop = Directory.Exists(Path.Combine(GameRootPath, "BepInEx"))
                                 || File.Exists(Path.Combine(GameRootPath, "doorstop_config.ini"));

        var hasOtherIndicators = hasUnityPlayer
                                 || hasGameAssembly
                                 || hasSteamApi
                                 || hasUnityDataSignature
                                 || hasBepInExOrDoorstop;

        if (!hasOtherIndicators)
        {
            throw new InvalidOperationException(
                "所选目录虽然包含 .exe，但缺少可识别的游戏根目录特征（如 UnityPlayer.dll、GameAssembly.dll、steam_api*.dll、*_Data/Managed、BepInEx 或 doorstop_config.ini），请重新选择正确的游戏根目录。");
        }
    }

    private List<WorkshopModInfo> GetEnabledModsInOrder()
    {
        return Mods.Where(m => m.IsEnabled).ToList();
    }

    private void AppendLog(string message, bool reset = false)
    {
        if (reset)
        {
            Logs.Clear();
        }

        Logs.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Message = message,
        });

        while (Logs.Count > MaxLogEntries)
        {
            Logs.RemoveAt(0);
        }

        OnPropertyChanged(nameof(LatestLogMessage));
        SyncRecentLogs();
    }

    private void ClearLogs()
    {
        Logs.Clear();
        AppendLog("日志已清空。", reset: false);
    }

    private void SyncRecentLogs()
    {
        RecentLogs.Clear();
        foreach (var item in Logs.TakeLast(5))
        {
            RecentLogs.Add(item);
        }
    }

    private void UpdateModSummary()
    {
        OnPropertyChanged(nameof(ManagedModCount));
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(DisabledModCount));

        try
        {
            ConflictedModCount = _workshopService.BuildConflicts(GetEnabledModsInOrder()).Count;
        }
        catch
        {
            ConflictedModCount = 0;
        }

        NotifyDashboardStatusChanged();
    }

    private void NotifyDashboardStatusChanged()
    {
        OnPropertyChanged(nameof(BepInExStatusText));
        OnPropertyChanged(nameof(EnvironmentAvailabilityText));
        OnPropertyChanged(nameof(DashboardPrimaryStatusText));
        OnPropertyChanged(nameof(DashboardSecondaryStatusText));
    }
}

