using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public HomePageViewModel()
    {
        RefreshModsCommand = new AsyncRelayCommand(RefreshModsByUserAsync);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);
        DetectGameRootPathCommand = new AsyncRelayCommand(DetectGameRootPathAsync);
        DetectWorkshopPathCommand = new RelayCommand(DetectWorkshopPath);
        CreateBaselineCommand = new AsyncRelayCommand(CreateBaselineAsync);
        RestoreBaselineCommand = new AsyncRelayCommand(RestoreBaselineAsync);
        PreviewConflictsCommand = new RelayCommand(PreviewConflicts);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync);
        MoveSelectedUpCommand = new RelayCommand(MoveSelectedUp);
        MoveSelectedDownCommand = new RelayCommand(MoveSelectedDown);
        InstallBepInExCommand = new AsyncRelayCommand(InstallBepInExAsync);
        OpenSelectedModFolderCommand = new RelayCommand(OpenSelectedModFolder);
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

    public string GameRootPath
    {
        get => _gameRootPath;
        set
        {
            if (SetProperty(ref _gameRootPath, value))
            {
                CheckBepInExStatus();
            }
        }
    }

    public string WorkshopContentPath
    {
        get => _workshopContentPath;
        set => SetProperty(ref _workshopContentPath, value);
    }

    public WorkshopModInfo? SelectedMod
    {
        get => _selectedMod;
        set => SetProperty(ref _selectedMod, value);
    }

    public bool IsBepInExInstalled
    {
        get => _isBepInExInstalled;
        private set => SetProperty(ref _isBepInExInstalled, value);
    }

    public string BepInExStatusText => IsBepInExInstalled ? "已安装" : "未安装";
    public string InstallBepInExButtonText => IsBepInExInstalled ? "重装" : "安装";

    public IAsyncRelayCommand RefreshModsCommand { get; }
    public IAsyncRelayCommand CheckUpdatesCommand { get; }
    public IAsyncRelayCommand DetectGameRootPathCommand { get; }
    public IRelayCommand DetectWorkshopPathCommand { get; }
    public IAsyncRelayCommand CreateBaselineCommand { get; }
    public IAsyncRelayCommand RestoreBaselineCommand { get; }
    public IRelayCommand PreviewConflictsCommand { get; }
    public IAsyncRelayCommand ApplyCommand { get; }
    public IRelayCommand MoveSelectedUpCommand { get; }
    public IRelayCommand MoveSelectedDownCommand { get; }
    public IAsyncRelayCommand InstallBepInExCommand { get; }
    public IRelayCommand OpenSelectedModFolderCommand { get; }

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
        OnPropertyChanged(nameof(BepInExStatusText));
        OnPropertyChanged(nameof(InstallBepInExButtonText));
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
        Mods.Clear();

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
        }

        SelectedMod = Mods.FirstOrDefault();
        AppendLog($"扫描完成：找到 {Mods.Count} 个可管理 mod。", reset: true);
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

    private async Task ApplyAsync()
    {
        try
        {
            ValidateGameRootForActionsOrThrow();
            if (!EnsureBepInExInstalledForAction("加载已选模组"))
            {
                return;
            }

            var enabledMods = GetEnabledModsInOrder();
            var signatures = BuildEnabledModSignatures(enabledMods);

            _modApplyService.Apply(GameRootPath, enabledMods, signatures);

            AppendLog($"加载完成：已加载 {enabledMods.Count} 个模组。", reset: true);
            foreach (var mod in enabledMods)
            {
                AppendLog($"- {mod.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Apply 失败：{ex.Message}", reset: true);
        }

        await SaveConfigAsync();
    }

    private async Task CheckUpdatesAsync()
    {
        try
        {
            ValidateGameRootForActionsOrThrow();
            if (!EnsureBepInExInstalledForAction("检查更新"))
            {
                return;
            }

            var enabledMods = GetEnabledModsInOrder();
            if (enabledMods.Count == 0)
            {
                AppendLog("没有已勾选的 mod，无需检查更新。", reset: true);
                return;
            }

            var state = _modApplyService.LoadState(GameRootPath);
            var appliedSignatures = state?.AppliedModSignatures
                                  ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var currentSignatures = BuildEnabledModSignatures(enabledMods);

            var changedMods = enabledMods
                .Where(mod => !appliedSignatures.TryGetValue(mod.ModId, out var oldSignature)
                              || !string.Equals(oldSignature, currentSignatures[mod.ModId], StringComparison.Ordinal))
                .ToList();

            if (changedMods.Count == 0)
            {
                AppendLog($"检查完成：{enabledMods.Count} 个已勾选 mod 均无变化。", reset: true);
                return;
            }

            _modApplyService.Apply(GameRootPath, enabledMods, currentSignatures);

            AppendLog($"检查完成：发现 {changedMods.Count} 个 mod 有更新，已按当前优先级重新应用全部已勾选 mod。", reset: true);
            foreach (var mod in changedMods)
            {
                AppendLog($"- {mod.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"检查更新失败：{ex.Message}", reset: true);
        }

        await SaveConfigAsync();
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

    private Dictionary<string, string> BuildEnabledModSignatures(IReadOnlyList<WorkshopModInfo> mods)
    {
        var signatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            signatures[mod.ModId] = _workshopService.BuildManagedSignature(mod.BepInExRootPath);
        }

        return signatures;
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
    }
}

