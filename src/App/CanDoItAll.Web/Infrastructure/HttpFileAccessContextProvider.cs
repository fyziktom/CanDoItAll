using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CanDoItAll.Web.Infrastructure;

internal sealed class HttpFileAccessContextProvider(
    IHttpContextAccessor httpContextAccessor,
    IOptions<ApiAccessOptions> apiOptions,
    ICanonicalRuntimeDatabase runtimeDatabase) : IFileAccessContextProvider
{
    public ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (!apiOptions.Value.Authorization.Enabled)
        {
            return ValueTask.FromResult(new FileAccessContext(
                new FileAccessActorId(LocalWorkspaceFileAccessPolicy.ActorId),
                new FileAccessSessionId($"runtime-{Environment.ProcessId}"),
                runtimeDatabase.Profile.Profile.Id,
                runtimeDatabase.Generation,
                authorizationRevision: 0,
                new FileAccessCorrelationId(httpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"))));
        }

        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Forbidden,
                "An authenticated file access context is required.");
        }

        string actor = httpContext.User.FindFirstValue("sub") ??
                       httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                       throw new FileAccessDeniedException(
                           FileAccessFailureCode.Forbidden,
                           "The authenticated file access context has no actor identifier.");
        string session = httpContext.User.FindFirstValue("jti") ??
                         throw new FileAccessDeniedException(
                             FileAccessFailureCode.Forbidden,
                             "The authenticated file access context has no session identifier.");
        long authorizationRevision = long.TryParse(
            httpContext.User.FindFirstValue("auth_rev"),
            out long parsedRevision)
            ? parsedRevision
            : 0;
        return ValueTask.FromResult(new FileAccessContext(
            new FileAccessActorId(actor),
            new FileAccessSessionId(session),
            runtimeDatabase.Profile.Profile.Id,
            runtimeDatabase.Generation,
            authorizationRevision,
            new FileAccessCorrelationId(httpContext.TraceIdentifier)));
    }
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
