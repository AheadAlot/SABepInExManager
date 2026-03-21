using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SABepInExManager.Models;
using SABepInExManager.Services;

namespace SABepInExManager.ViewModels;

public class DiagnosticsPageViewModel : ViewModelBase
{
    private readonly HomePageViewModel _homePage;
    private readonly BepInExService _bepInExService = new();

    private bool _hasRunDiagnostics;
    private bool _isInstalled;
    private int _expectedFileCount;
    private int _matchedFileCount;
    private string _bepInExVersionText = "未知";
    private string _configurationManagerVersionText = "未安装";
    private string _doorstopVersionText = "未知";
    private string _localVersionText = "未知";
    private string _summaryText = "尚未执行诊断。";
    private bool _hasIssues;
    private string _placeholderMessage = "请先点击“诊断”执行检测。";
    private string _lastActionMessage = string.Empty;

    public DiagnosticsPageViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
        _homePage = homePage;

        RunDiagnosticsCommand = new RelayCommand(RunDiagnostics);

        HomePage.PropertyChanged += OnHomePagePropertyChanged;
    }

    public HomePageViewModel HomePage { get; }

    public ObservableCollection<DiagnosticIssueItemViewModel> Issues { get; } = new();

    public IRelayCommand RunDiagnosticsCommand { get; }

    public bool HasRunDiagnostics
    {
        get => _hasRunDiagnostics;
        private set
        {
            if (!SetProperty(ref _hasRunDiagnostics, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowIssueList));
            OnPropertyChanged(nameof(ShowEmptyIssueState));
            OnPropertyChanged(nameof(ShowPlaceholderMessage));
        }
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        private set
        {
            if (!SetProperty(ref _isInstalled, value))
            {
                return;
            }

            OnPropertyChanged(nameof(InstallStateText));
        }
    }

    public string InstallStateText => IsInstalled ? "正常" : "异常";

    public int ExpectedFileCount
    {
        get => _expectedFileCount;
        private set
        {
            if (!SetProperty(ref _expectedFileCount, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FileMatchText));
        }
    }

    public int MatchedFileCount
    {
        get => _matchedFileCount;
        private set
        {
            if (!SetProperty(ref _matchedFileCount, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FileMatchText));
        }
    }

    public string FileMatchText => $"{MatchedFileCount} / {ExpectedFileCount}";

    public string BepInExVersionText
    {
        get => _bepInExVersionText;
        private set => SetProperty(ref _bepInExVersionText, value);
    }

    public string ConfigurationManagerVersionText
    {
        get => _configurationManagerVersionText;
        private set => SetProperty(ref _configurationManagerVersionText, value);
    }

    public string DoorstopVersionText
    {
        get => _doorstopVersionText;
        private set => SetProperty(ref _doorstopVersionText, value);
    }

    public string LocalVersionText
    {
        get => _localVersionText;
        private set => SetProperty(ref _localVersionText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public bool HasIssues
    {
        get => _hasIssues;
        private set
        {
            if (!SetProperty(ref _hasIssues, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowIssueList));
            OnPropertyChanged(nameof(ShowEmptyIssueState));
        }
    }

    public bool ShowIssueList => HasRunDiagnostics && HasIssues;
    public bool ShowEmptyIssueState => HasRunDiagnostics && !HasIssues;
    public bool ShowPlaceholderMessage => !HasRunDiagnostics || ShowEmptyIssueState;

    public string PlaceholderMessage
    {
        get => _placeholderMessage;
        private set => SetProperty(ref _placeholderMessage, value);
    }

    public string LastActionMessage
    {
        get => _lastActionMessage;
        private set => SetProperty(ref _lastActionMessage, value);
    }

    private void OnHomePagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(HomePageViewModel.GameRootPath), StringComparison.Ordinal))
        {
            return;
        }

        ResetResult();
    }

    private void RunDiagnostics()
    {
        var result = _bepInExService.RunDiagnostics(HomePage.GameRootPath);

        HasRunDiagnostics = true;
        IsInstalled = result.IsInstalled;
        ExpectedFileCount = result.ExpectedFileCount;
        MatchedFileCount = result.MatchedFileCount;
        BepInExVersionText = string.IsNullOrWhiteSpace(result.BepInExVersionText) ? "未知" : result.BepInExVersionText;
        ConfigurationManagerVersionText = string.IsNullOrWhiteSpace(result.ConfigurationManagerVersionText) ? "未安装" : result.ConfigurationManagerVersionText;
        DoorstopVersionText = string.IsNullOrWhiteSpace(result.DoorstopVersionText) ? "未知" : result.DoorstopVersionText;
        LocalVersionText = string.IsNullOrWhiteSpace(result.LocalVersionText) ? "未知" : result.LocalVersionText;

        Issues.Clear();
        foreach (var issue in result.Issues)
        {
            Issues.Add(new DiagnosticIssueItemViewModel(issue));
        }

        HasIssues = Issues.Count > 0;
        SummaryText = HasIssues
            ? $"诊断完成：检测到 {Issues.Count} 个问题。"
            : "诊断完成：未发现问题。";
        HomePage.Logs.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Message = SummaryText,
        });

        PlaceholderMessage = HasIssues ? "" : "未发现异常项目。";
        LastActionMessage = string.Empty;
    }

    public async Task FixIssueAsync(DiagnosticIssueItemViewModel? issue)
    {
        if (issue is null)
        {
            return;
        }

        try
        {
            if (string.Equals(issue.Key, "missing_files", StringComparison.Ordinal))
            {
                var result = _bepInExService.RepairMissingFilesFromReference(HomePage.GameRootPath);
                LastActionMessage = result.Message;
                HomePage.Logs.Add(new LogEntry
                {
                    Timestamp = DateTimeOffset.Now,
                    Message = $"诊断修复：{result.Message}",
                });

                if (result.Success)
                {
                    RunDiagnostics();
                }

                return;
            }

            if (issue.IsDangerousFix)
            {
                await HomePage.ReinstallBepInExAsync();
                LastActionMessage = "已执行重装 BepInEx 修复。";
                RunDiagnostics();
                return;
            }

            LastActionMessage = "当前问题不支持自动修复。";
        }
        catch (Exception ex)
        {
            LastActionMessage = $"修复失败：{ex.Message}";
            HomePage.Logs.Add(new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Message = $"诊断修复失败：{ex.Message}",
            });
        }
    }

    private void ResetResult()
    {
        HasRunDiagnostics = false;
        IsInstalled = false;
        ExpectedFileCount = 0;
        MatchedFileCount = 0;
        BepInExVersionText = "未知";
        ConfigurationManagerVersionText = "未安装";
        DoorstopVersionText = "未知";
        LocalVersionText = "未知";
        SummaryText = "游戏根目录已变更，请重新执行诊断。";
        PlaceholderMessage = "请先点击“诊断”执行检测。";
        LastActionMessage = string.Empty;
        Issues.Clear();
        HasIssues = false;
    }
}

