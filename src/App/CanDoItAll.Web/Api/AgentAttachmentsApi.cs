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

    private static async Task<IResult> StageImageAsync(
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

public sealed class AgentImageAttachmentUploadRequest
{
    public IFormFile? File { get; set; }
}
