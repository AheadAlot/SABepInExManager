using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using SABepInExManager.Services;
using SABepInExManager.ViewModels;

namespace SABepInExManager.Views;

public partial class MainWindow : Window
{
    private readonly DialogService _dialogService = new();

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        if (DataContext is ShellWindowViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is ShellWindowViewModel vm)
        {
            await vm.SaveConfigAsync();
        }
    }

    private void OnModsListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ShellWindowViewModel vm)
        {
            vm.HomePage.OpenSelectedModFolderCommand.Execute(null);
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

        await _dialogService.ShowHelpAsync(this, "可管理 Mod 列表说明", helpText);
    }
}

