using CanDoItAll.AgentFramework.Core;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class AgentAttachmentsApi
{
    private const long MultipartRequestOverheadBytes = 64 * 1024;
    private const long MultipartRequestLimitBytes =
        AgentChatAttachmentStagingService.MaxImageAttachmentBytes +
        MultipartRequestOverheadBytes;

    public static RouteGroupBuilder MapAgentAttachmentsApi(this RouteGroupBuilder group)
    {
        group.MapGroup("/agents")
            .WithTags("Agents")
            .DisableAntiforgery()
            .MapPost("/attachments/images", StageImageAsync)
            .WithName("StageAgentImageAttachment")
            .Accepts<AgentImageAttachmentUploadRequest>("multipart/form-data")
            .WithMetadata(new RequestSizeLimitAttribute(MultipartRequestLimitBytes))
            .WithMetadata(new RequestFormLimitsAttribute
            {
                MultipartBodyLengthLimit = MultipartRequestLimitBytes
            })
            .Produces<AgentChatAttachmentStagingResult>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        return group;
    }

    /// <summary>
    /// Upload an image file so that later agent chat messages and execution runs can attach it.
    /// </summary>
    /// <remarks>
    /// Stores one image in the managed workspace and returns its workspace-relative path. Send that path in
    /// <c>attachmentPaths</c> of <c>POST /api/agents/{agentId}/chat</c> or in <c>inputAttachmentPaths</c> of the
    /// execution-run operations, or of their streaming forms, to give the image to the agent; one request can attach
    /// up to 8 images. Uploading sends nothing to an agent or provider.
    ///
    /// Send <c>multipart/form-data</c> with one file part named <c>file</c>: a PNG, JPEG, GIF or WebP image of at
    /// most 10 MiB (10485760 bytes). The image type is taken from the file name extension (<c>.png</c>, <c>.jpg</c>,
    /// <c>.jpeg</c>, <c>.gif</c> or <c>.webp</c>); a part content type, when sent, must match it (<c>image/png</c>,
    /// <c>image/jpeg</c>, <c>image/gif</c> or <c>image/webp</c>). The image content itself is not inspected. A whole
    /// request larger than the image limit plus 64 KiB is rejected before the operation runs, without an error
    /// envelope.
    ///
    /// Every upload creates a new file under <c>artifacts/chat-attachments/</c> in the workspace, with a unique name
    /// derived from the uploaded file name; uploading the same image again stores another copy.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="request">The multipart form with the image in the file part <c>file</c>.</param>
    /// <response code="200">
    /// The image was stored. The body gives the workspace-relative path to attach, the image content type and the
    /// stored size in bytes.
    /// </response>
    /// <response code="400">
    /// Nothing was stored: no file part or an empty file (<c>agents.attachment-required</c>), or an image that is not
    /// accepted (<c>agents.attachment-invalid</c>): an unsupported extension, a content type that does not match the
    /// extension, more than 10 MiB, or a received size that differs from the size the part declared.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> StageImageAsync(
        [FromForm] AgentImageAttachmentUploadRequest request,
        IAgentChatAttachmentStagingService stagingService,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length <= 0)
        {
            return ApiEndpointResults.BadRequest(
                "An image attachment file is required.",
                "agents.attachment-required");
        }

        try
        {
            await using var content = request.File.OpenReadStream();
            var result = await stagingService.StageImageAsync(
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                content,
                cancellationToken);
            return Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(
                exception.Message,
                "agents.attachment-invalid");
        }
    }
}

/// <summary>
/// Multipart form of an agent image attachment upload: exactly one file part named <c>file</c>.
/// </summary>
public sealed class AgentImageAttachmentUploadRequest
{
    /// <summary>
    /// The image to store (required): a PNG, JPEG, GIF or WebP file of at most 10 MiB (10485760 bytes), recognized by
    /// its file name extension (<c>.png</c>, <c>.jpg</c>, <c>.jpeg</c>, <c>.gif</c> or <c>.webp</c>). A part content
    /// type, when sent, must match the extension.
    /// </summary>
    public IFormFile? File { get; set; }
}
