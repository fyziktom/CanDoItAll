using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkflowPayloadPolicyScope
{
    RunInput,
    EventPayload,
    ExecutorOutput,
    ExecutorError,
    ExternalRequest,
    PluginLogMessage,
    PluginLogDetails,
    ToolReceipt,
    PreviewSimulationOutput
}

public sealed record WorkflowPayloadPolicyRequest(
    WorkflowRunId? RunId,
    WorkflowPayloadPolicyScope Scope,
    string Payload,
    WorkflowArtifactKind ArtifactKind,
    string Name,
    string ContentType,
    DateTimeOffset CreatedAtUtc)
{
    public WorkflowNodeId? NodeId { get; init; }

    public bool CaptureArtifact { get; init; }

    public bool ForceArtifact { get; init; }

    public int? MaxInlinePayloadCharacters { get; init; }
}

public sealed record WorkflowPayloadPolicyResult(
    string InlinePayload,
    int OriginalPayloadCharacters,
    int RedactedPayloadCharacters,
    bool InlineTruncated,
    int MaxInlinePayloadCharacters,
    string Reference,
    WorkflowArtifactRecord? Artifact);

public interface IWorkflowPayloadPolicyService
{
    ValueTask<WorkflowPayloadPolicyResult> ApplyAsync(
        WorkflowPayloadPolicyRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class WorkflowPayloadPolicyService : IWorkflowPayloadPolicyService
{
    public const string TruncationMarker = "...[TRUNCATED]";
    private const int AbsoluteMaxInlinePayloadCharacters = 1_000_000;

    private readonly IWorkflowSettingsService? settingsService;
    private readonly IWorkflowArtifactContentStore? artifactContentStore;

    public WorkflowPayloadPolicyService(
        IWorkflowSettingsService? settingsService = null,
        IWorkflowArtifactContentStore? artifactContentStore = null)
    {
        this.settingsService = settingsService;
        this.artifactContentStore = artifactContentStore;
    }

    public async ValueTask<WorkflowPayloadPolicyResult> ApplyAsync(
        WorkflowPayloadPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var artifactPolicy = await ResolveArtifactPolicyAsync(cancellationToken);
        var maxInlineCharacters = ResolveMaxInlinePayloadCharacters(
            artifactPolicy.MaxInlinePayloadCharacters,
            request.MaxInlinePayloadCharacters);
        var originalPayload = request.Payload ?? string.Empty;
        var redactedPayload = RedactPayload(originalPayload);
        var inlinePayload = BoundPayload(redactedPayload, maxInlineCharacters);
        var inlineTruncated = originalPayload.Length > maxInlineCharacters ||
                              redactedPayload.Length > maxInlineCharacters ||
                              inlinePayload.Length < redactedPayload.Length;
        var artifact = ShouldCreateArtifact(request, artifactPolicy, inlineTruncated)
            ? CreateArtifact(
                request,
                inlineTruncated,
                originalPayload.Length,
                redactedPayload.Length,
                maxInlineCharacters)
            : null;
        if (artifact is not null && artifactContentStore is not null)
        {
            await artifactContentStore.SaveContentAsync(
                artifact,
                redactedPayload,
                cancellationToken);
        }

        return new WorkflowPayloadPolicyResult(
            inlinePayload,
            originalPayload.Length,
            redactedPayload.Length,
            inlineTruncated,
            maxInlineCharacters,
            artifact?.StoragePath ?? string.Empty,
            artifact);
    }

    private async ValueTask<WorkflowArtifactPolicy> ResolveArtifactPolicyAsync(CancellationToken cancellationToken)
    {
        var settings = settingsService is null
            ? WorkflowSettings.Default
            : await settingsService.GetSettingsAsync(cancellationToken);
        if (settings.ArtifactPolicy.MaxInlinePayloadCharacters <= 0)
        {
            throw new InvalidOperationException("Workflow artifact inline payload limit must be positive.");
        }

        return settings.ArtifactPolicy;
    }

    private static int ResolveMaxInlinePayloadCharacters(
        int policyMaxInlinePayloadCharacters,
        int? requestedMaxInlinePayloadCharacters)
    {
        var requestedMax = requestedMaxInlinePayloadCharacters.GetValueOrDefault(policyMaxInlinePayloadCharacters);
        if (requestedMax <= 0)
        {
            throw new InvalidOperationException("Workflow payload inline limit must be positive.");
        }

        return Math.Clamp(
            Math.Min(policyMaxInlinePayloadCharacters, requestedMax),
            1,
            AbsoluteMaxInlinePayloadCharacters);
    }

    private static string RedactPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        return LooksLikeJson(payload)
            ? WorkflowExecutorRedaction.RedactJson(payload, int.MaxValue)
            : WorkflowExecutorRedaction.RedactText(payload);
    }

    public static string BoundPayload(string payload, int maxInlinePayloadCharacters)
    {
        if (maxInlinePayloadCharacters <= 0)
        {
            throw new InvalidOperationException("Workflow payload inline limit must be positive.");
        }

        if (payload.Length <= maxInlinePayloadCharacters)
        {
            return payload;
        }

        if (maxInlinePayloadCharacters <= TruncationMarker.Length)
        {
            return payload[..maxInlinePayloadCharacters];
        }

        return string.Concat(
            payload.AsSpan(0, maxInlinePayloadCharacters - TruncationMarker.Length),
            TruncationMarker);
    }

    private static bool ShouldCreateArtifact(
        WorkflowPayloadPolicyRequest request,
        WorkflowArtifactPolicy artifactPolicy,
        bool inlineTruncated)
    {
        return request.RunId.HasValue &&
               request.CaptureArtifact &&
               artifactPolicy.AllowedArtifactKinds.Contains(request.ArtifactKind) &&
               (inlineTruncated || artifactPolicy.CaptureNodeOutputs && request.ForceArtifact);
    }

    private static WorkflowArtifactRecord CreateArtifact(
        WorkflowPayloadPolicyRequest request,
        bool inlineTruncated,
        int originalPayloadCharacters,
        int redactedPayloadCharacters,
        int maxInlinePayloadCharacters)
    {
        var runId = request.RunId
            ?? throw new InvalidOperationException("Workflow payload artifacts require a workflow run id.");
        var artifactId = WorkflowArtifactId.New();
        var storagePath = CreateStoragePath(runId, artifactId, request.Scope, request.ArtifactKind);
        var summary = CreateArtifactSummary(
            request.Scope,
            inlineTruncated,
            originalPayloadCharacters,
            redactedPayloadCharacters,
            maxInlinePayloadCharacters);

        return new WorkflowArtifactRecord(
            artifactId,
            runId,
            request.ArtifactKind,
            request.NodeId,
            ResolveArtifactName(request),
            ResolveContentType(request),
            storagePath,
            summary,
            request.CreatedAtUtc);
    }

    private static string CreateStoragePath(
        WorkflowRunId runId,
        WorkflowArtifactId artifactId,
        WorkflowPayloadPolicyScope scope,
        WorkflowArtifactKind artifactKind)
    {
        return $"workflow-runs/{runId.Value:N}/payloads/{ResolveScopeSegment(scope)}-{artifactId.Value:N}{ResolveExtension(artifactKind)}";
    }

    private static string CreateArtifactSummary(
        WorkflowPayloadPolicyScope scope,
        bool inlineTruncated,
        int originalPayloadCharacters,
        int redactedPayloadCharacters,
        int maxInlinePayloadCharacters)
    {
        var disposition = inlineTruncated
            ? "Inline payload was truncated and the full raw payload was not stored inline."
            : "Artifact capture was requested by workflow policy.";
        return $"{ResolveScopeDisplayName(scope)} payload reference. {disposition} Original characters: {originalPayloadCharacters}. Redacted characters: {redactedPayloadCharacters}. Inline limit: {maxInlinePayloadCharacters}.";
    }

    private static string ResolveArtifactName(WorkflowPayloadPolicyRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            return WorkflowExecutorRedaction.RedactText(request.Name).Trim();
        }

        return request.Scope switch
        {
            WorkflowPayloadPolicyScope.RunInput => "workflow-input.json",
            WorkflowPayloadPolicyScope.ExecutorOutput => "workflow-node-output.json",
            WorkflowPayloadPolicyScope.ExecutorError => "workflow-node-error.txt",
            WorkflowPayloadPolicyScope.ExternalRequest => "workflow-external-request.json",
            WorkflowPayloadPolicyScope.PluginLogMessage => "plugin-log-message.txt",
            WorkflowPayloadPolicyScope.PluginLogDetails => "plugin-log-details.json",
            WorkflowPayloadPolicyScope.ToolReceipt => "workflow-tool-receipt.json",
            WorkflowPayloadPolicyScope.PreviewSimulationOutput => "workflow-preview-output.json",
            _ => "workflow-payload.txt"
        };
    }

