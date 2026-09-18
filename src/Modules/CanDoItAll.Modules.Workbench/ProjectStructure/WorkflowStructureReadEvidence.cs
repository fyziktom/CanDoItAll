using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed record WorkflowStructureReadEvidence(WorkflowProjectStructureOperation Operation,
    int PartIndex, int PartCount, IReadOnlyList<ProjectWriteAdmission> Targets) {
    private const int TargetsPerPart = 64;
    internal const int SchemaVersion = 1;

    internal static void Capture(WorkflowStructureReadContext context, WorkflowProjectStructureOperation operation,
        IReadOnlyList<ProjectWriteAdmission> targets) {
        if (context.CaptureReadEvidence is not { } capture) {
            return;
        }
        var actual = RequireInvocation(context);
        var parts = targets.Count == 0 ? 1 : checked((targets.Count - 1) / TargetsPerPart + 1);
        for (var index = 0; index < parts; index++) {
            var payload = new WorkflowStructureReadEvidence(operation, index, parts,
                targets.Skip(checked(index * TargetsPerPart)).Take(TargetsPerPart).ToArray());
            var evidence = new WorkflowProviderReadEvidence(WorkflowStructureReadContext.DisclosureOwner, SchemaVersion,
                actual.Occurrence!, actual.VersionId, actual.NodeId,
                JsonSerializer.Serialize(payload, WorkflowProviderDisclosureContent.JsonOptions));
            evidence.Validate();
            capture(evidence);
        }
    }

    internal static WorkflowNodeInvocationBinding RequireInvocation(WorkflowStructureReadContext context) {
        var actual = WorkflowExecutorExecutionAuditScope.CurrentInvocation;
        if (actual is null || actual.Occurrence != context.Occurrence || actual.VersionId != context.VersionId ||
                actual.NodeId != context.StepId || actual.ExecutorId != WorkflowExecutorIds.ProjectStructure ||
                actual.CompilerVersion != WorkflowProviderDisclosureProtocol.Current) {
            throw new InvalidOperationException("Protected Structure output requires the original actual Workflow node invocation. A legacy or incomplete checkpoint cannot mint new read evidence.");
        }
        return actual;
    }
}
