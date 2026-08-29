using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CanDoItAll.Web.Api;

internal static class ApiManagedTokenValidation {
    public static async Task ValidateAsync(TokenValidatedContext context) {
        var version = context.Principal?.FindFirst(ApiManagedTokenClaims.Version)?.Value;
        if (version is null) {
            return;
        }
        if (version != ApiManagedTokenClaims.CurrentVersion ||
            !Guid.TryParseExact(context.Principal?.FindFirst(ApiManagedTokenClaims.TokenId)?.Value, "N", out var tokenId)) {
            context.Fail("The managed API token is invalid.");
            return;
        }

        try {
            var registry = context.HttpContext.RequestServices.GetRequiredService<IApiTokenRegistry>();
            var clock = context.HttpContext.RequestServices.GetRequiredService<IClock>();
            var token = await registry.FindAsync(tokenId, context.HttpContext.RequestAborted);
            if (token is null || token.GetStatus(clock.GetUtcNow()) != ApiTokenStatus.Active) {
                context.Fail("The API token is revoked, expired or deleted.");
                return;
            }
            context.HttpContext.Features.Set(new ValidatedApiCredential(token.Id, context.SecurityToken.Issuer, token.DisplayName));
        } catch (Exception exception) {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(ApiManagedTokenValidation))
                .LogError("Cannot validate registered API token {TokenId}: {ErrorType}. Access denied.",
                    tokenId, exception.GetType().Name);
            context.Fail("The API token registry is unavailable.");
        }
    }
}
