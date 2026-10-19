using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectHierarchyGovernedInvocationFactory(ProjectAgentSourceMutationAuthority agents,
    ProjectProcessExecutionMutationService processes, IAgentCatalogReadLeaseStore catalog, ProjectWriteAdmissionService projects,
    ProjectLifecycleCurrentWriteAuthority currentWrite) {
    internal Task<ProjectLifecycleInvocation> ForAgentAsync(ProjectAgentMutationAdmission original,
        AgentToolSessionObservation session, AgentToolAdmittedInvocation proposal, ProjectLifecycleSubmission submission,
        CancellationToken cancellationToken) {
        var binding = ProjectDefinitionGovernedBindings.ForAgent(original, session, proposal);
        var targets = Targets(submission.Command);
        var source = ProjectGovernedInvocationSource.Bind(agents.Bind(original, targets), proposal);
        return InvocationAsync(binding, new ProjectDefinitionAgentReceiptAccess(original, catalog, projects), source, submission, cancellationToken);
    }

    internal Task<ProjectLifecycleInvocation> ForProcessAsync(ProjectProcessMutationAdmission original,
        AgentToolAdmittedInvocation proposal, ProjectLifecycleSubmission submission, CancellationToken cancellationToken) {
        var binding = ProjectDefinitionGovernedBindings.ForProcess(original, ProjectProcessProjectOperation.ChangeHierarchy, proposal);
        var targets = Targets(submission.Command);
        var source = ProjectGovernedInvocationSource.Bind(processes.BindProjectSource(original, targets, ProjectProcessProjectOperation.ChangeHierarchy), proposal);
        return InvocationAsync(binding, new ProjectDefinitionProcessReceiptAccess(original, processes, catalog, projects), source, submission, cancellationToken);
    }

    private async Task<ProjectLifecycleInvocation> InvocationAsync(ProjectDefinitionGovernedBinding binding, IProjectDefinitionReceiptAccess read,
        ProjectMutationAuthorization source, ProjectLifecycleSubmission submission, CancellationToken cancellationToken) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("projects:hierarchy-governed-effect:v1"));
        var kind = binding.Effect.Kind switch {
            ProjectDefinitionEffectKind.Agent => ProjectLifecycleEffectKind.Agent,
            ProjectDefinitionEffectKind.Process => ProjectLifecycleEffectKind.Process,
            _ => throw new InvalidOperationException("Hierarchy execution requires an admitted Agent or Process source.")
        };
        hash.AppendData([(byte)kind, checked((byte)binding.Effect.Version)]);
        hash.AppendData(binding.Effect.ToArray());
        return ProjectLifecycleInvocation.ForGoverned(binding.Caller, new ProjectHierarchyReceiptAccess(read), source,
            new(kind, 1, hash.GetHashAndReset()), await currentWrite.CaptureHierarchyAsync(source, submission, cancellationToken));
    }

    internal static ImmutableArray<ProjectWriteAdmission> Targets(ProjectLifecycleCommand command) => command switch {
        ProjectLifecycleCommand.EnsureLink ensure => [ensure.Parent, ensure.Child],
        ProjectLifecycleCommand.ReconnectLink reconnect => [reconnect.Expected.Parent, reconnect.Expected.Child, reconnect.NewParent],
        _ => throw ProjectDefinitionToolProposalCodec.Denied("This tool admits only original hierarchy link or reconnect commands.")
    };
}

internal sealed class ProjectHierarchyReceiptAccess(IProjectDefinitionReceiptAccess access) : IProjectLifecycleReceiptAccess {
    public async Task<bool> CanReadAsync(ProjectLifecycleSubmission original, CancellationToken cancellationToken) {
        foreach (var target in ProjectHierarchyGovernedInvocationFactory.Targets(original.Command)) {
            if (!await access.CanReadAsync(target, cancellationToken)) {
                return false;
            }
        }
        return true;
    }
}
