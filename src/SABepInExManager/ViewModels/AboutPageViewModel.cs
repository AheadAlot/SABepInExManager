using SABepInExManager.Models;

namespace SABepInExManager.ViewModels;

public class AboutPageViewModel : ViewModelBase
{
    public string AppName => AppMetadata.Name;
    public string Description => AppMetadata.Description;
    public string RepositoryUrl => AppMetadata.RepositoryUrl;
    public string License => AppMetadata.License;
}

