namespace SABepInExManager.Models;

public static class AppMetadata
{
    public const string Name = "学生时代BepInEx模组管理器";
    public static string Version => typeof(AppMetadata).Assembly.GetName().Version?.ToString(3) ?? "未知";
    public const string Description = "《学生时代》创意工坊BepInEx模组管理器";
    public const string RepositoryOwner = "AheadAlot";
    public const string RepositoryName = "SABepInExManager";
    public const string RepositoryUrl = "https://github.com/AheadAlot/SABepInExManager";
    public const string License = "MIT";
}


