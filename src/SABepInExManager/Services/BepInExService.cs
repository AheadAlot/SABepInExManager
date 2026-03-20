using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class BepInExService
{
    private const string AutoUpdaterFileName = "SABepInExManager.AutoUpdater.dll";
    private const string AutoUpdaterSubdirectoryName = "SABepInExManager_AutoUpdater";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    public bool IsInstalled(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return false;
        }

        var preloader = Path.Combine(gameRoot, "BepInEx", "core", "BepInEx.Preloader.dll");
        var doorstop = Path.Combine(gameRoot, "doorstop_config.ini");
        // 安装判定需同时满足：BepInEx 关键 DLL 存在 + doorstop_config.ini 存在并指向 Preloader。
        if (!File.Exists(preloader) || !File.Exists(doorstop))
        {
            return false;
        }

        var text = File.ReadAllText(doorstop);
        return text.Contains("target_assembly", StringComparison.OrdinalIgnoreCase)
               && text.Contains("BepInEx.Preloader.dll", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<InstallResult> InstallFromGitHubAsync(string gameRoot, Action<string>? progressCallback = null)
    {
        void Report(string message) => progressCallback?.Invoke(message);

        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return new InstallResult { Success = false, Message = "游戏根目录无效。" };
        }

        if (IsInstalled(gameRoot))
        {
            return new InstallResult { Success = true, Message = "已检测到 BepInEx，无需重复安装。" };
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "SABepInExManager", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, "bepinex.zip");
        var extractPath = Path.Combine(tempRoot, "extract");
        Directory.CreateDirectory(extractPath);

        try
        {
            var installedFromBackup = false;

            try
            {
                Report("正在从 GitHub 获取 BepInEx 版本信息...");
                var releaseJson = await GetLatestReleaseJsonAsync("BepInEx/BepInEx");
                var assetUrl = PickAssetUrl(releaseJson);
                if (string.IsNullOrWhiteSpace(assetUrl))
                {
                    return new InstallResult { Success = false, Message = "未找到匹配当前平台的 BepInEx 安装包。" };
                }

                Report("正在下载 BepInEx（GitHub）...");
                await DownloadFileAsync(assetUrl, zipPath);
            }
            catch (Exception ex) when (IsNetworkRelatedException(ex))
            {
                Report($"GitHub 下载失败：{FormatNetworkError(ex)}");
                Report("检测到网络异常，开始使用 third_party/BepInEx 本地备选目录...");

                var backupPayloadRoot = FindBackupBepInExPayloadRoot();
                if (string.IsNullOrWhiteSpace(backupPayloadRoot))
                {
                    return new InstallResult
                    {
                        Success = false,
                        Message = $"网络异常且未找到本地备选目录（third_party/BepInEx）。原始错误：{FormatNetworkError(ex)}",
                    };
                }

                Report($"已切换到本地备选目录：{backupPayloadRoot}");
                Report("正在写入游戏目录...");
                CopyExtractedToGameRoot(backupPayloadRoot, gameRoot);
                installedFromBackup = true;
            }

            if (!installedFromBackup)
            {
                Report("正在解压 BepInEx...");
                ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

                Report("正在写入游戏目录...");
                CopyExtractedToGameRoot(extractPath, gameRoot);
            }

            Report("正在安装 ConfigurationManager...");
            var configManagerResult = await InstallConfigurationManagerAsync(gameRoot, tempRoot, Report);
            if (!configManagerResult.Success)
            {
                Report($"ConfigurationManager 安装失败：{configManagerResult.Message}");
            }

            Report("正在应用默认配置...");
            var configCopied = TryCopyBundledBepInExConfig(gameRoot);

            Report("正在部署 AutoUpdater...");
            var autoUpdaterInstallResult = TryInstallBundledAutoUpdater(gameRoot, out var autoUpdaterMessage);
            Report(autoUpdaterMessage);

            Report("正在校验安装结果...");
            if (!IsInstalled(gameRoot))
            {
                return new InstallResult { Success = false, Message = "安装后校验失败，请检查目录权限或手动安装。" };
            }

            var source = installedFromBackup ? "本地备选目录" : "GitHub";
            var configManagerStatus = configManagerResult.Success
                ? "已安装 ConfigurationManager"
                : $"未安装 ConfigurationManager（{configManagerResult.Message}）";
            var configStatus = configCopied
                ? "已写入默认 BepInEx.cfg"
                : "未找到内置 BepInEx.cfg，已跳过配置覆盖";
            var autoUpdaterStatus = autoUpdaterInstallResult
                ? "已部署 AutoUpdater"
                : $"未部署 AutoUpdater（{autoUpdaterMessage}）";

            var message = $"BepInEx 安装完成（来源：{source}），{configManagerStatus}，{configStatus}，{autoUpdaterStatus}。";

            return new InstallResult { Success = true, Message = message };
        }
        catch (Exception ex)
        {
            return new InstallResult { Success = false, Message = $"安装失败：{ex.Message}" };
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    public async Task<InstallResult> ReinstallAsync(string gameRoot, Action<string>? progressCallback = null)
    {
        void Report(string message) => progressCallback?.Invoke(message);

        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return new InstallResult { Success = false, Message = "游戏根目录无效。" };
        }

        try
        {
            Report("正在移除现有 BepInEx 相关文件...");
            RemoveExistingInstallation(gameRoot, Report);
        }
        catch (Exception ex)
        {
            return new InstallResult { Success = false, Message = $"重装前清理失败：{ex.Message}" };
        }

        return await InstallFromGitHubAsync(gameRoot, progressCallback);
    }

    public Task<InstallResult> UpdateAutoUpdaterAsync(string gameRoot, Action<string>? progressCallback = null)
    {
        void Report(string message) => progressCallback?.Invoke(message);

        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return Task.FromResult(new InstallResult { Success = false, Message = "游戏根目录无效。" });
        }

        try
        {
            Report("正在部署 AutoUpdater...");
            var installed = TryInstallBundledAutoUpdater(gameRoot, out var message);
            Report(message);

            return Task.FromResult(new InstallResult
            {
                Success = installed,
                Message = installed
                    ? "AutoUpdater 更新完成，已覆盖写入游戏目录。"
                    : $"AutoUpdater 更新失败：{message}",
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new InstallResult
            {
                Success = false,
                Message = $"AutoUpdater 更新失败：{ex.Message}",
            });
        }
    }

    private static async Task<string> GetLatestReleaseJsonAsync(string repo)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
        request.Headers.Add("User-Agent", "SABepInExManager");

        using var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static string? PickAssetUrl(string releaseJson)
    {
        using var doc = JsonDocument.Parse(releaseJson);
        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var keyword = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "win"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "mac"
                : "linux";

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (asset.TryGetProperty("browser_download_url", out var urlElement))
            {
                return urlElement.GetString();
            }
        }

        return null;
    }

    private static string? PickConfigurationManagerAssetUrl(string releaseJson)
    {
        using var doc = JsonDocument.Parse(releaseJson);
        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var allZipAssets = assets
            .EnumerateArray()
            .Select(a => new
            {
                Name = a.GetProperty("name").GetString() ?? string.Empty,
                Url = a.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null,
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Url) && x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (allZipAssets.Count == 0)
        {
            return null;
        }

        // 优先 BepInEx5 版本（当前项目使用 BepInEx 5 安装结构）。
        var preferred = allZipAssets.FirstOrDefault(x =>
            x.Name.Contains("bepinex", StringComparison.OrdinalIgnoreCase)
            && x.Name.Contains("5", StringComparison.OrdinalIgnoreCase));

        if (preferred?.Url is not null)
        {
            return preferred.Url;
        }

        // 回退：任意包含 ConfigurationManager 关键字的 zip。
        var fallback = allZipAssets.FirstOrDefault(x =>
            x.Name.Contains("configuration", StringComparison.OrdinalIgnoreCase)
            || x.Name.Contains("configmanager", StringComparison.OrdinalIgnoreCase));

        return fallback?.Url;
    }

    private static async Task DownloadFileAsync(string url, string destinationPath)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "SABepInExManager");

        using var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        await using var output = File.Create(destinationPath);
        await response.Content.CopyToAsync(output);
    }

    private static async Task<InstallResult> InstallConfigurationManagerAsync(string gameRoot, string tempRoot, Action<string>? progressCallback)
    {
        try
        {
            var zipPath = Path.Combine(tempRoot, "configuration-manager.zip");
            var extractPath = Path.Combine(tempRoot, "configuration-manager-extract");
            Directory.CreateDirectory(extractPath);

            var installedFromBackup = false;

            try
            {
                progressCallback?.Invoke("正在从 GitHub 获取 ConfigurationManager 版本信息...");
                var releaseJson = await GetLatestReleaseJsonAsync("BepInEx/BepInEx.ConfigurationManager");
                var assetUrl = PickConfigurationManagerAssetUrl(releaseJson);
                if (string.IsNullOrWhiteSpace(assetUrl))
                {
                    return new InstallResult { Success = false, Message = "未找到 ConfigurationManager 安装包。" };
                }

                progressCallback?.Invoke("正在下载 ConfigurationManager（GitHub）...");
                await DownloadFileAsync(assetUrl, zipPath);
            }
            catch (Exception ex) when (IsNetworkRelatedException(ex))
            {
                progressCallback?.Invoke($"ConfigurationManager GitHub 下载失败：{FormatNetworkError(ex)}");
                progressCallback?.Invoke("检测到网络异常，开始使用 third_party/BepInEx.ConfigurationManager 本地备选目录...");

                var backupSourceRoot = FindBackupConfigurationManagerSourceRoot();
                if (string.IsNullOrWhiteSpace(backupSourceRoot))
                {
                    return new InstallResult
                    {
                        Success = false,
                        Message = $"网络异常且未找到 ConfigurationManager 本地备选目录（third_party/BepInEx.ConfigurationManager）。原始错误：{FormatNetworkError(ex)}",
                    };
                }

                var dllPathsFromBackup = Directory
                    .GetFiles(backupSourceRoot, "*ConfigurationManager*.dll", SearchOption.AllDirectories)
                    .ToList();

                if (dllPathsFromBackup.Count == 0)
                {
                    return new InstallResult { Success = false, Message = "ConfigurationManager 本地备选目录中未找到 DLL。" };
                }

                var pluginsDirFromBackup = Path.Combine(gameRoot, "BepInEx", "plugins", "ConfigurationManager");
                Directory.CreateDirectory(pluginsDirFromBackup);

                foreach (var dllPath in dllPathsFromBackup)
                {
                    var fileName = Path.GetFileName(dllPath);
                    var destination = Path.Combine(pluginsDirFromBackup, fileName);
                    File.Copy(dllPath, destination, overwrite: true);
                }

                installedFromBackup = true;
                progressCallback?.Invoke($"ConfigurationManager 已切换到本地备选目录：{backupSourceRoot}");
            }

            if (!installedFromBackup)
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

                var dllPaths = Directory
                    .GetFiles(extractPath, "*ConfigurationManager*.dll", SearchOption.AllDirectories)
                    .ToList();

                if (dllPaths.Count == 0)
                {
                    return new InstallResult { Success = false, Message = "ConfigurationManager 安装包中未找到 DLL。" };
                }

                var pluginsDir = Path.Combine(gameRoot, "BepInEx", "plugins", "ConfigurationManager");
                Directory.CreateDirectory(pluginsDir);

                foreach (var dllPath in dllPaths)
                {
                    var fileName = Path.GetFileName(dllPath);
                    var destination = Path.Combine(pluginsDir, fileName);
                    File.Copy(dllPath, destination, overwrite: true);
                }
            }

            var source = installedFromBackup ? "本地备选目录" : "GitHub";
            return new InstallResult { Success = true, Message = $"ConfigurationManager 安装完成（来源：{source}）。" };
        }
        catch (Exception ex)
        {
            return new InstallResult
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    private static bool IsNetworkRelatedException(Exception ex)
    {
        if (ex is HttpRequestException || ex is TimeoutException || ex is TaskCanceledException)
        {
            return true;
        }

        return ex.InnerException is not null && IsNetworkRelatedException(ex.InnerException);
    }

    private static string FormatNetworkError(Exception ex)
    {
        if (ex is TaskCanceledException or TimeoutException)
        {
            return "请求超时（可能受国内网络环境影响）";
        }

        if (ex is HttpRequestException httpRequestException)
        {
            return string.IsNullOrWhiteSpace(httpRequestException.Message)
                ? "网络请求失败"
                : httpRequestException.Message;
        }

        return ex.Message;
    }

    private static string? FindBackupBepInExPayloadRoot()
    {
        var folderCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", "BepInEx"),
            Path.Combine(Directory.GetCurrentDirectory(), "third_party", "BepInEx"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "BepInEx")),
        };

        foreach (var folder in folderCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var payloadRoot = ResolveInstallPayloadRoot(folder);
            var bepinexDir = Path.Combine(payloadRoot, "BepInEx");
            var doorstopFile = Path.Combine(payloadRoot, "doorstop_config.ini");
            var preloaderDll = Path.Combine(payloadRoot, "BepInEx", "core", "BepInEx.Preloader.dll");

            if (Directory.Exists(bepinexDir)
                && (File.Exists(doorstopFile) || File.Exists(preloaderDll)))
            {
                return payloadRoot;
            }
        }

        return null;
    }

    private static string? FindBackupConfigurationManagerSourceRoot()
    {
        var folderCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", "BepInEx.ConfigurationManager"),
            Path.Combine(Directory.GetCurrentDirectory(), "third_party", "BepInEx.ConfigurationManager"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "BepInEx.ConfigurationManager")),
        };

        foreach (var folder in folderCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var hasDll = Directory
                .GetFiles(folder, "*ConfigurationManager*.dll", SearchOption.AllDirectories)
                .Any();

            if (hasDll)
            {
                return folder;
            }
        }

        return null;
    }

    private static bool TryCopyBundledBepInExConfig(string gameRoot)
    {
        var sourceCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "configs", "BepInEx.cfg"),
            Path.Combine(Directory.GetCurrentDirectory(), "configs", "BepInEx.cfg"),
        };

        var source = sourceCandidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var targetDir = Path.Combine(gameRoot, "BepInEx", "config");
        Directory.CreateDirectory(targetDir);
        var target = Path.Combine(targetDir, "BepInEx.cfg");
        File.Copy(source, target, overwrite: true);
        return true;
    }

    private static bool TryInstallBundledAutoUpdater(string gameRoot, out string message)
    {
        var sourceDirCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "patchers", AutoUpdaterSubdirectoryName),
        };

        var sourceDir = sourceDirCandidates
            .FirstOrDefault(dir => Directory.Exists(dir) && File.Exists(Path.Combine(dir, AutoUpdaterFileName)));
        if (string.IsNullOrWhiteSpace(sourceDir))
        {
            message = "未找到随程序分发的 AutoUpdater DLL，已跳过";
            return false;
        }

        var targetDir = Path.Combine(gameRoot, "BepInEx", "patchers", AutoUpdaterSubdirectoryName);
        Directory.CreateDirectory(targetDir);

        CopyDirectory(sourceDir, targetDir, overwrite: true);

        message = $"AutoUpdater 已部署到 {targetDir}（包含全部依赖）";
        return true;
    }

    private static void CopyExtractedToGameRoot(string extractRoot, string gameRoot)
    {
        var root = ResolveInstallPayloadRoot(extractRoot);

        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(gameRoot, name), overwrite: true);
        }

        foreach (var file in Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            File.Copy(file, Path.Combine(gameRoot, name), overwrite: true);
        }
    }

    private static string ResolveInstallPayloadRoot(string extractRoot)
    {
        // 目标结构：
        // gameRoot/
        //   BepInEx/(config, core, ...)
        //   doorstop_config.ini
        //   以及其他根目录文件（如 winhttp.dll 等）
        //
        // GitHub 资产有时会多包一层目录，故这里递归定位真实 payload 根目录。
        var candidates = Directory
            .EnumerateDirectories(extractRoot, "*", SearchOption.AllDirectories)
            .Prepend(extractRoot);

        foreach (var candidate in candidates)
        {
            var bepinexDir = Path.Combine(candidate, "BepInEx");
            var doorstopFile = Path.Combine(candidate, "doorstop_config.ini");
            var preloaderDll = Path.Combine(candidate, "BepInEx", "core", "BepInEx.Preloader.dll");

            // 以 BepInEx 目录 + doorstop（或 preloader）联合判定 payload 根。
            if (Directory.Exists(bepinexDir)
                && (File.Exists(doorstopFile) || File.Exists(preloaderDll)))
            {
                return candidate;
            }
        }

        // 回退：兼容常见“单目录包裹”的结构。
        var topDirs = Directory.GetDirectories(extractRoot);
        return topDirs.Length == 1 ? topDirs[0] : extractRoot;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var target = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, target, overwrite);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var target = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, target, overwrite);
        }
    }

    private static void RemoveExistingInstallation(string gameRoot, Action<string>? progressCallback)
    {
        var bepinexDir = Path.Combine(gameRoot, "BepInEx");
        if (Directory.Exists(bepinexDir))
        {
            Directory.Delete(bepinexDir, recursive: true);
            progressCallback?.Invoke("已移除目录：BepInEx");
        }

        var fileCandidates = new[]
        {
            "doorstop_config.ini",
            "winhttp.dll",
        };

        foreach (var fileName in fileCandidates)
        {
            var filePath = Path.Combine(gameRoot, fileName);
            if (!File.Exists(filePath))
            {
                continue;
            }

            File.Delete(filePath);
            progressCallback?.Invoke($"已移除文件：{fileName}");
        }
    }
}


