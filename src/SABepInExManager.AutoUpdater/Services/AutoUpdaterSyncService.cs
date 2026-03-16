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
using SABepInExManager.AutoUpdater.Models;

namespace SABepInExManager.AutoUpdater.Services;

public class AutoUpdaterSyncService
{
    private const string AutoUpdaterStateDbFolder = "SABepInExManager_AutoUpdater";
    private const string LegacyAutoUpdaterStateFolder = "SABepInExManager.AutoUpdater";
    private const string AutoUpdaterStateDbFileName = "state.db";
    private const string LegacyAutoUpdaterStateJsonFileName = "state.json";
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
    private readonly AutoUpdaterSqliteStateStore _stateStore = new();

    public AutoUpdaterSyncService(ManualLogSource logger)
    {
        _logger = logger;
    }

    public void Run()
    {
        if (GetGameRoot() is not string gameRoot || string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            _logger.LogWarning("[AutoUpdater] 无法定位游戏根目录，跳过。");
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
            _logger.LogInfo("[AutoUpdater] 未检测到已启用模组，跳过。");
            return;
        }

        var detectedWorkshopRoot = ResolveWorkshopRoot(gameRoot, appState);
        if (string.IsNullOrWhiteSpace(detectedWorkshopRoot) || !Directory.Exists(detectedWorkshopRoot))
        {
            _logger.LogWarning($"[AutoUpdater] Workshop 目录无效: {detectedWorkshopRoot}");
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
            _logger.LogInfo("[AutoUpdater] 已启用列表中的模组未在 Workshop 找到可管理条目，跳过。");
            return;
        }

        var autoUpdaterState = LoadAutoUpdaterState(gameRoot);
        var newSignatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var newEntries = new Dictionary<string, IReadOnlyList<ManagedFileEntry>>(StringComparer.OrdinalIgnoreCase);
        var newCaches = new Dictionary<string, Dictionary<string, AutoUpdaterCachedFileState>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < enabledMods.Count; i++)
        {
            var mod = enabledMods[i];
            var entries = _managedFileManifestService.BuildEntries(mod);
            newEntries[mod.ModId] = entries;
            autoUpdaterState.Mods.TryGetValue(mod.ModId, out var oldModState);
            var computed = BuildManagedSignatureWithCache(entries, oldModState);
            newSignatures[mod.ModId] = computed.Signature;
            newCaches[mod.ModId] = computed.CachedFiles;
        }

        var firstChangedIndex = -1;
        for (var i = 0; i < enabledMods.Count; i++)
        {
            var mod = enabledMods[i];
            autoUpdaterState.Mods.TryGetValue(mod.ModId, out var oldModState);
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
            var cacheUpdated = false;
            for (var i = 0; i < enabledMods.Count; i++)
            {
                var mod = enabledMods[i];
                autoUpdaterState.Mods.TryGetValue(mod.ModId, out var oldModState);
                if (oldModState == null)
                {
                    continue;
                }

                if (!NeedsCacheWrite(oldModState, newCaches[mod.ModId]))
                {
                    continue;
                }

                oldModState.Signature = newSignatures[mod.ModId];
                oldModState.CachedFiles = newCaches[mod.ModId];
                cacheUpdated = true;
            }

            if (cacheUpdated)
            {
                autoUpdaterState.AppId = PathConstants.WorkshopAppId;
                autoUpdaterState.LastRunAt = DateTimeOffset.Now;
                SaveAutoUpdaterState(gameRoot, autoUpdaterState);
                _logger.LogInfo("[AutoUpdater] 已启用模组无变化，已刷新缓存状态。");
                return;
            }

            _logger.LogInfo("[AutoUpdater] 已启用模组无变化，跳过同步。");
            return;
        }

        var gameBepInExRoot = Path.Combine(gameRoot, "BepInEx");
        var selfAssemblyPath = Path.GetFullPath(GetType().Assembly.Location);
        var now = DateTimeOffset.Now;

