using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CanDoItAll.Web.Api;

internal static class ApiManagedTokenValidation {
    public static async Task ValidateAsync(TokenValidatedContext context) {
        Guid tokenId = default;
        try {
            using var document = ApiTokenPayloadValidation.Read(context.SecurityToken);
            var payload = document.RootElement;
            if (!payload.TryGetProperty(ApiManagedTokenClaims.Version, out _)) {
                if (payload.TryGetProperty(ApiManagedTokenClaims.Kind, out _) || payload.TryGetProperty(ApiManagedTokenClaims.UserId, out _) ||
                    payload.TryGetProperty(ApiManagedTokenClaims.AuthenticationRevision, out _)) {
                    context.Fail("Session claims require a managed credential.");
                }
                return;
            }
            var version = ApiTokenPayloadValidation.Text(payload, ApiManagedTokenClaims.Version);
            if (version is not (ApiManagedTokenClaims.CurrentVersion or ApiManagedTokenClaims.SessionVersion) ||
                !Guid.TryParseExact(ApiTokenPayloadValidation.Text(payload, ApiManagedTokenClaims.TokenId), "N", out tokenId)) {
                context.Fail("The managed API token is invalid.");
                return;
            }
            var registry = context.HttpContext.RequestServices.GetRequiredService<IApiTokenRegistry>();
            var clock = context.HttpContext.RequestServices.GetRequiredService<IClock>();
            var token = await registry.FindAsync(tokenId, context.HttpContext.RequestAborted);
            if (token is null || token.GetStatus(clock.GetUtcNow()) != ApiTokenStatus.Active || !ApiTokenPayloadValidation.Matches(payload, token)) {
                context.Fail("The API token is revoked, expired or deleted.");
                return;
            }
            ApiSessionIdentity? identity = null;
            if (token.Kind != ApiCredentialKind.Machine) {
                identity = await context.HttpContext.RequestServices.GetRequiredService<ApiSessionService>()
                    .ResolveIdentityAsync(token, context.HttpContext.RequestAborted);
                if (identity is null) {
                    context.Fail("The API session is no longer valid.");
                    return;
                }
            }
            context.HttpContext.Features.Set(new ValidatedApiCredential(token.Id, context.SecurityToken.Issuer, token.DisplayName, token.Kind, identity));
        } catch (Exception exception) {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(ApiManagedTokenValidation))
                .LogError("Cannot validate registered API token {TokenId}: {ErrorType}. Access denied.",
                    tokenId, exception.GetType().Name);
            context.Fail("The API token registry is unavailable.");
        }
    }
}
