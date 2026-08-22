using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowLaunchIdempotencyRequestFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WorkflowLaunchIdempotencyScope CreateScope(
        WorkflowLaunchIntent intent,
        WorkflowLaunchIdempotencyKey callerKey)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(intent.Selection);
        ArgumentNullException.ThrowIfNull(intent.Origin);

        var (workflowId, requestedVersionId) = intent.Selection switch
        {
            WorkflowDefinitionSelection.ExactSavedVersion exact => (exact.WorkflowId, exact.VersionId),
            WorkflowDefinitionSelection.LatestActive latest => (latest.WorkflowId, (WorkflowVersionId?)null),
            WorkflowDefinitionSelection.DraftPreview draft => (draft.Definition.Id, draft.Definition.VersionId),
            _ => throw new InvalidOperationException(
                $"Workflow definition selection '{intent.Selection.GetType().Name}' is not supported.")
        };

        return new WorkflowLaunchIdempotencyScope(
            callerKey,
            workflowId,
            intent.Selection.Kind,
            requestedVersionId,
            intent.Mode,
            intent.Origin.Kind,
            new WorkflowLaunchOriginScopeKey(Hash(CreateAuthorizedOriginScopePayload(intent.Origin))));
    }

    public static WorkflowLaunchRequestFingerprint CreateFingerprint(
        WorkflowLaunchIntent intent,
        string normalizedInputJson)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedInputJson);

        var canonicalInputJson = CanonicalizeInputJson(normalizedInputJson);
        var canonicalInputHash = HashString(canonicalInputJson);
        var payload = new RequestFingerprintPayload(
            intent.Selection,
            canonicalInputJson,
            intent.CompletionPolicy,
            intent.RequestedBackend,
            intent.PreviewSimulationPlan,
            CreateOriginAuthorizationPayload(intent.Origin));
        return new WorkflowLaunchRequestFingerprint(Hash(payload), canonicalInputHash);
    }

    public static string CreateKeyHash(WorkflowLaunchIdempotencyKey callerKey)
        => HashString(callerKey.Value);

    public static string CanonicalizeInputJson(string inputJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputJson);
        using var document = JsonDocument.Parse(inputJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "Workflow launch input must be a valid JSON object.",
                nameof(inputJson));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalJson(writer, document.RootElement);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static object CreateOriginScopePayload(WorkflowLaunchOrigin origin)
        => origin switch
        {
            WorkflowLaunchOrigin.Api api => new ActorOriginScopePayload(
                api.Actor.Kind,
                api.Actor.SubjectId,
                SessionId: null),
            WorkflowLaunchOrigin.Preview preview => new ActorOriginScopePayload(
                preview.Actor.Kind,
                preview.Actor.SubjectId,
                SessionId: null),
            WorkflowLaunchOrigin.SchedulerPlanRun scheduler => new SchedulerOriginScopePayload(
                scheduler.PlanId),
            WorkflowLaunchOrigin.ProjectStructureNode project => new ProjectOriginScopePayload(
                project.ProjectId,
                project.NodeId.Value,
                project.RequestingActor.SubjectId,
                project.SessionId.Value),
            WorkflowLaunchOrigin.AgentRuntimeInvocation agent => new ActorOriginScopePayload(
                agent.Agent.Kind,
                agent.Agent.SubjectId,
                agent.RuntimeSessionId.Value),
            WorkflowLaunchOrigin.ProcessAssignment process => new ProcessOriginScopePayload(
                process.ProcessRunId,
                process.AssignmentId),
            _ => throw new InvalidOperationException(
                $"Workflow launch origin '{origin.GetType().Name}' is not supported.")
        };

    private static AuthorizedOriginScopePayload CreateAuthorizedOriginScopePayload(
        WorkflowLaunchOrigin origin)
        => new(
            CreateOriginAuthorizationPayload(origin),
            CreateOriginScopePayload(origin));

    private static OriginAuthorizationPayload CreateOriginAuthorizationPayload(
        WorkflowLaunchOrigin origin)
    {
        if (origin.AuthorizationScope is null ||
            string.IsNullOrWhiteSpace(origin.AuthorizationPolicyFingerprint))
        {
            throw new InvalidOperationException(
                "Workflow launch idempotency requires a trusted authorization scope and policy fingerprint.");
        }

        return new OriginAuthorizationPayload(
            origin.AuthorizationScope.Kind,
            origin.AuthorizationScope.Key,
            origin.AuthorizationPolicyFingerprint);
    }

    private static string Hash<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
        return HashString(json);
    }

    private static string HashString(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                             .EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new ArgumentException(
                    $"Unsupported JSON value kind '{element.ValueKind}'.",
                    nameof(element));
        }
    }

    private sealed record RequestFingerprintPayload(
        WorkflowDefinitionSelection Selection,
        string InputJson,
        WorkflowLaunchCompletionPolicy CompletionPolicy,
        WorkflowRuntimeBackendKind? RequestedBackend,
        WorkflowPreviewSimulationPlan PreviewSimulationPlan,
        OriginAuthorizationPayload OriginAuthorization);

    private sealed record AuthorizedOriginScopePayload(
        OriginAuthorizationPayload Authorization,
        object Lineage);

    private sealed record OriginAuthorizationPayload(
        WorkspaceScopeKind ScopeKind,
        string ScopeKey,
        string PolicyFingerprint);

    private sealed record ActorOriginScopePayload(
        WorkflowLaunchActorKind ActorKind,
        string SubjectId,
        string? SessionId);

    private sealed record SchedulerOriginScopePayload(Guid PlanId);

    private sealed record ProjectOriginScopePayload(
        Guid ProjectId,
        string NodeId,
        string AgentSubjectId,
        string SessionId);

    private sealed record ProcessOriginScopePayload(
        Guid ProcessRunId,
        Guid AssignmentId);
}