        for (var i = firstChangedIndex; i < enabledMods.Count; i++)
        {
            var mod = enabledMods[i];
            var entries = newEntries[mod.ModId];
            autoUpdaterState.Mods.TryGetValue(mod.ModId, out var oldModState);
            var newState = AutoUpdaterFileSyncEngine.SyncSingleMod(
                gameBepInExRoot,
                selfAssemblyPath,
                mod.ModId,
                entries,
                oldModState,
                now,
                logInfo: message => _logger.LogInfo(message),
                logWarning: message => _logger.LogWarning(message));
            newState.Signature = newSignatures[mod.ModId];
            newState.CachedFiles = newCaches[mod.ModId];
            autoUpdaterState.Mods[mod.ModId] = newState;

            _logger.LogInfo($"[AutoUpdater] [{mod.ModId}] 已同步 {entries.Count} 个文件。");
        }

        autoUpdaterState.AppId = PathConstants.WorkshopAppId;
        autoUpdaterState.LastRunAt = now;
        SaveAutoUpdaterState(gameRoot, autoUpdaterState);
        _logger.LogInfo($"[AutoUpdater] 同步完成：从序号 {firstChangedIndex + 1} 开始重放，总启用模组 {enabledMods.Count}。");
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
        => Path.Combine(AppContext.BaseDirectory, PathConstants.ManagerStateFolder, PathConstants.StateFileName);

    private static string GetAutoUpdaterStateDbPath(string gameRoot)
        => Path.Combine(gameRoot, "BepInEx", "patchers", AutoUpdaterStateDbFolder, AutoUpdaterStateDbFileName);

