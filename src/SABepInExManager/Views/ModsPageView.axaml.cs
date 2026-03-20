using Avalonia.Controls;
using Avalonia.Input;
using SABepInExManager.ViewModels;

namespace SABepInExManager.Views;

public partial class ModsPageView : UserControl
{
    public ModsPageView()
    {
        InitializeComponent();
    }

    private void OnModsListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ModsPageViewModel vm)
        {
            vm.HomePage.OpenSelectedModFolderCommand.Execute(null);
        }
    }
}

