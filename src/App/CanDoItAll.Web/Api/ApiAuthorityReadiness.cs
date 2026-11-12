using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal static class ApiAuthorityReadiness {
    internal static async Task PrepareAsync(IServiceProvider services, CancellationToken cancellationToken = default) {
        var options = services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        if (!options.Enabled || !options.Authorization.Enabled) {
            return;
        }
        await services.GetRequiredService<IApiTokenRegistry>().PrepareCurrentReadAsync(cancellationToken);
        if (options.UserAuthentication.Enabled) {
            await services.GetRequiredService<IApiUserStore>().EnsureAuthorityFormatAsync(cancellationToken);
        }
    }
}
