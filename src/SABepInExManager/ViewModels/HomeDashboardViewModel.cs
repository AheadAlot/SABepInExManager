namespace SABepInExManager.ViewModels;

public class HomeDashboardViewModel : ViewModelBase
{
    public HomeDashboardViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
    }

    public HomePageViewModel HomePage { get; }
}

