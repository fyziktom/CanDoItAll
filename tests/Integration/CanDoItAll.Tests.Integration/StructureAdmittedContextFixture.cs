using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.AgentContext;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Integration.Runtime;

internal static class StructureAdmittedContextFixture {
    internal static IReadOnlyList<IAgentChatContextAttachmentCodec> Codecs() =>
        [new ProjectStructureInvocationSnapshotCodec(), new ProjectStructureGanttObservationCodec()];

    internal static AgentChatContextInvocation Capture(Guid agentId, Guid projectId, DatabaseProfileGeneration generation,
        ProjectStructureAgentChatView view = ProjectStructureAgentChatView.Gantt) {
        var now = DateTimeOffset.UtcNow;
        var surface = new ProjectStructureSurface(projectId, "Original planning surface", [], [], null);
        var captured = ProjectStructureInvocationSnapshotMapper.Capture(surface, view, [], generation, now);
        var scope = new AgentChatContextScope(AgentChatContextScopeId.Create(),
            new(new(AgentChatTrustedSourceKinds.ProjectStructure), new(projectId.ToString("D"))),
            surface.ProjectName, WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted);
        var contributors = new List<AgentChatContextContributorPublication> {
            new(new(new("project-structure.selection"), 100, "Original captured Structure planning context."), [captured.AttachmentDraft])
        };
        if (view == ProjectStructureAgentChatView.Gantt) {
            var observation = new ProjectStructureGanttObservation(projectId, ProjectStructureGanttObservationCompleteness.Ready,
                7, 4, 2, 1, 0, now, now.AddDays(3), ["Original bounded schedule observation."], "original-row-order", null, now);
            contributors.Add(ProjectStructureGanttObservationContributor.BuildPublication(observation, generation, now,
                now.Add(ProjectStructureInvocationSnapshotMapper.FreshnessLifetime)));
        }
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var publication = registry.PublishModuleContext(new(scope, contributors));
        return AgentChatContextInvocationFactory.Create(registry.Capture(), agentId, null,
            "Read the original authorized planning facts.", AgentExecutionOperationId.New(), generation, now);
    }
}
