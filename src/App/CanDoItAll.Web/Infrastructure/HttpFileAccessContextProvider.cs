using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CanDoItAll.Web.Infrastructure;

internal sealed class HttpFileAccessContextProvider(
    IHttpContextAccessor httpContextAccessor,
    IOptions<ApiAccessOptions> apiOptions,
    ICanonicalRuntimeDatabase runtimeDatabase,
    IInteractiveAccessPrincipalProvider interactiveAccessPrincipalProvider) : IFileAccessContextProvider
{
    public async ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (!apiOptions.Value.Authorization.Enabled)
        {
            return new FileAccessContext(
                new FileAccessActorId(LocalWorkspaceFileAccessPolicy.ActorId),
                new FileAccessSessionId($"runtime-{Environment.ProcessId}"),
                runtimeDatabase.Profile.Profile.Id,
                runtimeDatabase.Generation,
                authorizationRevision: 0,
                new FileAccessCorrelationId(httpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N")));
        }

        ClaimsPrincipal principal;
        string correlationId;
        if (interactiveAccessPrincipalProvider.IsAvailable)
        {
            principal = await interactiveAccessPrincipalProvider.GetCurrentAsync(cancellationToken);
            correlationId = httpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        }
        else if (httpContext?.User.Identity?.IsAuthenticated == true)
        {
            principal = httpContext.User;
            correlationId = httpContext.TraceIdentifier;
        }
        else
        {
            throw AuthenticationRequired();
        }

        if (principal.Identity?.IsAuthenticated != true)
        {
            throw AuthenticationRequired();
        }

        string actor = principal.FindFirstValue("sub") ??
                       principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                       throw new FileAccessDeniedException(
                           FileAccessFailureCode.Forbidden,
                           "The authenticated file access context has no actor identifier.");
        string session = principal.FindFirstValue("jti") ??
                         throw new FileAccessDeniedException(
                             FileAccessFailureCode.Forbidden,
                             "The authenticated file access context has no session identifier.");
        long authorizationRevision = long.TryParse(
            principal.FindFirstValue("auth_rev"),
            out long parsedRevision)
            ? parsedRevision
            : 0;
        return new FileAccessContext(
            new FileAccessActorId(actor),
            new FileAccessSessionId(session),
            runtimeDatabase.Profile.Profile.Id,
            runtimeDatabase.Generation,
            authorizationRevision,
            new FileAccessCorrelationId(correlationId));
    }

    private static FileAccessDeniedException AuthenticationRequired() => new(
        FileAccessFailureCode.Forbidden,
        "An authenticated file access context is required.");
}

internal sealed class WebFileAccessPolicy(
    IOptions<ApiAccessOptions> apiOptions,
    ICanonicalRuntimeDatabase runtimeDatabase) : IFileAccessPolicy
{
    public ValueTask AuthorizeAsync(
        FileAccessGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool validActor = apiOptions.Value.Authorization.Enabled
            ? !string.Equals(request.Context.ActorId.Value, LocalWorkspaceFileAccessPolicy.ActorId, StringComparison.Ordinal)
            : string.Equals(request.Context.ActorId.Value, LocalWorkspaceFileAccessPolicy.ActorId, StringComparison.Ordinal);
        if (!validActor ||
            request.Context.RuntimeProfileId != runtimeDatabase.Profile.Profile.Id ||
            request.Context.RuntimeGeneration != runtimeDatabase.Generation)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Forbidden,
                "The current file access context is not authorized for this runtime.");
        }

        return ValueTask.CompletedTask;
    }
}
