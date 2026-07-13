using CanDoItAll.FileTools.Integration;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CanDoItAll.Web.Infrastructure;

public static class ManagedFilesEndpointRoutes
{
    public const string FileHandleHeaderName = "X-CanDoItAll-File-Handle";

    public static IEndpointRouteBuilder MapCanDoItAllManagedFiles(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/authorized-files/content",
                (HttpContext context, IAuthorizedFileHttpContentService service) =>
                    HandleAuthorizedFileAsync(ReadHandle(context), download: false, context, service))
            .ApplyApiAuthorization(endpoints);
        endpoints.MapGet(
                "/authorized-files/download",
                (HttpContext context, IAuthorizedFileHttpContentService service) =>
                    HandleAuthorizedFileAsync(ReadHandle(context), download: true, context, service))
            .ApplyApiAuthorization(endpoints);
        endpoints.MapGet(
                "/storage/objects/preview",
                (HttpContext context, IAuthorizedFileHttpContentService service) =>
                    HandleAuthorizedFileAsync(ReadHandle(context), download: false, context, service))
            .ApplyApiAuthorization(endpoints);
        endpoints.MapGet(
                "/storage/objects/download",
                (HttpContext context, IAuthorizedFileHttpContentService service) =>
                    HandleAuthorizedFileAsync(ReadHandle(context), download: true, context, service))
            .ApplyApiAuthorization(endpoints);
        endpoints.MapGet(
                "/managed-files/{**path}",
                () => TypedResults.Problem(
                    "Direct managed-file paths are no longer accepted. Use an authorized file handle.",
                    statusCode: StatusCodes.Status410Gone))
            .ApplyApiAuthorization(endpoints);
        return endpoints;
    }

    private static string ReadHandle(HttpContext context)
        => context.Request.Headers[FileHandleHeaderName].ToString();

    private static async Task<IResult> HandleAuthorizedFileAsync(
        string handle,
        bool download,
        HttpContext httpContext,
        IAuthorizedFileHttpContentService service)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return TypedResults.Problem(
                "An authorized file handle is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        try
        {
            AuthorizedFileHttpContent content = await service.OpenAsync(
                handle,
                download ? FileAccessOperation.Download : FileAccessOperation.View,
                httpContext.RequestAborted);
            return TypedResults.File(
                content.Stream,
                content.ContentType,
                download ? content.DisplayName : null,
                enableRangeProcessing: true);
        }
        catch (FileAccessDeniedException)
        {
            return TypedResults.Problem(
                "The file is not authorized for this request.",
                statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
