using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Web.Api;

internal static class WorkspaceSettingsApi {
    internal const int MaximumRequestBytes = 64 * 1024;
    private const string RequestTooLargeCode = "settings.workspace-request-too-large";

    public static void MapWorkspaceSettings(this RouteGroupBuilder api) {
        var settings = api.MapGroup("/settings/workspace")
            .WithApiSection(ApiAccessScopeNames.ReadWorkspaceSettings, ApiAccessScopeNames.WriteWorkspaceSettings);
        settings.MapGet("", (WorkspaceService workspace, CancellationToken cancellationToken) => workspace.ReadSettingsSnapshotAsync(cancellationToken))
            .WithName("GetWorkspaceSettings").Produces<WorkspaceSettingsSnapshot>().ProducesApiErrors(401, 403, 503)
            .DescribeApi("Read workspace settings", "Acquire the current Workspace defaults and their original dataset/head expectation. Requires workspace-settings read capability. This observation cannot resolve an earlier unconfirmed replacement.", "Coherent current defaults and mandatory original expectation, including an explicit empty head.");
        settings.MapPut("", SaveAsync).WithName("SaveWorkspaceSettings")
            .Accepts<ReplaceWorkspaceSettingsCommand>("application/json")
            .Produces<WorkspaceSettingsSaveOutcome.Accepted>()
            .Produces<WorkspaceSettingsSaveOutcome.Rejected>(400)
            .Produces<WorkspaceSettingsSaveOutcome.Rejected>(409)
            .Produces<WorkspaceSettingsSaveOutcome.Unconfirmed>(503)
            .ProducesApiErrors(401, 403, 413, 415)
            .DescribeApi("Conditionally replace workspace settings", "Replace all six defaults only when the original dataset and head revision still match. The response identifies this attempt's exact accepted snapshot; it is not a later read. An unconfirmed attempt must not be automatically retried. This route has no durable replay receipt.", "Exact accepted snapshot with optional postcommit diagnostic, known rejection, or unconfirmed commit outcome.", "The mandatory original expectation and all six proposed values; omitted expectations and unknown members are invalid. Maximum UTF-8 request body is 65536 bytes.");
        settings.MapGet("/v2", ReadV2Async).WithName("GetWorkspaceSettingsV2")
            .Produces<WorkspaceSettingsSnapshotV2>().ProducesApiErrors(401, 403, 503)
            .DescribeApi("Read workspace defaults and decision policy", "Acquire all six defaults, the complete configured root policy or explicit legacy absence, and their original dataset/head. Invalid stored policy returns 503 without substituting defaults.", "A coherent version-two observation; this read cannot resolve an earlier unconfirmed replacement.");
        settings.MapPut("/v2", SaveV2Async).WithName("SaveWorkspaceSettingsV2")
            .Accepts<ReplaceWorkspaceSettingsV2Command>("application/json")
            .Produces<WorkspaceSettingsSaveOutcomeV2.Accepted>()
            .Produces<WorkspaceSettingsSaveOutcomeV2.Rejected>(400)
            .Produces<WorkspaceSettingsSaveOutcomeV2.Rejected>(409)
            .Produces<WorkspaceSettingsSaveOutcomeV2.Unconfirmed>(503)
            .ProducesApiErrors(401, 403, 413, 415)
            .DescribeApi("Conditionally replace workspace defaults and decision policy", "Replace defaults and a complete root policy under the original dataset/head CAS. Workspace has no parent to inherit from. This write does not activate producers or alter already admitted decisions.", "Exact version-two acceptance, rejection or unconfirmed outcome; never automatically retry an unconfirmed write.", "Schema version two, original expectation, all six defaults and complete nonnull policy. Maximum UTF-8 body is 65536 bytes.");
    }

