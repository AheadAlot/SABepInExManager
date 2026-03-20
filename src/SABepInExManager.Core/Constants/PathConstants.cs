namespace SABepInExManager.Core.Constants;

public static class PathConstants
{
    public const string WorkshopAppId = "1991040";
    public const string StateRootFolder = ".workshop_bepinex_manager";
    public const string BaselineFolder = "baseline";
    public const string ManagerConfigFolder = "SABepInExManager";
    public const string ManagerStateFolder = "state";
    public const string StateFileName = "state.json";
    public const string ConfigFileName = "config.json";

    public static readonly string[] ManagedBepInExSubDirs = ["plugins", "config", "patchers"];
}

