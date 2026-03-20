namespace SABepInExManager.ViewModels;

public class LogsPageViewModel : ViewModelBase
{
    public LogsPageViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
    }

    public HomePageViewModel HomePage { get; }
}

