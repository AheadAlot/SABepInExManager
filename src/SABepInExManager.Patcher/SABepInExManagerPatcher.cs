using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using SABepInExManager.Patcher.Services;

namespace SABepInExManager.Patcher;

public static class SABepInExManagerPatcher
{
    public static IEnumerable<string> TargetDLLs { get; } = Array.Empty<string>();

    private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("SABepInExManager.Patcher");

    public static void Initialize()
    {
        try
        {
            var syncService = new PatcherSyncService(Logger);
            syncService.Run();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Patcher] 初始化失败: {ex}");
        }
    }

    public static void Patch(AssemblyDefinition assembly)
    {
    }

    public static void Finish()
    {
    }
}