    private static async Task<IResult> ReadV2Async(WorkspaceService workspace, CancellationToken cancellationToken) {
        try {
            return Results.Json(await workspace.ReadSettingsSnapshotV2Async(cancellationToken));
        } catch (WorkspaceSettingsPolicyUnavailableException) {
            return Results.Json(new ApiErrorResponse([
                new("settings.workspace-policy-unavailable", "The stored Workspace decision policy is invalid or unsupported; no replacement policy was assumed.", ErrorSeverity.Error)
            ]), statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> SaveV2Async(WorkspaceService workspace, IProviderProfileRegistry providers,
        HttpContext context, IOptions<HttpJsonOptions> options, CancellationToken cancellationToken) {
        if (!context.Request.HasJsonContentType()) {
            return Results.Json(new ApiErrorResponse([
                new("settings.workspace-media-type", "Workspace settings require a JSON request body; this attempt was not dispatched.", ErrorSeverity.Warning)
            ]), statusCode: StatusCodes.Status415UnsupportedMediaType);
        }
        if (await ReadBodyAsync(context.Request, cancellationToken) is not { } bytes) {
            return RequestTooLarge();
        }
        ReplaceWorkspaceSettingsV2Command? command;
        try {
            command = JsonSerializer.Deserialize<ReplaceWorkspaceSettingsV2Command>(bytes.Span, options.Value.SerializerOptions);
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            return Outcome(new WorkspaceSettingsSaveOutcomeV2.Rejected(WorkspaceSettingsRejectionReason.InvalidValues));
        }
        if (command is null || command.Values.DefaultProviderProfileId is { } providerId &&
                (await providers.GetProviderAsync(providerId, cancellationToken))?.IsEnabled != true) {
            return Outcome(new WorkspaceSettingsSaveOutcomeV2.Rejected(WorkspaceSettingsRejectionReason.InvalidValues));
        }
        return Outcome(await workspace.SaveSettingsV2Async(command, cancellationToken));
    }

    private static async Task<IResult> SaveAsync(WorkspaceService workspace, IProviderProfileRegistry providers,
        HttpContext context, IOptions<HttpJsonOptions> options, CancellationToken cancellationToken) {
        if (await ReadBodyAsync(context.Request, cancellationToken) is not { } bytes) {
            return RequestTooLarge();
        }
        ReplaceWorkspaceSettingsCommand? command;
        try {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (HasDuplicateMembers(root) || root.EnumerateObject().Any(property =>
                    string.Equals(property.Name, "values", StringComparison.OrdinalIgnoreCase) && HasDuplicateMembers(property.Value))) {
                return InvalidValues();
            }
            command = root.Deserialize<ReplaceWorkspaceSettingsCommand>(options.Value.SerializerOptions);
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            return InvalidValues();
        }
        if (command is null) {
            return InvalidValues();
        }
        if (command.Values.DefaultProviderProfileId is { } providerId &&
                (await providers.GetProviderAsync(providerId, cancellationToken))?.IsEnabled != true) {
            return InvalidValues();
        }
        return Outcome(await workspace.SaveSettingsAsync(command, cancellationToken));
    }

    private static async Task<ReadOnlyMemory<byte>?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken) {
        if (request.ContentLength is > MaximumRequestBytes) {
            return null;
        }
        var bytes = new byte[MaximumRequestBytes + 1];
        var length = 0;
        while (length < bytes.Length) {
            var read = await request.Body.ReadAsync(bytes.AsMemory(length), cancellationToken);
            if (read == 0) {
                break;
            }
            length += read;
        }
        if (length > MaximumRequestBytes) {
            return null;
        }
        return bytes.AsMemory(0, length);
    }

    private static IResult Outcome(WorkspaceSettingsSaveOutcomeV2 outcome) => Results.Json(outcome, statusCode: outcome switch {
        WorkspaceSettingsSaveOutcomeV2.Accepted => StatusCodes.Status200OK,
        WorkspaceSettingsSaveOutcomeV2.Rejected { Reason: WorkspaceSettingsRejectionReason.InvalidValues } => StatusCodes.Status400BadRequest,
        WorkspaceSettingsSaveOutcomeV2.Rejected => StatusCodes.Status409Conflict,
        WorkspaceSettingsSaveOutcomeV2.Unconfirmed => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    });

    private static bool HasDuplicateMembers(JsonElement value) {
        if (value.ValueKind != JsonValueKind.Object) {
            return true;
        }
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        return value.EnumerateObject().Any(property => !names.Add(property.Name));
    }

    private static IResult InvalidValues() => Outcome(new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.InvalidValues));

    private static IResult RequestTooLarge() => Results.Json(new ApiErrorResponse([
        new(RequestTooLargeCode, "The Workspace request exceeds the 65536-byte limit; this attempt was not dispatched.", ErrorSeverity.Warning)
    ]), statusCode: StatusCodes.Status413PayloadTooLarge);

    private static IResult Outcome(WorkspaceSettingsSaveOutcome outcome) => Results.Json(outcome, statusCode: outcome switch {
        WorkspaceSettingsSaveOutcome.Accepted => StatusCodes.Status200OK,
        WorkspaceSettingsSaveOutcome.Rejected { Reason: WorkspaceSettingsRejectionReason.InvalidValues } => StatusCodes.Status400BadRequest,
        WorkspaceSettingsSaveOutcome.Rejected => StatusCodes.Status409Conflict,
        WorkspaceSettingsSaveOutcome.Unconfirmed => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    });
}
