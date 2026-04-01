using CanDoItAll.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;

namespace CanDoItAll.Web.Infrastructure;

public static class ManagedFilesEndpointRoutes
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public static IEndpointRouteBuilder MapCanDoItAllManagedFiles(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/managed-files/{**path}", HandleManagedFileRequest);

        return endpoints;
    }

    private static IResult HandleManagedFileRequest(
        HttpContext httpContext,
        string? path,
        IWorkspacePathAccessGuard pathAccessGuard)
    {
        if (ContainsTraversalSegments(httpContext.Request.Path.Value))
        {
            return TypedResults.BadRequest("The resolved path is outside the active managed files root.");
        }

        var resolution = pathAccessGuard.ResolveManagedFilePath(path ?? string.Empty);
        if (!resolution.IsSuccess)
        {
            return TypedResults.BadRequest(resolution.Message);
        }

        if (!File.Exists(resolution.FullPath))
        {
            return TypedResults.NotFound();
        }

        var contentType = ContentTypeProvider.TryGetContentType(resolution.FullPath, out var resolvedContentType)
            ? resolvedContentType
            : "application/octet-stream";

        return TypedResults.PhysicalFile(resolution.FullPath, contentType, enableRangeProcessing: true);
    }

    private static bool ContainsTraversalSegments(string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return false;
        }

        var unescapedPath = Uri.UnescapeDataString(requestPath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var segments = unescapedPath.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Any(segment => segment is "." or "..");
    }
}
