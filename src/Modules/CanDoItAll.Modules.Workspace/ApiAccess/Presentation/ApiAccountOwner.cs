using System.Collections.Immutable;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Modules.Workspace.ApiAccess.Presentation;

public sealed class ApiAccountOwner(ApiUserAdministrationService administration) : IApiAccountOwner {
    public async Task<ApiPage<ApiAccountMetadata>> SearchAsync(ApiPageQuery query, CancellationToken cancellationToken) {
        var page = await administration.SearchAsync(query.Search, query.Offset, ApiPageQuery.PageSize, cancellationToken);
        return new(page.Items.Select(ApiOwnerProjection.Account).ToImmutableArray(), page.TotalCount);
    }

    public async Task<ApiAccountMetadata?> ObserveAsync(Guid id, CancellationToken cancellationToken) {
        try {
            return ApiOwnerProjection.Account(await administration.GetAsync(id, cancellationToken));
        } catch (KeyNotFoundException) {
            return null;
        }
    }

    public async Task<ApiAccountWriteResult> ApplyAsync(ApiAccountIntent intent, CancellationToken cancellationToken) {
        string? password = intent.TakePassword();
        try {
            if (intent.Action != ApiWriteAction.CreateAccount && (intent.Id is null || intent.ExpectedVersion is null)) {
                return new(new(ApiWriteState.Refused, ApiFailure.Invalid));
            }
            var scopes = ApiScopeCatalog.Parse(intent.ScopeText);
            if (intent.Action == ApiWriteAction.DeleteAccount) {
                var warning = await administration.DeleteWithOutcomeAsync(intent.Id!.Value, intent.ExpectedVersion!.Value, cancellationToken);
                return new(new(warning ? ApiWriteState.CommittedWithWarning : ApiWriteState.Committed,
                    warning ? ApiFailure.Diagnostic : ApiFailure.None, intent.Id, intent.ExpectedVersion));
            }
            var account = intent.Action switch {
                ApiWriteAction.CreateAccount => await administration.CreateAsync(new(intent.UserName, intent.DisplayName, password, intent.Enabled, scopes), cancellationToken),
                ApiWriteAction.UpdateAccount => await administration.UpdateAsync(intent.Id!.Value, new(intent.UserName, intent.DisplayName, intent.Enabled, scopes, intent.ExpectedVersion!.Value), cancellationToken),
                ApiWriteAction.ResetPassword => await administration.ResetPasswordAsync(intent.Id!.Value, new(password, intent.ExpectedVersion!.Value), cancellationToken),
                _ => throw new ArgumentException("Unsupported account operation.")
            };
            return new(new(account.DiagnosticWarning ? ApiWriteState.CommittedWithWarning : ApiWriteState.Committed,
                account.DiagnosticWarning ? ApiFailure.Diagnostic : ApiFailure.None, account.Id, account.Version), ApiOwnerProjection.Account(account));
        } catch (Exception exception) {
            var result = ApiOwnerProjection.Failure(exception);
            return new(result with { Identity = result.Identity ?? intent.Id });
        } finally {
            password = null;
            intent.Dispose();
        }
    }
}