    private static string ResolveContentType(WorkflowPayloadPolicyRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            return WorkflowExecutorRedaction.RedactText(request.ContentType).Trim();
        }

        return request.ArtifactKind switch
        {
            WorkflowArtifactKind.Json or WorkflowArtifactKind.ToolReceipt or WorkflowArtifactKind.PreviewSimulation => "application/json",
            WorkflowArtifactKind.Text => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static string ResolveScopeSegment(WorkflowPayloadPolicyScope scope)
        => scope switch
        {
            WorkflowPayloadPolicyScope.RunInput => "run-input",
            WorkflowPayloadPolicyScope.EventPayload => "event-payload",
            WorkflowPayloadPolicyScope.ExecutorOutput => "executor-output",
            WorkflowPayloadPolicyScope.ExecutorError => "executor-error",
            WorkflowPayloadPolicyScope.ExternalRequest => "external-request",
            WorkflowPayloadPolicyScope.PluginLogMessage => "plugin-log-message",
            WorkflowPayloadPolicyScope.PluginLogDetails => "plugin-log-details",
            WorkflowPayloadPolicyScope.ToolReceipt => "tool-receipt",
            WorkflowPayloadPolicyScope.PreviewSimulationOutput => "preview-simulation-output",
            _ => "payload"
        };

    private static string ResolveScopeDisplayName(WorkflowPayloadPolicyScope scope)
        => scope switch
        {
            WorkflowPayloadPolicyScope.RunInput => "Workflow input",
            WorkflowPayloadPolicyScope.EventPayload => "Workflow event",
            WorkflowPayloadPolicyScope.ExecutorOutput => "Workflow executor output",
            WorkflowPayloadPolicyScope.ExecutorError => "Workflow executor error",
            WorkflowPayloadPolicyScope.ExternalRequest => "Workflow external request",
            WorkflowPayloadPolicyScope.PluginLogMessage => "Plugin log message",
            WorkflowPayloadPolicyScope.PluginLogDetails => "Plugin log details",
            WorkflowPayloadPolicyScope.ToolReceipt => "Workflow tool receipt",
            WorkflowPayloadPolicyScope.PreviewSimulationOutput => "Workflow preview simulation output",
            _ => "Workflow payload"
        };

    private static string ResolveExtension(WorkflowArtifactKind artifactKind)
        => artifactKind switch
        {
            WorkflowArtifactKind.Json => ".json",
            WorkflowArtifactKind.ToolReceipt => ".tool-receipt.json",
            WorkflowArtifactKind.PreviewSimulation => ".preview.json",
            WorkflowArtifactKind.Text => ".txt",
            _ => ".bin"
        };

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        return trimmed.Length > 0 && trimmed[0] is '{' or '[';
    }
}
