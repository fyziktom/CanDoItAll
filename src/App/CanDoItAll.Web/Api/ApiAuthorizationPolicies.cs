using System.Security.Claims;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal static class ApiAuthorizationPolicies
{
    public const string IssueTokens = "Api.IssueTokens";

    public const string ReadMemoryProviders = "Api.MemoryProviders.Read";

    public const string WriteMemoryProviders = "Api.MemoryProviders.Write";

    public const string QueryMemoryProviders = "Api.MemoryProviders.Query";

    public const string WriteProjectStructure = "Api.ProjectStructure.Write";

    public static bool HasScope(ClaimsPrincipal principal, string requiredScope)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return HasScope(principal, requiredScope, alternateScope: null);
    }

    public static bool HasApiOrSpecificScope(
        ClaimsPrincipal principal,
        string specificScope)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return HasScope(principal, ApiAccessScopeNames.Api, specificScope);
    }

    private static bool HasScope(
        ClaimsPrincipal principal,
        string requiredScope,
        string? alternateScope)
    {
        foreach (var claim in principal.Claims)
        {
            if (!IsScopeClaimType(claim.Type))
            {
                continue;
            }

            foreach (var range in claim.Value.AsSpan().Split(' '))
            {
                var scope = claim.Value.AsSpan(range).Trim();
                if (scope.Equals(requiredScope.AsSpan(), StringComparison.Ordinal) ||
                    alternateScope is not null &&
                    scope.Equals(alternateScope.AsSpan(), StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsScopeClaimType(string claimType) =>
        claimType is
            "scope" or
            "scopes" or
            "scp" or
            "http://schemas.microsoft.com/identity/claims/scope";
}
