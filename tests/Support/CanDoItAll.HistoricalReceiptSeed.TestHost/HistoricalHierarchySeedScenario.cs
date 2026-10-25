using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.HistoricalReceipts;

internal sealed record HistoricalHierarchySeedResult(IReadOnlyList<Guid> OperationIds, IReadOnlyList<AgentProjectStructureLifetime> Targets);

internal static class HistoricalHierarchySeedScenario {
    internal static async Task<HistoricalHierarchySeedResult> CreateAsync(IServiceProvider services, AgentToolAdmissionJournal journal,
        AgentToolRunLease lease, IAgentRuntimeToolProvider provider, AgentRuntimeToolProviderContext context, IReadOnlyList<AITool> tools,
        ProjectWriteAdmission parent, JsonSerializerOptions json, CancellationToken cancellationToken, bool definitions = false) {
        List<Guid> operations = [];
        List<ProjectWriteAdmission> targets = [];
        var resultJson = new JsonSerializerOptions(json);
        resultJson.Converters.Add(new JsonStringEnumConverter<ProjectStatus>());
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        for (var index = 0; index < 3; index++) {
            var result = await InvokeAsync(ProjectStructureToolPolicy.ProjectStructureSubprojectCreate, new() {
                ["parentProjectId"] = parent.ProjectId,
                ["request"] = new ProjectStructureProjectSaveRequest($"Original restart hierarchy child {index}", "", "", "") {
                    ExpectedParentAdmission = parent
                }
            }, retainResult: true);
            var summary = result.Deserialize<ProjectSummary>(resultJson) ?? throw new InvalidDataException("The created target has no acknowledgement.");
            targets.Add(await admissions.CaptureAsync(summary.Id, cancellationToken)
                ?? throw new InvalidOperationException("The created target lifetime is missing."));
        }
        if (definitions) {
            for (var index = 0; index < 2; index++) {
                var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(targets[index].ProjectId, cancellationToken)).Value
                    ?? throw new InvalidOperationException("The original created definition is unavailable.");
                await InvokeAsync(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, new() {
                    ["projectId"] = targets[index].ProjectId,
                    ["request"] = new ProjectStructureProjectSaveRequest($"Original restart created target edit {index}", "", "", "") {
                        ExpectedDefinition = original.Expectation
                    }
                }, retainResult: index == 0);
            }
        } else {
            var linked = await InvokeAsync(ProjectStructureToolPolicy.ProjectStructureSubprojectLink, Arguments(targets[0], targets[1]), retainResult: true);
            var edge = linked.Deserialize<ProjectStructureHierarchyAcknowledgement>(json)
                ?? throw new InvalidDataException("The original link acknowledgement is missing.");
            var reconnect = await InvokeAsync(ProjectStructureToolPolicy.ProjectStructureSubprojectLink,
                Arguments(targets[2], targets[1], new(edge.EdgeId, targets[0], targets[1])), retainResult: false);
            if (reconnect.Deserialize<ProjectStructureHierarchyAcknowledgement>(json) is not { Reconnected: true }) {
                throw new InvalidDataException("The actual reconnect did not acknowledge its original accepted effect.");
            }
        }
        return new(operations, targets.Select(target => new AgentProjectStructureLifetime(target.DatabaseProfileId, target.ProjectId, target.LifetimeId)).ToArray());

        async Task<JsonElement> InvokeAsync(string name, Dictionary<string, object?> arguments, bool retainResult) {
            var metadata = provider.GetToolMetadata(context).Single(item => item.ToolName == name);
            var payload = (metadata.PrepareAdmission ?? throw new InvalidOperationException("The hierarchy seed requires durable admission."))(
                JsonSerializer.SerializeToElement(arguments, json));
            var callId = $"historical-restart-hierarchy-{operations.Count}";
            var admitted = await journal.AdmitBatchAsync(lease, payload.Digest, Envelope("{}"), [new(callId, payload, false)], cancellationToken);
            var claim = await journal.ClaimInvocationAsync(lease, admitted.Batches[^1].Id, callId, payload, cancellationToken);
            using var dispatch = claim.Bind();
            using var effect = AgentToolInvocationEffectScope.Begin();
            var result = await tools.OfType<AIFunction>().Single(item => item.Name == name).InvokeAsync(new AIFunctionArguments(arguments), cancellationToken);
            if (result is not JsonElement accepted || effect.CommittedEffect?.SourceId != claim.Proposal.IntentId.Value.ToString("D")) {
                throw new InvalidDataException("The registered tool did not acknowledge the admitted original owner effect.");
            }
            if (retainResult) {
                await journal.CompleteInvocationAsync(claim, Envelope(accepted.GetRawText()), AgentToolEffectState.Committed, cancellationToken);
            }
            operations.Add(claim.Proposal.IntentId.Value);
            return accepted;
        }
    }

    private static Dictionary<string, object?> Arguments(ProjectWriteAdmission parent, ProjectWriteAdmission child,
        ProjectHierarchyEdgeExpectation? previous = null) => new() {
        ["parentProjectId"] = parent.ProjectId,
        ["request"] = new ProjectStructureSubprojectChangeRequest(child.ProjectId, previous?.Parent.ProjectId) {
            ExpectedParentAdmission = parent, ExpectedChildAdmission = child, ExpectedCurrentEdge = previous
        }
    };

    private static AgentToolProtocolEnvelope Envelope(string json) => AgentToolProtocolEnvelope.Create("historical-seed-sdk", 1, json);
}
