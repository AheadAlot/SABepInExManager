using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SABepInExManager.Services;
using SABepInExManager.ViewModels;

namespace SABepInExManager.Views;

public partial class HomePageView : UserControl
{
    private ListBox? _logListBox;
    private HomePageViewModel? _boundViewModel;
    private readonly DialogService _dialogService = new();
    private bool _isScrollQueued;

    public HomePageView()
    {
        InitializeComponent();
        _logListBox = this.FindControl<ListBox>("LogListBox");
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        UnbindViewModelEvents();

        if (DataContext is HomePageViewModel vm)
        {
            _boundViewModel = vm;
            _boundViewModel.Logs.CollectionChanged += OnLogsCollectionChanged;
            QueueScrollLogToEnd();
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset)
        {
            QueueScrollLogToEnd();
        }
    }

    private void QueueScrollLogToEnd()
    {
        if (_isScrollQueued)
        {
            return;
        }

        _isScrollQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isScrollQueued = false;
            ScrollLogToEnd();
        }, DispatcherPriority.Background);
    }

    private void ScrollLogToEnd()
    {
        _logListBox ??= this.FindControl<ListBox>("LogListBox");
        if (_logListBox is null || _boundViewModel is null || _boundViewModel.Logs.Count == 0)
        {
            return;
        }

        var last = _boundViewModel.Logs[^1];
        _logListBox.ScrollIntoView(last);
    }

    private void UnbindViewModelEvents()
    {
        if (_boundViewModel is null)
        {
            return;
        }

        _boundViewModel.Logs.CollectionChanged -= OnLogsCollectionChanged;
        _boundViewModel = null;
    }

    private void OnModsListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HomePageViewModel vm)
        {
            vm.OpenSelectedModFolderCommand.Execute(null);
        }
    }

    private async void OnShowModListHelpClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var helpText =
            "1. 只有勾选的 Mod 在点击“应用”时会被处理。\n" +
            "2. 如果存在相同文件，列表靠下的 Mod 会覆盖靠上的 Mod；不重复的文件会保留。\n" +
            "3. 可直接在列表中拖拽条目进行排序（拖到的位置越靠下，优先级越高）。\n" +
            "4. 双击列表项可以直接打开该 Mod 所在目录。\n" +
            "5. 如果 Mod 名称显示为数字，表示该 Mod 缺少 manifest.json；此数字即该 Mod 的 Steam 创意工坊 ID。";

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        await _dialogService.ShowHelpAsync(owner, "可管理 Mod 列表说明", helpText);
    }

    private async void OnPickGameRootFolderClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not HomePageViewModel vm)
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择游戏根目录",
            AllowMultiple = false,
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            vm.GameRootPath = folder.Path.LocalPath;
        }
    }

    private async void OnPickWorkshopFolderClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not HomePageViewModel vm)
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择创意工坊路径",
            AllowMultiple = false,
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            vm.WorkshopContentPath = folder.Path.LocalPath;
        }
    }

    private async void OnRestoreBaselineClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not HomePageViewModel vm)
        {
            return;
        }

        var confirmed = await ShowConfirmDialogAsync(
            new ConfirmDialogOptions
            {
                Title = "确认恢复备份",
                Message = "该操作会覆盖当前游戏目录中的已应用改动，可能导致当前 Mod 应用状态丢失。是否继续？",
                ConfirmText = "确认恢复",
                IsDangerous = true,
                WarningHint = "该操作具有覆盖性，建议先确保游戏目录备份可用。"
            });

        if (confirmed)
        {
            await vm.RestoreBaselineCommand.ExecuteAsync(null);
        }
    }

    private async void OnInstallBepInExClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not HomePageViewModel vm)
        {
            return;
        }

        if (!vm.IsBepInExInstalled)
        {
            await vm.InstallBepInExCommand.ExecuteAsync(null);
            return;
        }

        var confirmed = await ShowConfirmDialogAsync(
            new ConfirmDialogOptions
            {
                Title = "确认重装 BepInEx",
                Message = "将删除当前游戏目录中的 BepInEx 相关目录和文件，然后重新安装。该操作存在风险，是否继续？",
                ConfirmText = "确认重装",
                IsDangerous = true,
                WarningHint = "将移除 BepInEx 目录及 doorstop_config.ini、winhttp.dll 等文件。"
            });

        if (confirmed)
        {
            await vm.ReinstallBepInExAsync();
        }
    }

    private async Task<bool> ShowConfirmDialogAsync(ConfirmDialogOptions options)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return false;
        }

        return await _dialogService.ShowConfirmAsync(owner, options);
    }
}


