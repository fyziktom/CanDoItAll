using CanDoItAll.FileTools.Integration;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CanDoItAll.Web.Infrastructure;

public static class ManagedFilesEndpointRoutes
{
    public const string FileHandleHeaderName = "X-CanDoItAll-File-Handle";

    private const string FilesTag = "Files";
    private const string FileContentType = "application/octet-stream";

    public static IEndpointRouteBuilder MapCanDoItAllManagedFiles(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/authorized-files/content",
                GetAuthorizedFileContentAsync)
            .WithTags(FilesTag)
            .Produces(StatusCodes.Status200OK, typeof(Stream), FileContentType)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ApplyApiAuthorization(endpoints);
        endpoints.MapGet(
                "/authorized-files/download",
                DownloadAuthorizedFileAsync)
            .WithTags(FilesTag)
            .Produces(StatusCodes.Status200OK, typeof(Stream), FileContentType)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ApplyApiAuthorization(endpoints);
        endpoints.MapGet(
                "/storage/objects/preview",
                PreviewStorageObjectAsync)
            .WithTags(FilesTag)
            .Produces(StatusCodes.Status200OK, typeof(Stream), FileContentType)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ApplyApiAuthorization(endpoints);
        endpoints.MapGet(
                "/storage/objects/download",
                DownloadStorageObjectAsync)
            .WithTags(FilesTag)
            .Produces(StatusCodes.Status200OK, typeof(Stream), FileContentType)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ApplyApiAuthorization(endpoints);
        endpoints.MapGet(
                "/managed-files/{**path}",
                RejectManagedFilePath)
            .WithTags(FilesTag)
            .ProducesProblem(StatusCodes.Status410Gone)
            .ApplyApiAuthorization(endpoints);
        return endpoints;
    }

    /// <summary>
    /// Return the content of a file identified by an authorized file handle, for display in the client.
    /// </summary>
    /// <remarks>
    /// Send the handle in the <c>X-CanDoItAll-File-Handle</c> request header. A handle is an opaque 43-character
    /// value issued by the application's file browsing and file opening features; no operation in this document
    /// issues one. It is valid for a short time (five minutes unless the host is configured otherwise), only for the
    /// operations it was issued for (viewing or downloading) and only for the identity, session and active database
    /// that obtained it. The route reads only the header: a <c>ref</c> query-string value, as found in some stored
    /// display addresses, is ignored.
    ///
    /// The response body is the stored bytes of the file, with the file's stored media type as its
    /// <c>Content-Type</c> and without a download file name. Files larger than the host's file interaction limit
    /// (64 MiB unless configured otherwise) are refused. To receive the file as an attachment with its file name, use
    /// <c>GET /authorized-files/download</c>. <c>GET /storage/objects/preview</c> is an identical alias of this route.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token issued by this host, and the handle must
    /// have been issued for that token's subject and session. With authorization disabled (the development default),
    /// any caller of this host process can use a handle issued in it while the active database stays the same.
    /// Reading the file changes nothing.
    /// </remarks>
    /// <response code="200">
    /// The file content. The actual <c>Content-Type</c> is the stored media type of the file, which can differ from the
    /// declared <c>application/octet-stream</c>.
    /// </response>
    /// <response code="401">
    /// The <c>X-CanDoItAll-File-Handle</c> header is missing or blank. When API authorization is enabled, a missing or
    /// invalid bearer token is also rejected with 401, but with the general <c>errors</c> envelope (code
    /// <c>api.authorization-required</c>) instead of a problem document.
    /// </response>
    /// <response code="403">
    /// The handle does not authorize this request: it is malformed, unknown, expired or revoked, was issued for
    /// another caller, session or database, does not allow viewing, its storage source is unavailable, or the file
    /// exceeds the interaction limit. Obtain a new handle from the application.
    /// </response>
    internal static Task<IResult> GetAuthorizedFileContentAsync(
        HttpContext context,
        IAuthorizedFileHttpContentService service) =>
        HandleAuthorizedFileAsync(ReadHandle(context), download: false, context, service);

    /// <summary>
    /// Download a file identified by an authorized file handle as an attachment with its file name.
    /// </summary>
    /// <remarks>
    /// Send the handle in the <c>X-CanDoItAll-File-Handle</c> request header. A handle is an opaque 43-character
    /// value issued by the application's file browsing and file opening features; no operation in this document
    /// issues one. It is valid for a short time (five minutes unless the host is configured otherwise), only for the
    /// operations it was issued for (viewing or downloading) and only for the identity, session and active database
    /// that obtained it. The route reads only the header: a <c>ref</c> query-string value, as found in some stored
    /// display addresses, is ignored.
    ///
    /// The response body is the stored bytes of the file, with the file's stored media type as its
    /// <c>Content-Type</c> and a <c>Content-Disposition: attachment</c> header carrying the file's display name. Files
    /// larger than the host's file interaction limit (64 MiB unless configured otherwise) are refused. To display the
    /// file instead, use <c>GET /authorized-files/content</c>. <c>GET /storage/objects/download</c> is an identical
    /// alias of this route.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token issued by this host, and the handle must
    /// have been issued for that token's subject and session. With authorization disabled (the development default),
    /// any caller of this host process can use a handle issued in it while the active database stays the same.
    /// Downloading the file changes nothing.
    /// </remarks>
    /// <response code="200">
    /// The file content as an attachment. The actual <c>Content-Type</c> is the stored media type of the file, which
    /// can differ from the declared <c>application/octet-stream</c>.
    /// </response>
    /// <response code="401">
    /// The <c>X-CanDoItAll-File-Handle</c> header is missing or blank. When API authorization is enabled, a missing or
    /// invalid bearer token is also rejected with 401, but with the general <c>errors</c> envelope (code
    /// <c>api.authorization-required</c>) instead of a problem document.
    /// </response>
    /// <response code="403">
    /// The handle does not authorize this request: it is malformed, unknown, expired or revoked, was issued for
    /// another caller, session or database, does not allow downloading, its storage source is unavailable, or the file
    /// exceeds the interaction limit. Obtain a new handle from the application.
    /// </response>
    internal static Task<IResult> DownloadAuthorizedFileAsync(
        HttpContext context,
        IAuthorizedFileHttpContentService service) =>
        HandleAuthorizedFileAsync(ReadHandle(context), download: true, context, service);

    /// <summary>
    /// Return the content of a stored object identified by an authorized file handle, for display in the client.
    /// </summary>
    /// <remarks>
    /// Identical alias of <c>GET /authorized-files/content</c>, kept for storage display addresses: same handle header,
    /// response, limits, authority and failures. The handle is read only from the <c>X-CanDoItAll-File-Handle</c>
    /// request header; a <c>ref</c> query-string value, as found in stored display addresses such as
    /// <c>/storage/objects/preview?ref={reference}</c>, is ignored, so such an address alone is answered with 401.
    ///
    /// A handle is an opaque 43-character value issued by the application's file browsing and file opening features;
    /// no operation in this document issues one. It is valid for a short time (five minutes unless the host is
    /// configured otherwise), only for the operations it was issued for and only for the identity, session and active
    /// database that obtained it.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token issued by this host, and the handle must
    /// have been issued for that token's subject and session. With authorization disabled (the development default),
    /// any caller of this host process can use a handle issued in it while the active database stays the same.
    /// </remarks>
    /// <response code="200">
    /// The object content. The actual <c>Content-Type</c> is the stored media type of the object, which can differ from
    /// the declared <c>application/octet-stream</c>.
    /// </response>
    /// <response code="401">
    /// The <c>X-CanDoItAll-File-Handle</c> header is missing or blank. When API authorization is enabled, a missing or
    /// invalid bearer token is also rejected with 401, but with the general <c>errors</c> envelope (code
    /// <c>api.authorization-required</c>) instead of a problem document.
    /// </response>
    /// <response code="403">
    /// The handle does not authorize this request: it is malformed, unknown, expired or revoked, was issued for
    /// another caller, session or database, does not allow viewing, its storage source is unavailable, or the object
    /// exceeds the interaction limit. Obtain a new handle from the application.
    /// </response>
    internal static Task<IResult> PreviewStorageObjectAsync(
        HttpContext context,
        IAuthorizedFileHttpContentService service) =>
        HandleAuthorizedFileAsync(ReadHandle(context), download: false, context, service);

    /// <summary>
    /// Download a stored object identified by an authorized file handle as an attachment with its file name.
    /// </summary>
    /// <remarks>
    /// Identical alias of <c>GET /authorized-files/download</c>, kept for storage display addresses: same handle
    /// header, response, limits, authority and failures. The handle is read only from the
    /// <c>X-CanDoItAll-File-Handle</c> request header; a <c>ref</c> query-string value, as found in stored display
    /// addresses such as <c>/storage/objects/download?ref={reference}</c>, is ignored, so such an address alone is
    /// answered with 401.
    ///
    /// A handle is an opaque 43-character value issued by the application's file browsing and file opening features;
    /// no operation in this document issues one. It is valid for a short time (five minutes unless the host is
    /// configured otherwise), only for the operations it was issued for and only for the identity, session and active
    /// database that obtained it.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token issued by this host, and the handle must
    /// have been issued for that token's subject and session. With authorization disabled (the development default),
    /// any caller of this host process can use a handle issued in it while the active database stays the same.
    /// </remarks>
    /// <response code="200">
    /// The object content as an attachment. The actual <c>Content-Type</c> is the stored media type of the object,
    /// which can differ from the declared <c>application/octet-stream</c>.
    /// </response>
    /// <response code="401">
    /// The <c>X-CanDoItAll-File-Handle</c> header is missing or blank. When API authorization is enabled, a missing or
    /// invalid bearer token is also rejected with 401, but with the general <c>errors</c> envelope (code
    /// <c>api.authorization-required</c>) instead of a problem document.
    /// </response>
    /// <response code="403">
    /// The handle does not authorize this request: it is malformed, unknown, expired or revoked, was issued for
    /// another caller, session or database, does not allow downloading, its storage source is unavailable, or the
    /// object exceeds the interaction limit. Obtain a new handle from the application.
    /// </response>
    internal static Task<IResult> DownloadStorageObjectAsync(
        HttpContext context,
        IAuthorizedFileHttpContentService service) =>
        HandleAuthorizedFileAsync(ReadHandle(context), download: true, context, service);

    /// <summary>
    /// Reject a retired direct managed-file address; files are served only through authorized file handles.
    /// </summary>
    /// <remarks>
    /// Addresses of the form <c>/managed-files/{path}</c> were once used to read workspace files by path. They are no
    /// longer served: every request that reaches this route is answered with HTTP 410, whatever the path, and no file
    /// is read. Use <c>GET /authorized-files/content</c> or <c>GET /authorized-files/download</c> with a handle
    /// obtained from the application instead.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token issued by this host is required before the
    /// 410 answer; without one the request is rejected with 401 and the general <c>errors</c> envelope.
    /// </remarks>
    /// <response code="410">Direct managed-file paths are no longer accepted; use an authorized file handle.</response>
    internal static ProblemHttpResult RejectManagedFilePath() =>
        TypedResults.Problem(
            "Direct managed-file paths are no longer accepted. Use an authorized file handle.",
            statusCode: StatusCodes.Status410Gone);

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
