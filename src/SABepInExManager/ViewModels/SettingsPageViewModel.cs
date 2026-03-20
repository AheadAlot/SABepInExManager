namespace SABepInExManager.ViewModels;

public class SettingsPageViewModel : ViewModelBase
{
    public SettingsPageViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
    }

    public HomePageViewModel HomePage { get; }
}

