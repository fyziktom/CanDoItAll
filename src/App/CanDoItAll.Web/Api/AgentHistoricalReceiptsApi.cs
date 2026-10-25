using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Web.Api;

internal static class AgentHistoricalReceiptsApi {
    internal const string EndpointName = "ReadAgentSavedEffectReceipts";
    internal const string InvalidRequestCode = "agents.historical-receipt-request-invalid";

    internal static void MapAgentHistoricalReceiptsApi(this RouteGroupBuilder agents) {
        agents.MapPost("/execution-runs/{executionRunId:guid}/saved-receipts/read", ReadAsync)
            .WithName(EndpointName)
            .WithApiPermission(ApiAccessScopeNames.ReadAgents)
            .WithSummary("Review one page of original saved effect receipts.")
            .WithDescription("Reads at most four currently authorized original owner receipts. This explicit read does not resume execution, recover or update journal files, release coordination warnings, approve tools, or retry effects. Preserve the original request and lossless cursor revisions; a changed snapshot or current context requires explicit view refresh. Each request is limited to 4096 UTF-8 bytes. Receipt observations are ephemeral and contain no accepted payload or source credentials.")
            .Accepts<AgentHistoricalReceiptReviewRequest>("application/json")
            .Produces<AgentHistoricalReceiptReview>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden,
                StatusCodes.Status413PayloadTooLarge, StatusCodes.Status415UnsupportedMediaType, StatusCodes.Status503ServiceUnavailable);
    }

    internal static async Task<IResult> ReadAsync(Guid executionRunId, HttpContext context,
        Func<IAgentHistoricalReceiptReviewService> historicalReceipts, IOptions<JsonOptions> options, CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasJsonContentType()) {
            return Error(StatusCodes.Status415UnsupportedMediaType);
        }
        if (executionRunId == Guid.Empty) {
            return Error(StatusCodes.Status400BadRequest);
        }
        if (context.Request.ContentLength > AgentHistoricalReceiptReviewRequest.MaximumUtf8Bytes) {
            return Error(StatusCodes.Status413PayloadTooLarge);
        }
        AgentHistoricalReceiptReviewRequest request;
        try {
            var bytes = new byte[AgentHistoricalReceiptReviewRequest.MaximumUtf8Bytes + 1];
            var length = await context.Request.Body.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
            if (length > AgentHistoricalReceiptReviewRequest.MaximumUtf8Bytes) {
                return Error(StatusCodes.Status413PayloadTooLarge);
            }
            request = JsonSerializer.Deserialize<AgentHistoricalReceiptReviewRequest>(bytes.AsSpan(0, length), options.Value.SerializerOptions)
                ?? throw new JsonException("An original receipt review reference is required.");
            if (request.ExecutionRunId != executionRunId) {
                return Error(StatusCodes.Status400BadRequest);
            }
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            return Error(StatusCodes.Status400BadRequest);
        }
        try {
            return Results.Ok(await historicalReceipts().ReadSavedEffectReceiptsAsync(request, cancellationToken));
        } catch (NotSupportedException) {
            return Error(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static IResult Error(int status) => ApiAccessEndpoints.Error(status,
        "The saved receipt review request is invalid or unavailable; no execution was requested.", InvalidRequestCode);
}
