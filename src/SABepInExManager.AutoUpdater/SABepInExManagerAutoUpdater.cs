using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using SABepInExManager.AutoUpdater.Services;

namespace SABepInExManager.AutoUpdater;

public static class SABepInExManagerAutoUpdater
{
    public static IEnumerable<string> TargetDLLs { get; } = Array.Empty<string>();

    private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("SABepInExManager.AutoUpdater");

    public static void Initialize()
    {
        try
        {
            var syncService = new AutoUpdaterSyncService(Logger);
            syncService.Run();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[AutoUpdater] 初始化失败: {ex}");
        }
    }

    public static void Patch(AssemblyDefinition assembly)
    {
    }

    public static void Finish()
    {
    }
}

