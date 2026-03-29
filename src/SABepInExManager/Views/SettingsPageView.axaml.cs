using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SABepInExManager.Models;
using SABepInExManager.Services;
using SABepInExManager.ViewModels;
using System.Diagnostics;
using System.Linq;

namespace SABepInExManager.Views;

public partial class SettingsPageView : UserControl
{
    private readonly DialogService _dialogService = new();

    public SettingsPageView()
    {
        InitializeComponent();
    }

    private async void OnPickGameRootFolderClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel vm)
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
            vm.HomePage.GameRootPath = folder.Path.LocalPath;
        }
    }

    private async void OnPickWorkshopFolderClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel vm)
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
            vm.HomePage.WorkshopContentPath = folder.Path.LocalPath;
        }
    }

    private async void OnDeleteAllBackupsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel vm)
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            owner,
            new ConfirmDialogOptions
            {
                Title = "确认删除所有备份",
                Message = "将永久删除当前游戏目录下的所有备份快照。该操作不可撤销，是否继续？",
                ConfirmText = "确认删除",
                IsDangerous = true,
                WarningHint = "删除后无法恢复，请先确认已有外部备份。"
            });

        if (!confirmed)
        {
            return;
        }

        await vm.HomePage.DeleteAllBaselineSnapshotsAsync();
    }

    private async void OnReinstallBepInExClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel vm)
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            owner,
            new ConfirmDialogOptions
            {
                Title = "确认重装 BepInEx",
                Message = "将删除当前游戏目录中的 BepInEx 相关目录和文件，然后重新安装。该操作存在风险，是否继续？",
                ConfirmText = "确认重装",
                IsDangerous = true,
                WarningHint = "将移除 BepInEx 目录及 doorstop_config.ini、winhttp.dll 等文件。"
            });

        if (!confirmed)
        {
            return;
        }

        await vm.HomePage.ReinstallBepInExAsync();
    }

    private async void OnClearModVersionCacheClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel vm)
        {
            return;
        }

        await vm.HomePage.ClearModVersionCacheAsync();
    }

    private async void OnUpdateAutoUpdaterClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel vm)
        {
            return;
        }

        await vm.HomePage.UpdateAutoUpdaterAsync();
    }

    private void OnOpenReleasePageClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"{AppMetadata.RepositoryUrl}/releases",
                UseShellExecute = true,
            });
        }
        catch
        {
            // no-op
        }
    }
}

