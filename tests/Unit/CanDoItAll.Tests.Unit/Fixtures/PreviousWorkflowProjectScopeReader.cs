using System.Text.Json;
using System.Text.Json.Serialization;

using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal sealed class PreviousWorkflowProjectScopeReader : JsonConverter<WorkflowStructureAuthority> {
    private const string AgentScopeV1 = "agent-execution/project-scope-v1";
    private const string AuthenticatedScopeV1 = "authenticated-operator/project-scope-v1";
    private const string LocalScopeV1 = "local-operator/project-scope-v1";

    public override WorkflowStructureAuthority Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var payload = JsonSerializer.Deserialize<Payload>(ref reader, options)
            ?? throw new JsonException("The saved Workflow authority is missing.");
        WorkflowStructureAuthorityChannel channel;
        if (payload.ProjectScope is not null) {
            channel = payload.Channel.ValueKind == JsonValueKind.String ? payload.Channel.GetString() switch {
                AgentScopeV1 => WorkflowStructureAuthorityChannel.AgentExecution,
                AuthenticatedScopeV1 => WorkflowStructureAuthorityChannel.AuthenticatedOperator,
                LocalScopeV1 => WorkflowStructureAuthorityChannel.LocalOperator,
                _ => throw new JsonException("The Workflow authority project-scope version is unsupported.")
            } : throw new JsonException("A scoped Workflow authority requires its versioned authority marker.");
        } else {
            channel = payload.Channel.Deserialize<WorkflowStructureAuthorityChannel>(options);
        }
        if (!Enum.IsDefined(channel)) {
            throw new JsonException("The saved Workflow authority channel is unsupported.");
        }
        var authority = new WorkflowStructureAuthority(channel, payload.Principal, payload.DatabaseProfileId,
            payload.ProjectId, payload.CanCreateTasks, payload.CanCreateAssets, payload.ExpiresAtUtc, payload.PolicyFingerprint) {
            OperatorSurface = payload.OperatorSurface,
            AgentGovernance = payload.AgentGovernance,
            ProcessAuthority = payload.ProcessAuthority,
            SchedulerAuthority = payload.SchedulerAuthority,
            AllProjects = payload.AllProjects,
            ProjectIds = payload.ProjectScope is null ? payload.ProjectIds : payload.ProjectIds.Distinct().Order().ToArray(),
            ProjectScope = payload.ProjectScope
        };
        authority.ProjectScope?.Validate(authority);
        return authority;
    }

    public override void Write(Utf8JsonWriter writer, WorkflowStructureAuthority value, JsonSerializerOptions options) {
        value.ProjectScope?.Validate(value);
        var channel = value.ProjectScope is null ? JsonSerializer.SerializeToElement(value.Channel, options)
            : JsonSerializer.SerializeToElement(value.Channel switch {
                WorkflowStructureAuthorityChannel.AgentExecution => AgentScopeV1,
                WorkflowStructureAuthorityChannel.AuthenticatedOperator => AuthenticatedScopeV1,
                WorkflowStructureAuthorityChannel.LocalOperator => LocalScopeV1,
                _ => throw new JsonException("The saved Workflow authority channel is unsupported.")
            }, options);
        JsonSerializer.Serialize(writer, new Payload(channel, value.Principal, value.DatabaseProfileId,
            value.ProjectId, value.CanCreateTasks, value.CanCreateAssets, value.ExpiresAtUtc, value.PolicyFingerprint) {
            OperatorSurface = value.OperatorSurface,
            AgentGovernance = value.AgentGovernance,
            ProcessAuthority = value.ProcessAuthority,
            SchedulerAuthority = value.SchedulerAuthority,
            AllProjects = value.AllProjects,
            ProjectIds = value.ProjectScope is null ? value.ProjectIds : value.ProjectIds.Distinct().Order().ToArray(),
            ProjectScope = value.ProjectScope
        }, options);
    }

    private sealed record Payload(JsonElement Channel, WorkflowLaunchActor Principal, Guid DatabaseProfileId,
        Guid ProjectId, bool CanCreateTasks, bool CanCreateAssets, DateTimeOffset? ExpiresAtUtc, string PolicyFingerprint) {
        public WorkflowStructureOperatorSurface OperatorSurface { get; init; }
        public AgentExecutionGovernanceSnapshot? AgentGovernance { get; init; }
        public WorkflowStructureProcessAuthority? ProcessAuthority { get; init; }
        public WorkflowStructureSchedulerAuthority? SchedulerAuthority { get; init; }
        public bool AllProjects { get; init; }
        public IReadOnlyList<Guid> ProjectIds { get; init; } = [];
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkflowStructureProjectScope? ProjectScope { get; init; }
    }
}
