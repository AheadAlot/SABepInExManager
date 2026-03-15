namespace SABepInExManager.ViewModels;

public class ModsPageViewModel : ViewModelBase
{
    public ModsPageViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
    }

    public HomePageViewModel HomePage { get; }
}

