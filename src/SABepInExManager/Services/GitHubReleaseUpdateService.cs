using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class GitHubReleaseUpdateService : IAppUpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(bool manualTrigger, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.Now;
        var currentVersion = AppMetadata.Version;

        try
        {
            var endpoint = $"https://api.github.com/repos/{AppMetadata.RepositoryOwner}/{AppMetadata.RepositoryName}/releases/latest";
            using var response = await HttpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return CreateFailedResult($"请求 GitHub Release 失败：HTTP {(int)response.StatusCode}", checkedAt, manualTrigger);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            var tagName = GetString(root, "tag_name");
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return CreateFailedResult("GitHub 返回的 release 缺少 tag_name。", checkedAt, manualTrigger);
            }

            if (!TryParseVersion(tagName, out var latestVersion))
            {
                return CreateFailedResult($"无法解析远端版本号：{tagName}", checkedAt, manualTrigger);
            }

            if (!TryParseVersion(currentVersion, out var current))
            {
                return CreateFailedResult($"无法解析当前版本号：{currentVersion}", checkedAt, manualTrigger);
            }

            var updateAvailable = latestVersion > current;
            var status = updateAvailable ? AppUpdateStatus.UpdateAvailable : AppUpdateStatus.UpToDate;

            var info = new AppUpdateInfo
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion.ToString(3),
                ReleaseTag = tagName,
                ReleaseName = GetString(root, "name") ?? string.Empty,
                ReleaseUrl = GetString(root, "html_url") ?? AppMetadata.RepositoryUrl,
                PublishedAt = GetDateTimeOffset(root, "published_at"),
                IsUpdateAvailable = updateAvailable,
            };

            return new AppUpdateCheckResult
            {
                Succeeded = true,
                Status = status,
                UpdateInfo = info,
                ErrorMessage = null,
                CheckedAt = checkedAt,
                IsManualTrigger = manualTrigger,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CreateFailedResult($"检查更新失败：{ex.Message}", checkedAt, manualTrigger);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SABepInExManager/1.0");
        return client;
    }

    private static AppUpdateCheckResult CreateFailedResult(string message, DateTimeOffset checkedAt, bool manualTrigger)
    {
        return new AppUpdateCheckResult
        {
            Succeeded = false,
            Status = AppUpdateStatus.Failed,
            ErrorMessage = message,
            CheckedAt = checkedAt,
            IsManualTrigger = manualTrigger,
        };
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var p) || p.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var p) || p.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(p.GetString(), out var parsed) ? parsed : null;
    }

    private static bool TryParseVersion(string rawVersion, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return false;
        }

        var normalized = rawVersion.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var dashIndex = normalized.IndexOf('-');
        if (dashIndex > 0)
        {
            normalized = normalized[..dashIndex];
        }

        if (!Version.TryParse(normalized, out var parsedVersion) || parsedVersion is null)
        {
            return false;
        }

        version = parsedVersion;
        return true;
    }
}

