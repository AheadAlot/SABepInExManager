namespace SABepInExManager.ViewModels;

public class NavigationItemViewModel : ViewModelBase
{
    public NavigationItemViewModel(string key, string title, string icon, ViewModelBase pageViewModel)
    {
        Key = key;
        Title = title;
        Icon = icon;
        PageViewModel = pageViewModel;
    }

    public string Key { get; }
    public string Title { get; }
    public string Icon { get; }
    public ViewModelBase PageViewModel { get; }
}

