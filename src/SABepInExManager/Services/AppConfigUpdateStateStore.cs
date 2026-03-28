using System;
using System.Threading;
using System.Threading.Tasks;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class AppConfigUpdateStateStore : IAppUpdateStateStore
{
    private readonly ConfigService _configService;

    public AppConfigUpdateStateStore(ConfigService? configService = null)
    {
        _configService = configService ?? new ConfigService();
    }

    public async Task<AppUpdateCheckResult?> LoadLastStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var config = await _configService.LoadAsync();
        if (config.LastUpdateCheckAt is null
            && string.IsNullOrWhiteSpace(config.LastKnownLatestVersion)
            && string.IsNullOrWhiteSpace(config.LastUpdateCheckError)
            && !config.HasPendingAppUpdate)
        {
            return null;
        }

        var status = config.LastUpdateCheckSucceeded
            ? (config.HasPendingAppUpdate ? AppUpdateStatus.UpdateAvailable : AppUpdateStatus.UpToDate)
            : AppUpdateStatus.Failed;

        AppUpdateInfo? updateInfo = null;
        if (!string.IsNullOrWhiteSpace(config.LastKnownLatestVersion)
            || !string.IsNullOrWhiteSpace(config.LastKnownReleaseUrl))
        {
            updateInfo = new AppUpdateInfo
            {
                CurrentVersion = AppMetadata.Version,
                LatestVersion = config.LastKnownLatestVersion ?? string.Empty,
                ReleaseTag = config.LastKnownReleaseTag ?? string.Empty,
                ReleaseName = config.LastKnownReleaseName ?? string.Empty,
                ReleaseUrl = config.LastKnownReleaseUrl ?? string.Empty,
                PublishedAt = config.LastKnownReleasePublishedAt,
                IsUpdateAvailable = config.HasPendingAppUpdate,
            };
        }

        return new AppUpdateCheckResult
        {
            Succeeded = config.LastUpdateCheckSucceeded,
            Status = status,
            UpdateInfo = updateInfo,
            ErrorMessage = config.LastUpdateCheckError,
            CheckedAt = config.LastUpdateCheckAt ?? DateTimeOffset.MinValue,
            IsManualTrigger = false,
        };
    }

    public async Task SaveLastStateAsync(AppUpdateCheckResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();

        var config = await _configService.LoadAsync();
        config.LastUpdateCheckAt = result.CheckedAt;
        config.LastUpdateCheckSucceeded = result.Succeeded;

        if (result.Succeeded)
        {
            config.LastUpdateCheckError = null;
            if (result.UpdateInfo is not null)
            {
                config.LastKnownLatestVersion = result.UpdateInfo.LatestVersion;
                config.LastKnownReleaseTag = result.UpdateInfo.ReleaseTag;
                config.LastKnownReleaseName = result.UpdateInfo.ReleaseName;
                config.LastKnownReleaseUrl = result.UpdateInfo.ReleaseUrl;
                config.LastKnownReleasePublishedAt = result.UpdateInfo.PublishedAt;
                config.HasPendingAppUpdate = result.UpdateInfo.IsUpdateAvailable;
            }
            else
            {
                config.HasPendingAppUpdate = false;
            }
        }
        else
        {
            config.LastUpdateCheckError = result.ErrorMessage;
            if (result.UpdateInfo is not null)
            {
                config.LastKnownLatestVersion = result.UpdateInfo.LatestVersion;
                config.LastKnownReleaseTag = result.UpdateInfo.ReleaseTag;
                config.LastKnownReleaseName = result.UpdateInfo.ReleaseName;
                config.LastKnownReleaseUrl = result.UpdateInfo.ReleaseUrl;
                config.LastKnownReleasePublishedAt = result.UpdateInfo.PublishedAt;
                config.HasPendingAppUpdate = result.UpdateInfo.IsUpdateAvailable;
            }
        }

        await _configService.SaveAsync(config);
    }
}

