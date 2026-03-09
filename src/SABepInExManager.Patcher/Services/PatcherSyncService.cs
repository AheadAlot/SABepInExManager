using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx.Logging;
using SABepInExManager.Core.Constants;
using SABepInExManager.Core.Models;
using SABepInExManager.Core.Services;
using SABepInExManager.Patcher.Models;

namespace SABepInExManager.Patcher.Services;

public class PatcherSyncService
{
    private const string PatcherStateFolder = "SABepInExManager.Patcher";
    private const string PatcherStateFileName = "state.json";
    private const string BackupSuffix = ".bak";
    private const string PreservedPluginDirectory = "ConfigurationManager";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly ManualLogSource _logger;
    private readonly WorkshopPathResolver _workshopPathResolver = new();
    private readonly WorkshopModDiscoveryService _modDiscoveryService = new();
    private readonly ManagedFileManifestService _managedFileManifestService = new();
    private readonly SignatureService _signatureService = new();

    public PatcherSyncService(ManualLogSource logger)
    {
        _logger = logger;
    }

    public void Run()
    {
        if (GetGameRoot() is not string gameRoot || string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            _logger.LogWarning("[Patcher] 无法定位游戏根目录，跳过。");
            return;
        }

        var appState = LoadAppState(gameRoot!);
        var enabledOrder = appState?.EnabledModIds
                           ?.Where(id => !string.IsNullOrWhiteSpace(id))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .ToList()
                       ?? [];

        if (enabledOrder.Count == 0)
        {
            _logger.LogInfo("[Patcher] 未检测到已启用模组，跳过。");
            return;
        }

        var detectedWorkshopRoot = ResolveWorkshopRoot(gameRoot, appState);
        if (string.IsNullOrWhiteSpace(detectedWorkshopRoot) || !Directory.Exists(detectedWorkshopRoot))
        {
            _logger.LogWarning($"[Patcher] Workshop 目录无效: {detectedWorkshopRoot}");
            return;
        }

        var workshopRoot = detectedWorkshopRoot;

        var discovered = _modDiscoveryService.ScanMods(workshopRoot!, enabledOrder);
        var discoveredMap = discovered.ToDictionary(x => x.ModId, x => x, StringComparer.OrdinalIgnoreCase);
        var enabledMods = enabledOrder
            .Where(id => discoveredMap.ContainsKey(id))
            .Select(id => discoveredMap[id])
            .ToList();

        if (enabledMods.Count == 0)
        {
            _logger.LogInfo("[Patcher] 已启用列表中的模组未在 Workshop 找到可管理条目，跳过。");
            return;
        }

        var patcherState = LoadPatcherState(gameRoot);
        var newSignatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var newEntries = new Dictionary<string, IReadOnlyList<ManagedFileEntry>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < enabledMods.Count; i++)
        {
            var mod = enabledMods[i];
            var entries = _managedFileManifestService.BuildEntries(mod);
            newEntries[mod.ModId] = entries;
            newSignatures[mod.ModId] = _signatureService.BuildManagedSignature(entries);
        }

        var firstChangedIndex = -1;
        for (var i = 0; i < enabledMods.Count; i++)
        {
            var mod = enabledMods[i];
            patcherState.Mods.TryGetValue(mod.ModId, out var oldModState);
            var changed = oldModState == null
                          || !string.Equals(oldModState.Signature, newSignatures[mod.ModId], StringComparison.Ordinal);
            if (changed)
            {
                firstChangedIndex = i;
                break;
            }
        }

        if (firstChangedIndex < 0)
        {
            _logger.LogInfo("[Patcher] 已启用模组无变化，跳过同步。");
            return;
        }

        var gameBepInExRoot = Path.Combine(gameRoot, "BepInEx");
        var selfAssemblyPath = Path.GetFullPath(GetType().Assembly.Location);
        var now = DateTimeOffset.Now;

        for (var i = firstChangedIndex; i < enabledMods.Count; i++)
        {
            var mod = enabledMods[i];
            var entries = newEntries[mod.ModId];
            patcherState.Mods.TryGetValue(mod.ModId, out var oldModState);
            var newState = PatcherFileSyncEngine.SyncSingleMod(
                gameBepInExRoot,
                selfAssemblyPath,
                mod.ModId,
                entries,
                oldModState,
                now,
                logInfo: message => _logger.LogInfo(message),
                logWarning: message => _logger.LogWarning(message));
            newState.Signature = newSignatures[mod.ModId];
            patcherState.Mods[mod.ModId] = newState;

            _logger.LogInfo($"[Patcher] [{mod.ModId}] 已同步 {entries.Count} 个文件。");
        }

        patcherState.AppId = PathConstants.WorkshopAppId;
        patcherState.LastRunAt = now;
        SavePatcherState(gameRoot, patcherState);
        _logger.LogInfo($"[Patcher] 同步完成：从序号 {firstChangedIndex + 1} 开始重放，总启用模组 {enabledMods.Count}。");
    }

    private static string? GetGameRoot()
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return null;
        }

        return Path.GetDirectoryName(exe);
    }

    private string? ResolveWorkshopRoot(string gameRoot, AppStateLite? appState)
    {
        var workshopContentPath = appState?.WorkshopContentPath;
        if (!string.IsNullOrWhiteSpace(workshopContentPath) && Directory.Exists(workshopContentPath))
        {
            return workshopContentPath;
        }

        return _workshopPathResolver.AutoDetectWorkshopPath(gameRoot);
    }

    private static string GetGuiStatePath(string gameRoot)
        => Path.Combine(gameRoot, PathConstants.StateRootFolder, PathConstants.StateFileName);

    private static string GetPatcherStatePath(string gameRoot)
        => Path.Combine(gameRoot, "BepInEx", "config", PatcherStateFolder, PatcherStateFileName);

    private AppStateLite? LoadAppState(string gameRoot)
    {
        var path = GetGuiStatePath(gameRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppStateLite>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Patcher] 读取 GUI state 失败: {ex.Message}");
            return null;
        }
    }

    private PatcherSyncState LoadPatcherState(string gameRoot)
    {
        var path = GetPatcherStatePath(gameRoot);
        if (!File.Exists(path))
        {
            return new PatcherSyncState
            {
                AppId = PathConstants.WorkshopAppId,
                LastRunAt = DateTimeOffset.MinValue,
            };
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PatcherSyncState>(json, JsonOptions) ?? new PatcherSyncState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Patcher] 读取 patcher state 失败，使用空状态继续: {ex.Message}");
            return new PatcherSyncState
            {
                AppId = PathConstants.WorkshopAppId,
                LastRunAt = DateTimeOffset.MinValue,
            };
        }
    }

    private void SavePatcherState(string gameRoot, PatcherSyncState state)
    {
        var path = GetPatcherStatePath(gameRoot);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }
}

