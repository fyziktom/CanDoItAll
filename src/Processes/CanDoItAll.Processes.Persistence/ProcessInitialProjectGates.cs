using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessInitialProjectGates {
    internal static IReadOnlyList<ProcessProjectAdmission> Capture(ProcessRuntimeCommitRequest request, ProcessPreparedLaunch? preparation = null) {
        if (request.InitialPlan is not { } plan) {
            return [];
        }
        Dictionary<Guid, ProcessProjectAdmission> projects = [];
        Add(request.Mutation.State.ProjectAdmission);
        if (plan.ExecutableDefinitions is { ProjectId: var projectId } definitions && projectId != Guid.Empty) {
            Add(new(definitions.DatabaseProfileId, projectId, definitions.ProjectLifetimeId));
        }
        AddAuthority(preparation?.Authority);
        AddAuthority(preparation?.ToolSource?.Execution.SourceAuthority);
        if (preparation?.LinkTarget is { } target && preparation.Authority?.ProjectAdmission?.ProjectId != target.ProjectId) {
            throw new InvalidOperationException("The prepared native target has no matching original Project admission.");
        }
        return projects.Values.ToArray();

        void AddAuthority(ProcessLaunchAuthority? authority) {
            Add(authority?.ProjectAdmission);
            if (authority?.Principal is ProcessLaunchPrincipal.AgentExecution { Ceiling.SourceProjectAdmission: { } source }) {
                Add(source);
            }
        }

        void Add(ProcessProjectAdmission? admission) {
            if (admission is null) {
                return;
            }
            if (projects.TryGetValue(admission.ProjectId, out var existing) && existing != admission) {
                throw new InvalidOperationException("An initial Process commit cannot lock conflicting admissions for the same Project.");
            }
            projects[admission.ProjectId] = admission;
        }
    }
}
