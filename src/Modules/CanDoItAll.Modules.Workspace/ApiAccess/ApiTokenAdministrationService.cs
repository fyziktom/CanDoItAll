using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

public interface IApiTokenAdministrationAccess {
    ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default);
}

public sealed class UnavailableApiTokenAdministrationAccess : IApiTokenAdministrationAccess {
    public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
}

public sealed class ApiTokenAdministrationService(
    IApiTokenService issuer,
    IApiTokenRegistry registry,
    IApiTokenAdministrationAccess access,
    IClock clock) {
    public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default)
        => access.CanManageAsync(cancellationToken);

    public async Task<ApiTokenIssueResult> IssueAsync(ApiTokenIssueRequest request, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        return issuer.IssueToken(request);
    }

    public async Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        return await registry.SearchAsync(query, cancellationToken);
    }

    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        await registry.RevokeAsync(id, clock.GetUtcNow(), cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        await registry.DeleteAsync(id, cancellationToken);
    }

    private async ValueTask EnsureAccessAsync(CancellationToken cancellationToken) {
        if (!await access.CanManageAsync(cancellationToken)) {
            throw new UnauthorizedAccessException("Token administration requires the trusted local UI or the api.tokens.issue scope.");
        }
    }
}
