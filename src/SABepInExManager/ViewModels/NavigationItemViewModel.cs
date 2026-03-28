using FluentIcons.Common;

namespace SABepInExManager.ViewModels;

public class NavigationItemViewModel : ViewModelBase
{
    private bool _hasNotificationDot;

    public NavigationItemViewModel(string key, string title, Symbol icon, ViewModelBase pageViewModel)
    {
        Key = key;
        Title = title;
        Icon = icon;
        PageViewModel = pageViewModel;
    }

    public string Key { get; }
    public string Title { get; }
    public Symbol Icon { get; }
    public ViewModelBase PageViewModel { get; }

    public bool HasNotificationDot
    {
        get => _hasNotificationDot;
        set => SetProperty(ref _hasNotificationDot, value);
    }
}

