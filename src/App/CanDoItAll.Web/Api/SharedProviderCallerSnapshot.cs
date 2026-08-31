using System.Security.Claims;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Web.Api;

internal static class SharedProviderCallerSnapshot {
    public static string? Subject(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated != true ? "api-authorization-disabled" :
        principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.Identity.Name;

    public static SharedProviderCallerIdentity From(HttpContext context) {
        if (context.User.Identity?.IsAuthenticated != true) {
            return new(SharedProviderCallerKind.AuthenticationDisabled);
        }
        if (context.Features.Get<ValidatedApiCredential>() is { } credential) {
            return new(SharedProviderCallerKind.ManagedCredential, credential.Id,
                string.IsNullOrWhiteSpace(credential.Issuer) ? null : credential.Issuer, credential.DisplayName);
        }
        return new(SharedProviderCallerKind.LegacyAuthenticated);
    }
}
