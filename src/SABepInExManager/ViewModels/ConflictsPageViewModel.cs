namespace SABepInExManager.ViewModels;

public class ConflictsPageViewModel : ViewModelBase
{
    public ConflictsPageViewModel(HomePageViewModel homePage)
    {
        HomePage = homePage;
    }

    public HomePageViewModel HomePage { get; }
}

