using System.ComponentModel;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal static class ApiAccessEndpoints {
    public static void MapAccess(this RouteGroupBuilder api, ApiAccessOptions options) {
        if (options.UserAuthentication.Enabled) {
            var sessions = api.MapGroup("/access").AddEndpointFilter<ApiAccessExceptionFilter>();
            sessions.MapPost("/login", LoginAsync).AllowAnonymous().WithName("LoginApiUser")
                .DescribeApi("Authenticate an API account", "Verify an ordinary account or configured administrator password and register a fresh session. No refresh token is issued. Wrong, unknown and disabled accounts share the same invalid-credentials response. Available only when user authentication is enabled.", "Fresh bearer token and safe server-selected identity; returned with no-store.", "Only username and password; unknown properties and caller-selected authority are rejected.")
                .Produces<ApiLoginResult>().ProducesApiErrors(400, 401, 429, 503);
            sessions.MapGet("/me", (HttpContext context) => Results.Ok(context.Features.Get<ValidatedApiCredential>()!.Session))
                .RequireAuthorization(ApiAuthorizationPolicies.UserSession).WithName("GetApiSession")
                .DescribeApi("Read current API session", "Read the safe identity of the current registered user or administrator session. Machine and legacy tokens cannot use self-session operations.", "Current validated session identity.")
                .Produces<ApiSessionIdentity>().ProducesApiErrors(401, 403);
            sessions.MapPost("/logout", LogoutAsync).RequireAuthorization(ApiAuthorizationPolicies.UserSession)
                .WithName("LogoutApiSession")
                .DescribeApi("Revoke current API session", "Revoke only this registered user or administrator session. Other sessions and unrelated machine credentials remain unchanged.", "Current session revoked; no response body.").Produces(204).ProducesApiErrors(401, 403);
        }
        if (!options.AccessManagement.Enabled) {
            return;
        }
        var management = api.MapGroup("/access").RequireAuthorization(ApiAuthorizationPolicies.ManageAccess)
            .AddEndpointFilter<ApiAccessExceptionFilter>();
        management.MapGet("/scopes", () => ApiScopeCatalog.All).WithName("GetApiScopeCatalog")
                .DescribeApi("Read assignable API capabilities", "Read the server-owned catalog, including credential-kind selection limits and sensitive capability markers. Requires the configured administrator session and HTTP management enabled.", "Current server capability catalog.").Produces<IReadOnlyList<ApiScopeDefinition>>().ProducesApiErrors(401, 403);
        management.MapGet("/users", ([Description("Optional account-name or credential-label search text.")] string? search, [Description("Zero-based result offset; defaults to zero.")] int? offset, [Description("Maximum entries to return, from 1 through 100; defaults to 25.")] int? pageSize, ApiUserAdministrationService service, CancellationToken cancellationToken) =>
            service.SearchAsync(search, offset ?? 0, pageSize ?? 25, cancellationToken)).WithName("ListApiUsers")
                .DescribeApi("Search ordinary API accounts", "Read a bounded account page without credential material. Requires the configured administrator session and HTTP management enabled.", "Matching account page and total count.").Produces<ApiUserPage>().ProducesApiErrors(400, 401, 403, 503);
        management.MapGet("/users/{id:guid}", ([Description("GUID of the account or registered credential addressed by this operation.")] Guid id, ApiUserAdministrationService service, CancellationToken cancellationToken) =>
            service.GetAsync(id, cancellationToken)).WithName("GetApiUser")
                .DescribeApi("Read an ordinary API account", "Read a safe profile and current mutation version. Requires the configured administrator session and HTTP management enabled.", "Current ordinary-account profile.").Produces<ApiUserDetails>().ProducesApiErrors(400, 401, 403, 404, 409, 503);
        management.MapPost("/users", (ApiUserCreateRequest request, ApiUserAdministrationService service, CancellationToken cancellationToken) =>
            service.CreateAsync(request, cancellationToken)).WithName("CreateApiUser")
                .DescribeApi("Create an ordinary API account", "Create a new stable account identity with a salted password hash and explicit business grants. Cannot promote an account or use the reserved administrator name. Requires the configured administrator session.", "Created account profile and initial version.", "New account fields with explicit enabled state and business capability selection.").Produces<ApiUserDetails>().ProducesApiErrors(400, 401, 403, 404, 409, 503);
        management.MapPut("/users/{id:guid}", ([Description("GUID of the account or registered credential addressed by this operation.")] Guid id, ApiUserUpdateRequest request, ApiUserAdministrationService service, CancellationToken cancellationToken) =>
            service.UpdateAsync(id, request, cancellationToken)).WithName("UpdateApiUser")
                .DescribeApi("Replace an ordinary API account profile", "Apply a version-checked profile replacement and invalidate existing sessions of this account. Requires the configured administrator session.", "Committed account profile and new version.", "Complete replacement profile and the version observed by the caller.").Produces<ApiUserDetails>().ProducesApiErrors(400, 401, 403, 404, 409, 503);
        management.MapPost("/users/{id:guid}/reset-password", ([Description("GUID of the account or registered credential addressed by this operation.")] Guid id, ApiUserPasswordResetRequest request, ApiUserAdministrationService service, CancellationToken cancellationToken) =>
            service.ResetPasswordAsync(id, request, cancellationToken)).WithName("ResetApiUserPassword")
                .DescribeApi("Reset an ordinary API account password", "Replace the password hash using the expected account version and invalidate its existing sessions. Requires the configured administrator session.", "Account profile after password reset, without any password material.", "New password and the current account version.").Produces<ApiUserDetails>().ProducesApiErrors(400, 401, 403, 404, 409, 503);
        management.MapDelete("/users/{id:guid}", async ([Description("GUID of the account or registered credential addressed by this operation.")] Guid id, [Description("Account version returned by the last read; a stale value prevents deletion and returns 409.")] long expectedVersion, ApiUserAdministrationService service, CancellationToken cancellationToken) => {
            await service.DeleteAsync(id, expectedVersion, cancellationToken);
            return Results.NoContent();
        }).WithName("DeleteApiUser")
                .DescribeApi("Delete an ordinary API account", "Delete the version-matched account and invalidate its sessions. Recreating the same username creates a new GUID. Requires the configured administrator session.", "Account deleted; no response body.").Produces(204).ProducesApiErrors(400, 401, 403, 404, 409, 503);
        management.MapGet("/tokens", ListTokensAsync).WithName("ListApiTokens")
                .DescribeApi("Search registered credential metadata", "Read safe metadata for one credential kind; defaults to machine credentials. Requires the configured administrator session. Never returns token plaintext or credential bindings.", "Matching credential metadata page and total count.").Produces<ApiTokenPage>().ProducesApiErrors(400, 401, 403, 503);
        management.MapPost("/tokens", (ApiTokenIssueRequest request, ApiTokenAdministrationService service, CancellationToken cancellationToken) =>
            service.IssueAsync(request, cancellationToken)).WithName("IssueApiToken")
                .DescribeApi("Issue a registered machine token", "Create a v1 machine registration and return its signed token once. Requires a registered configured-administrator session and HTTP management enabled; old api.tokens.issue is insufficient.", "New machine token and its expiry; returned once with no-store.", "Machine subject, display label, optional lifetime and selectable catalog capabilities.").Produces<ApiTokenIssueResult>().ProducesApiErrors(400, 401, 403, 503);
        management.MapPost("/tokens/{id:guid}/revoke", async ([Description("GUID of the account or registered credential addressed by this operation.")] Guid id, ApiTokenAdministrationService service, CancellationToken cancellationToken) => {
            await service.RevokeAsync(id, cancellationToken);
            return Results.NoContent();
        }).WithName("RevokeApiToken")
                .DescribeApi("Revoke a registered credential", "Revoke the addressed machine or session registration. Subsequent validation and bounded stream checks reject it. Requires the configured administrator session.", "Revocation completed; no response body.").Produces(204).ProducesApiErrors(400, 401, 403, 404, 409, 503);
        management.MapDelete("/tokens/{id:guid}", async ([Description("GUID of the account or registered credential addressed by this operation.")] Guid id, ApiTokenAdministrationService service, CancellationToken cancellationToken) => {
            await service.DeleteAsync(id, cancellationToken);
            return Results.NoContent();
        }).WithName("DeleteApiToken")
                .DescribeApi("Delete a registered credential", "Remove the addressed registration so future authentication fails. Requires the configured administrator session.", "Registration deletion completed; no response body.").Produces(204).ProducesApiErrors(400, 401, 403, 404, 409, 503);
    }

    private static async Task<IResult> LoginAsync(ApiLoginRequest request, HttpContext context, ApiSessionService sessions, ApiLoginThrottle throttle) {
        if (!throttle.TryAcquire(context, request.UserName)) {
            context.Response.Headers.RetryAfter = "60";
            return Error(429, "Login is temporarily rate limited.", "api.login-throttled");
        }
        var result = await sessions.LoginAsync(request, context.RequestAborted);
        if (result is not null) {
            return Results.Ok(result);
        }
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Error(401, "Invalid username or password.", "api.invalid-credentials");
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IApiTokenRegistry registry, IClock clock) {
        await registry.RevokeAsync(context.Features.Get<ValidatedApiCredential>()!.Id, clock.GetUtcNow(), context.RequestAborted);
        return Results.NoContent();
    }

    private static Task<ApiTokenPage> ListTokensAsync([Description("Optional account-name or credential-label search text.")] string? search, [Description("Zero-based result offset; defaults to zero.")] int? offset, [Description("Maximum entries to return, from 1 through 100; defaults to 25.")] int? pageSize, [Description("Credential category; 0 Machine, 1 UserSession, 2 AdministratorSession. Omitted means Machine.")] ApiCredentialKind? kind,
        ApiTokenAdministrationService service, CancellationToken cancellationToken) {
        if (kind is { } selected && !Enum.IsDefined(selected)) {
            throw new ArgumentException("Unknown credential kind.");
        }
        return service.SearchAsync(new(search ?? string.Empty, offset ?? 0, pageSize ?? 25, kind ?? ApiCredentialKind.Machine), cancellationToken);
    }

    internal static IResult Error(int status, string message, string code) =>
        Results.Json(new ApiErrorResponse([new(code, message, ErrorSeverity.Error)]), statusCode: status);
}

internal sealed class ApiAccessExceptionFilter(ILogger<ApiAccessExceptionFilter> logger) : IEndpointFilter {
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try {
            return await next(context);
        } catch (ApiLoginBusyException) {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            return ApiAccessEndpoints.Error(429, "Login is temporarily rate limited.", "api.login-throttled");
        } catch (ApiUserConflictException exception) {
            return ApiAccessEndpoints.Error(409, exception.Message, "api.account-conflict");
        } catch (KeyNotFoundException) {
            return ApiAccessEndpoints.Error(404, "The account no longer exists.", "api.account-not-found");
        } catch (UnauthorizedAccessException) {
            return ApiAccessEndpoints.Error(403, "Access administration is not authorized.", "api.authorization-forbidden");
        } catch (ArgumentException) {
            return ApiAccessEndpoints.Error(400, "The request contains invalid or missing fields.", "api.access-invalid");
        } catch (InvalidOperationException) {
            return ApiAccessEndpoints.Error(400, "The requested access operation is invalid.", "api.access-invalid");
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            logger.LogError("API access operation failed: {ErrorType}.", exception.GetType().Name);
            return ApiAccessEndpoints.Error(503, "The access store is unavailable.", "api.access-unavailable");
        }
    }
}
