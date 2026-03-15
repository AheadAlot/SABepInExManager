namespace SABepInExManager.ViewModels;

public class BackupPageViewModel : ViewModelBase
{
    public BackupPageViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
    }

    public HomePageViewModel HomePage { get; }
}

