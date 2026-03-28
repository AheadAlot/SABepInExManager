using System.Threading;
using System.Threading.Tasks;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public interface IAppUpdateStateStore
{
    Task<AppUpdateCheckResult?> LoadLastStateAsync(CancellationToken cancellationToken = default);
    Task SaveLastStateAsync(AppUpdateCheckResult result, CancellationToken cancellationToken = default);
}

