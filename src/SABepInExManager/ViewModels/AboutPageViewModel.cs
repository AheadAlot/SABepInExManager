using SABepInExManager.Models;
using System.Collections.Generic;

namespace SABepInExManager.ViewModels;

public class AboutPageViewModel : ViewModelBase
{
    public string AppName => AppMetadata.Name;
    public string AppVersion => AppMetadata.Version;
    public string Description => AppMetadata.Description;
    public string RepositoryUrl => AppMetadata.RepositoryUrl;
    public string License => AppMetadata.License;

    public IReadOnlyList<ThirdPartyComponentInfo> ThirdPartyItems { get; } =
    [
        new("BepInEx", "LGPL-2.1", "BepInEx 前置框架"),
        new("BepInEx.ConfigurationManager", "LGPL-3.0", "BepInEx 配置管理器")
    ];
}

public sealed class ThirdPartyComponentInfo(string name, string license, string info)
{
    public string Name { get; } = name;
    public string License { get; } = license;
    public string Info { get; } = info;
}

