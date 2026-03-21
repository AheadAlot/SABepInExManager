using Avalonia.Controls;
using SABepInExManager.Services;
using SABepInExManager.ViewModels;

namespace SABepInExManager.Views;

public partial class DiagnosticsPageView : UserControl
{
    private readonly DialogService _dialogService = new();

    public DiagnosticsPageView()
    {
        InitializeComponent();
    }

    private async void OnFixIssueClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not DiagnosticsPageViewModel vm
            || sender is not Button { Tag: DiagnosticIssueItemViewModel issue })
        {
            return;
        }

        if (issue.IsDangerousFix)
        {
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
                    WarningHint = "将移除 BepInEx 目录及 doorstop_config.ini、winhttp.dll 等文件。",
                });

            if (!confirmed)
            {
                return;
            }
        }

        await vm.FixIssueAsync(issue);
    }
}