    private static string GetLegacyAutoUpdaterStateJsonPath(string gameRoot)
        => Path.Combine(gameRoot, "BepInEx", "config", LegacyAutoUpdaterStateFolder, LegacyAutoUpdaterStateJsonFileName);

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
            _logger.LogWarning($"[AutoUpdater] 读取 GUI state 失败: {ex.Message}");
            return null;
        }
    }

    private AutoUpdaterSyncState LoadAutoUpdaterState(string gameRoot)
    {
        var dbPath = GetAutoUpdaterStateDbPath(gameRoot);
        var legacyJsonPath = GetLegacyAutoUpdaterStateJsonPath(gameRoot);

        try
        {
            if (!File.Exists(dbPath) && File.Exists(legacyJsonPath))
            {
                var legacyState = TryLoadLegacyJsonState(legacyJsonPath);
                if (legacyState != null)
                {
                    _stateStore.Save(dbPath, legacyState);
                    BackupLegacyJsonState(legacyJsonPath);
                }
            }

            var state = _stateStore.Load(dbPath);
            return EnsureStateDefaults(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[AutoUpdater] 读取 SQLite state 失败，使用空状态继续: {ex.Message}");
            return new AutoUpdaterSyncState
            {
                AppId = PathConstants.WorkshopAppId,
                LastRunAt = DateTimeOffset.MinValue,
            };
        }
    }

    private AutoUpdaterSyncState? TryLoadLegacyJsonState(string legacyJsonPath)
    {
        try
        {
            var json = File.ReadAllText(legacyJsonPath);
            var state = JsonSerializer.Deserialize<AutoUpdaterSyncState>(json, JsonOptions);
            if (state == null)
            {
                return null;
            }

            _logger.LogInfo("[AutoUpdater] 检测到旧版 state.json，已准备迁移到 SQLite。");
            return EnsureStateDefaults(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[AutoUpdater] 读取旧版 state.json 失败，跳过迁移: {ex.Message}");
            return null;
        }
    }

    private void BackupLegacyJsonState(string legacyJsonPath)
    {
        try
        {
            var backupPath = legacyJsonPath + BackupSuffix;
            File.Copy(legacyJsonPath, backupPath, overwrite: true);
            File.Delete(legacyJsonPath);
            _logger.LogInfo($"[AutoUpdater] 已完成 state.json -> SQLite 迁移，备份文件: {backupPath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[AutoUpdater] 迁移后备份旧 state.json 失败: {ex.Message}");
        }
    }

    private void SaveAutoUpdaterState(string gameRoot, AutoUpdaterSyncState state)
    {
        var dbPath = GetAutoUpdaterStateDbPath(gameRoot);

        try
        {
            _stateStore.Save(dbPath, EnsureStateDefaults(state));
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[AutoUpdater] 写入 SQLite state 失败: {ex.Message}");
        }
    }

    private static AutoUpdaterSyncState EnsureStateDefaults(AutoUpdaterSyncState state)
    {
        if (string.IsNullOrWhiteSpace(state.AppId))
        {
            state.AppId = PathConstants.WorkshopAppId;
        }

        state.Mods ??= new Dictionary<string, AutoUpdaterModState>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in state.Mods)
        {
            pair.Value.Files ??= [];
            pair.Value.CachedFiles ??= new Dictionary<string, AutoUpdaterCachedFileState>(StringComparer.OrdinalIgnoreCase);
        }

        return state;
    }

    private (string Signature, Dictionary<string, AutoUpdaterCachedFileState> CachedFiles) BuildManagedSignatureWithCache(
        IReadOnlyList<ManagedFileEntry> entries,
        AutoUpdaterModState? oldModState)
    {
        var oldCache = oldModState?.CachedFiles ?? new Dictionary<string, AutoUpdaterCachedFileState>(StringComparer.OrdinalIgnoreCase);
        var newCache = new Dictionary<string, AutoUpdaterCachedFileState>(StringComparer.OrdinalIgnoreCase);
        var builder = new System.Text.StringBuilder();

        foreach (var entry in entries
                     .OrderBy(x => x.TargetRelativePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            var target = NormalizeRelativePath(entry.TargetRelativePath);
            if (entry.IsDirectory)
            {
                builder.Append(target)
                    .Append("|DIR;");
                newCache[target] = new AutoUpdaterCachedFileState
                {
                    SourcePath = entry.SourcePath,
                    IsDirectory = true,
                    Length = 0,
                    LastWriteTimeUtc = DateTimeOffset.MinValue,
                    ContentHash = string.Empty,
                };
                continue;
            }

            if (!File.Exists(entry.SourcePath))
            {
                continue;
            }

            var fullSourcePath = Path.GetFullPath(entry.SourcePath);
            var fileInfo = new FileInfo(fullSourcePath);
            var lastWrite = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);

            var cachedHash = TryGetCachedHash(oldCache, target, fullSourcePath, fileInfo.Length, lastWrite);
            var hash = string.IsNullOrWhiteSpace(cachedHash)
                ? _signatureService.ComputeFileSha256Hex(fullSourcePath)
                : cachedHash;

            builder.Append(target)
                .Append('|')
                .Append(hash)
                .Append(';');

            newCache[target] = new AutoUpdaterCachedFileState
            {
                SourcePath = fullSourcePath,
                IsDirectory = false,
                Length = fileInfo.Length,
                LastWriteTimeUtc = lastWrite,
                ContentHash = hash,
            };
        }

        return (_signatureService.ComputeUtf8Sha256Hex(builder.ToString()), newCache);
    }

    private static string? TryGetCachedHash(
        IReadOnlyDictionary<string, AutoUpdaterCachedFileState> oldCache,
        string targetRelativePath,
        string sourcePath,
        long length,
        DateTimeOffset lastWriteUtc)
    {
        if (!oldCache.TryGetValue(targetRelativePath, out var cached)
            || cached.IsDirectory
            || string.IsNullOrWhiteSpace(cached.ContentHash))
        {
            return null;
        }

        if (!string.Equals(Path.GetFullPath(cached.SourcePath), sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (cached.Length != length)
        {
            return null;
        }

        return cached.LastWriteTimeUtc.UtcDateTime == lastWriteUtc.UtcDateTime
            ? cached.ContentHash
            : null;
    }

    private static bool NeedsCacheWrite(AutoUpdaterModState state, IReadOnlyDictionary<string, AutoUpdaterCachedFileState> newCache)
    {
        var oldCache = state.CachedFiles ?? new Dictionary<string, AutoUpdaterCachedFileState>(StringComparer.OrdinalIgnoreCase);
        if (oldCache.Count != newCache.Count)
        {
            return true;
        }

        foreach (var pair in newCache)
        {
            if (!oldCache.TryGetValue(pair.Key, out var oldEntry))
            {
                return true;
            }

            var newEntry = pair.Value;
            if (oldEntry.IsDirectory != newEntry.IsDirectory
                || oldEntry.Length != newEntry.Length
                || oldEntry.LastWriteTimeUtc.UtcDateTime != newEntry.LastWriteTimeUtc.UtcDateTime
                || !string.Equals(oldEntry.SourcePath, newEntry.SourcePath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(oldEntry.ContentHash, newEntry.ContentHash, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').TrimStart('/');
}

