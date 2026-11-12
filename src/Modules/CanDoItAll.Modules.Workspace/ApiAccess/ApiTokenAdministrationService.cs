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
        ArgumentNullException.ThrowIfNull(request);
        var captured = new ApiTokenIssueRequest {
            Subject = request.Subject,
            DisplayName = request.DisplayName,
            LifetimeMinutes = request.LifetimeMinutes,
            Scopes = request.Scopes?.ToList()!
        };
        await EnsureAccessAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return issuer.IssueToken(captured);
    }

    public async Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        return await registry.SearchAsync(query, cancellationToken);
    }

    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        ValidateIdentity(id);
        if (await registry.FindAsync(id, cancellationToken) is null) {
            throw new InvalidOperationException("The token no longer exists.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        await WriteAsync(id, () => registry.RevokeAsync(id, clock.GetUtcNow(), CancellationToken.None));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        ValidateIdentity(id);
        cancellationToken.ThrowIfCancellationRequested();
        await WriteAsync(id, () => registry.DeleteAsync(id, CancellationToken.None));
    }

    public async Task<ApiTokenSummary?> GetAsync(Guid id, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        var record = await registry.FindAsync(id, cancellationToken);
        return record is null ? null : ApiTokenSummary.FromRecord(record);
    }

    private static async Task WriteAsync(Guid id, Func<Task> write) {
        try {
            await write();
        } catch (Exception exception) {
            throw new ApiDurableAcknowledgementException(id, exception.GetType().Name);
        }
    }

    private static void ValidateIdentity(Guid id) {
        if (id == Guid.Empty) {
            throw new ArgumentException("A token identity is required.", nameof(id));
        }
    }

    private async ValueTask EnsureAccessAsync(CancellationToken cancellationToken) {
        if (!await access.CanManageAsync(cancellationToken)) {
            throw new UnauthorizedAccessException("Token administration requires the trusted local UI or a validated administrator session on the enabled HTTP surface.");
        }
    }
}
