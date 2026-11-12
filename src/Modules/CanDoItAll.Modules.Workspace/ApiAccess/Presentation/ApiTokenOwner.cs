using System.Collections.Immutable;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Modules.Workspace.ApiAccess.Presentation;

public sealed class ApiTokenOwner(ApiTokenAdministrationService administration) : IApiTokenOwner {
    public async Task<ApiTokenDisclosure> IssueAsync(ApiTokenIntent intent, CancellationToken cancellationToken) {
        ApiTokenIssueResult issued;
        try {
            var request = new ApiTokenIssueRequest {
                Subject = intent.Subject,
                DisplayName = intent.DisplayName,
                LifetimeMinutes = intent.LifetimeMinutes,
                Scopes = ApiScopeCatalog.Parse(intent.ScopeText)
            };
            issued = await administration.IssueAsync(request, cancellationToken);
        } catch (Exception exception) {
            return new(ApiOwnerProjection.Failure(exception));
        }
        var metadata = issued.Registration is { } registration ? ApiOwnerProjection.Token(registration) : null;
        return new(new(ApiWriteState.Committed, Identity: metadata?.Id), metadata, issued.Token);
    }

    public async Task<ApiPage<ApiTokenMetadata>> SearchAsync(ApiPageQuery query, CancellationToken cancellationToken) {
        var page = await administration.SearchAsync(new(query.Search, query.Offset, ApiPageQuery.PageSize, ApiCredentialKind.Machine), cancellationToken);
        return new(page.Items.Select(ApiOwnerProjection.Token).ToImmutableArray(), page.TotalCount);
    }

    public async Task<ApiTokenMetadata?> ObserveAsync(Guid id, CancellationToken cancellationToken) {
        var token = await administration.GetAsync(id, cancellationToken);
        return token is null ? null : ApiOwnerProjection.Token(token);
    }

    public async Task<ApiWriteResult> ApplyAsync(ApiTokenAction action, CancellationToken cancellationToken) {
        try {
            var token = await administration.GetAsync(action.Id, cancellationToken);
            if (token is null) {
                return new(ApiWriteState.Refused, ApiFailure.Missing, action.Id);
            }
            if (action.Kind != ApiTokenCategory.Machine || token.Kind != ApiCredentialKind.Machine ||
                action.Action is not (ApiWriteAction.RevokeToken or ApiWriteAction.DeleteToken)) {
                return new(ApiWriteState.Refused, ApiFailure.Invalid, action.Id);
            }
            if (action.Action == ApiWriteAction.RevokeToken) {
                await administration.RevokeAsync(action.Id, cancellationToken);
            } else {
                await administration.DeleteAsync(action.Id, cancellationToken);
            }
            return new(ApiWriteState.Committed, Identity: action.Id);
        } catch (Exception exception) {
            return ApiOwnerProjection.Failure(exception) with { Identity = action.Id };
        }
    }
}
