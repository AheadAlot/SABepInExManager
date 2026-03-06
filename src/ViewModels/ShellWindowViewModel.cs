using System.Threading.Tasks;
using SABepInExManager.Models;

namespace SABepInExManager.ViewModels;

public class ShellWindowViewModel : ViewModelBase
{
    private readonly HomePageViewModel _homePageViewModel = new();

    public ShellWindowViewModel()
    {
    }

    public string AppName => AppMetadata.Name;

    public HomePageViewModel HomePage => _homePageViewModel;

    public async Task InitializeAsync()
    {
        await _homePageViewModel.InitializeAsync();
    }

    public async Task SaveConfigAsync()
    {
        await _homePageViewModel.SaveConfigAsync();
    }
}


