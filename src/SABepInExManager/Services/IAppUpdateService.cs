using System.Threading;
using System.Threading.Tasks;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckForUpdatesAsync(bool manualTrigger, CancellationToken cancellationToken = default);
}

