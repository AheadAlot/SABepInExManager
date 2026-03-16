using Avalonia.Controls;
using SABepInExManager.Services;
using SABepInExManager.ViewModels;

namespace SABepInExManager.Views;

public partial class BackupPageView : UserControl
{
    private readonly DialogService _dialogService = new();

    public BackupPageView()
    {
        InitializeComponent();
    }

    private async void OnRestoreSnapshotClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not BackupPageViewModel vm
            || sender is not Button { Tag: string folderName }
            || string.IsNullOrWhiteSpace(folderName))
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(owner, new ConfirmDialogOptions
        {
            Title = "确认恢复备份",
            Message = $"将恢复备份目录 {folderName}，该操作会覆盖当前 BepInEx 管理目录中的文件。是否继续？",
            ConfirmText = "确认恢复",
            IsDangerous = true,
            WarningHint = "恢复后当前已应用的改动可能被覆盖。",
        });

        if (!confirmed)
        {
            return;
        }

        await vm.RestoreSnapshotAsync(folderName);
    }

    private async void OnDeleteSnapshotClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not BackupPageViewModel vm
            || sender is not Button { Tag: string folderName }
            || string.IsNullOrWhiteSpace(folderName))
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(owner, new ConfirmDialogOptions
        {
            Title = "确认删除备份",
            Message = $"将永久删除备份目录 {folderName}。该操作不可撤销，是否继续？",
            ConfirmText = "确认删除",
            IsDangerous = true,
            WarningHint = "删除后无法通过该快照进行恢复。",
        });

        if (!confirmed)
        {
            return;
        }

        await vm.DeleteSnapshotAsync(folderName);
    }
}

