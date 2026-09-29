using System.Collections.Immutable;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Modules.Workspace.ApiAccess.Presentation;

internal static class ApiOwnerProjection {
    internal static ApiTokenMetadata Token(ApiTokenSummary token) => new(token.Id, token.Subject, token.DisplayName,
        token.IssuedAtUtc, token.ExpiresAtUtc, token.Scopes.ToImmutableArray(), token.RevokedAtUtc, token.Kind switch {
            ApiCredentialKind.Machine => ApiTokenCategory.Machine,
            ApiCredentialKind.UserSession => ApiTokenCategory.UserSession,
            ApiCredentialKind.AdministratorSession => ApiTokenCategory.AdministratorSession,
            _ => throw new InvalidDataException("Unsupported credential kind.")
        });

    internal static ApiAccountMetadata Account(ApiUserDetails account) => new(account.Id, account.UserName,
        account.DisplayName, account.Enabled, account.Scopes.ToImmutableArray(), account.Version,
        account.CreatedAtUtc, account.UpdatedAtUtc);

    internal static ApiWriteResult Failure(Exception exception) => exception switch {
        ApiDurableAcknowledgementException unknown => new(ApiWriteState.Unknown, ApiFailure.UnknownAcknowledgement, unknown.CandidateId),
        UnauthorizedAccessException or OperationCanceledException => new(ApiWriteState.Refused, ApiFailure.Denied),
        ApiUserConflictException => new(ApiWriteState.Refused, ApiFailure.Conflict),
        KeyNotFoundException => new(ApiWriteState.Refused, ApiFailure.Missing),
        ArgumentException or InvalidOperationException => new(ApiWriteState.Refused, ApiFailure.Invalid),
        _ => new(ApiWriteState.Refused, ApiFailure.Unavailable)
    };
}
