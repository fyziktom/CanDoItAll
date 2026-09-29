using System.Collections.Immutable;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workspace.ApiAccess.Presentation;

public sealed class ApiAccessConfigurationOwner(IApiTokenService tokens, IApiTokenAdministrationAccess access,
    IOptions<ApiAccessOptions> options) : IApiAccessConfigurationOwner {
    public Task<ApiAccessConfiguration> ReadAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var status = tokens.GetStatus();
        return Task.FromResult(new ApiAccessConfiguration(status.ApiEnabled, status.OpenApiEnabled, status.SwaggerUiEnabled,
            status.AuthorizationEnabled, status.SigningKeyConfigured, status.Issuer, status.Audience,
            status.DefaultTokenLifetimeMinutes, status.MaxTokenLifetimeMinutes, status.UserAuthenticationEnabled,
            status.AccessManagementEnabled, status.UserTokenLifetimeMinutes, options.Value.BootstrapAdmin.UserName,
            ApiScopeCatalog.All.Select(scope => new ApiScopeChoice(scope.Name, scope.Label, scope.Description, scope.Section,
                scope.UserSelectable, scope.MachineSelectable, scope.Sensitive)).ToImmutableArray(), ApiAccessScopeNames.Api));
    }

    public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken) => access.CanManageAsync(cancellationToken);
}
