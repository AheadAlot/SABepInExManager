using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SABepInExManager.ViewModels;
using System.Linq;

namespace SABepInExManager.Views;

public partial class SettingsPageView : UserControl
{
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
}

