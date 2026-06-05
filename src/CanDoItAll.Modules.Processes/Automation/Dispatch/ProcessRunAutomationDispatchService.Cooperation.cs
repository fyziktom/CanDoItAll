using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static ProcessRunAssignment? ResolveDispatchCurrentAssignment(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessRunAssignment> runAssignments)
    {
        return ProcessDispatchAssignmentRouteHelper.ResolveCurrentAssignment(
            stepRun,
            stepRoleRequirements,
            runAssignments);
    }

    private static bool HasDispatchExecutableTarget(ProcessRunAssignment assignment)
    {
        return ProcessDispatchAssignmentRouteHelper.HasDispatchExecutableTarget(assignment);
    }

    private static AgentProcessCooperationMetadata ResolveProcessCooperationMetadata(
        ProcessStepRun stepRun,
        ProcessWorkBrief? workBrief,
        ProcessRoleRequirement? role,
        ProcessRunAssignment? assignment,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        IReadOnlyList<DispatchArtifactInput> artifactInputs,
        IReadOnlyList<DispatchBranchOutcome> branchOutcomes,
        AgentEditorModel agent)
    {
        var workspaceToolProfile = ResolveWorkspaceToolProfile(stepRun, workBrief, role, assignment, expectedArtifacts);
        var handoffSettings = AgentHandoffMetadata.Read(agent.ConfigurationJson);
        var a2aSettings = AgentA2AMetadata.Read(agent.ConfigurationJson);
        var hasEnabledA2AEndpoints = a2aSettings.RemoteEndpoints.Any(endpoint => endpoint.Enabled && endpoint.ExposeSkillsAsTools);
        var hasEnabledHandoff = handoffSettings.Enabled && handoffSettings.Routes.Any(route => route.Enabled);
        var hasProcessArtifactHandoff = artifactInputs.Any(input => input.Artifacts.Count > 0);
        var cooperationMode = ResolveCooperationMode(hasEnabledHandoff, hasEnabledA2AEndpoints, hasProcessArtifactHandoff);
        var summary = BuildCooperationSummary(
            cooperationMode,
            workspaceToolProfile,
            hasEnabledHandoff,
            hasEnabledA2AEndpoints,
            hasProcessArtifactHandoff,
            branchOutcomes.Count);

        return new AgentProcessCooperationMetadata(cooperationMode, workspaceToolProfile, summary);
    }

    private static AgentProcessCooperationMode ResolveCooperationMode(
        bool hasEnabledHandoff,
        bool hasEnabledA2AEndpoints,
        bool hasProcessArtifactHandoff)
    {
        if (hasEnabledHandoff && hasEnabledA2AEndpoints)
        {
            return AgentProcessCooperationMode.Hybrid;
        }

        if (hasEnabledHandoff)
        {
            return AgentProcessCooperationMode.MafLocalHandoff;
        }

        if (hasEnabledA2AEndpoints)
        {
            return AgentProcessCooperationMode.A2ARemoteHandoff;
        }

        return hasProcessArtifactHandoff
            ? AgentProcessCooperationMode.ProcessArtifactHandoff
            : AgentProcessCooperationMode.SingleAgent;
    }

    private static AgentWorkspaceToolProfileKind ResolveWorkspaceToolProfile(
        ProcessStepRun stepRun,
        ProcessWorkBrief? workBrief,
        ProcessRoleRequirement? role,
        ProcessRunAssignment? assignment,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        var primaryRoleText = BuildPrimaryRoleProfileText(stepRun, workBrief, role, assignment, expectedArtifacts);
        var roleText = BuildRoleProfileText(stepRun, workBrief, role, assignment, expectedArtifacts);
        if (RequiresRuntimeCleanupWorkspaceToolProfile(stepRun, roleText))
        {
            return AgentWorkspaceToolProfileKind.QualityValidation;
        }

        if (stepRun.StepKind == ProcessStepKind.Work && ContainsDevelopmentProfileSignal(primaryRoleText))
        {
            return AgentWorkspaceToolProfileKind.SoftwareDevelopment;
        }

        if (ContainsAny(primaryRoleText, "security", "threat", "vulnerability", "compliance"))
        {
            return AgentWorkspaceToolProfileKind.SecurityReview;
        }

        if (ContainsAny(primaryRoleText, "architect", "architecture", "design", "adr", "technical decision"))
        {
            return AgentWorkspaceToolProfileKind.ArchitectureReview;
        }

        if (stepRun.StepKind == ProcessStepKind.Review ||
            ContainsAny(primaryRoleText, "qa", "quality", "test", "tests", "validation", "verify", "proof", "review", "approval", "readiness"))
        {
            return AgentWorkspaceToolProfileKind.QualityValidation;
        }

        if (ContainsDevelopmentProfileSignal(roleText))
        {
            return AgentWorkspaceToolProfileKind.SoftwareDevelopment;
        }

        if (ContainsAny(roleText, "product", "business", "scope", "requirements", "stakeholder", "analysis", "owner"))
        {
            return AgentWorkspaceToolProfileKind.BusinessAnalysis;
        }

        return stepRun.StepKind == ProcessStepKind.Work
            ? AgentWorkspaceToolProfileKind.BusinessAnalysis
            : AgentWorkspaceToolProfileKind.ReadOnly;
    }

    private static bool RequiresRuntimeCleanupWorkspaceToolProfile(ProcessStepRun stepRun, string roleText)
    {
        return stepRun.StepKind == ProcessStepKind.End &&
               ContainsAny(
                   roleText,
                   "stop the managed app process",
                   "stop managed app process",
                   "stop app",
                   "stop the app",
                   "shutdown",
                   "managed run",
                   "process tree",
                   "runtime session");
    }

    private static bool ContainsDevelopmentProfileSignal(string text)
    {
        return ContainsAny(
            text,
            "developer",
            "engineer",
            "implementation",
            "implement",
            "build",
            "code",
            "blazor",
            ".net",
            "dotnet",
            "c#");
    }

    private static string BuildCooperationSummary(
        AgentProcessCooperationMode cooperationMode,
        AgentWorkspaceToolProfileKind workspaceToolProfile,
        bool hasEnabledHandoff,
        bool hasEnabledA2AEndpoints,
        bool hasProcessArtifactHandoff,
        int branchOutcomeCount)
    {
        var sources = new List<string>();
        if (hasEnabledHandoff)
        {
            sources.Add("MAF handoff routes are enabled on the selected technical agent");
        }

        if (hasEnabledA2AEndpoints)
        {
            sources.Add("A2A remote endpoint tools are enabled on the selected technical agent");
        }

        if (hasProcessArtifactHandoff)
        {
            sources.Add("upstream process artifacts are available as governed handoff inputs");
        }

        if (branchOutcomeCount > 0)
        {
            sources.Add($"{branchOutcomeCount} branch outcome option(s) are available");
        }

        var basis = sources.Count == 0
            ? "single-agent step execution"
            : string.Join("; ", sources);
        return $"Process dispatch selected cooperation mode '{cooperationMode}' with workspace tool profile '{AgentWorkspaceToolAccessProfiles.GetProfileKey(workspaceToolProfile)}' because {basis}.";
    }

    private static string BuildRoleProfileText(
        ProcessStepRun stepRun,
        ProcessWorkBrief? workBrief,
        ProcessRoleRequirement? role,
        ProcessRunAssignment? assignment,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        return string.Join(
                ' ',
                new[]
                {
                    stepRun.Title,
                    stepRun.RoleSnapshotSummary,
                    stepRun.CurrentExecutorName,
                    workBrief?.Title,
                    workBrief?.WorkBriefText,
                    workBrief?.ExpectedOutcome,
                    workBrief?.EvidenceExpectationSummary,
                    role?.Key,
                    role?.DisplayName,
                    role?.Purpose,
                    role?.StaffingIntent,
                    role?.PreferredExecutorKind,
                    role?.SnapshotSummary,
                    assignment?.ExecutorKind,
                    assignment?.BindingReason,
                    string.Join(' ', expectedArtifacts.Select(item => item.Title)),
                    string.Join(' ', expectedArtifacts.Select(item => item.ValidationRequirementSummary))
                }
                .Where(item => !string.IsNullOrWhiteSpace(item)))
            .ToLowerInvariant();
    }

    private static string BuildPrimaryRoleProfileText(
        ProcessStepRun stepRun,
        ProcessWorkBrief? workBrief,
        ProcessRoleRequirement? role,
        ProcessRunAssignment? assignment,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        return string.Join(
                ' ',
                new[]
                {
                    stepRun.Title,
                    stepRun.RoleSnapshotSummary,
                    stepRun.CurrentExecutorName,
                    workBrief?.Title,
                    workBrief?.ExpectedOutcome,
                    workBrief?.EvidenceExpectationSummary,
                    role?.Key,
                    role?.DisplayName,
                    role?.Purpose,
                    role?.StaffingIntent,
                    role?.PreferredExecutorKind,
                    role?.SnapshotSummary,
                    assignment?.ExecutorKind,
                    assignment?.BindingReason,
                    string.Join(' ', expectedArtifacts.Select(item => item.Title)),
                    string.Join(' ', expectedArtifacts.Select(item => item.ValidationRequirementSummary))
                }
                .Where(item => !string.IsNullOrWhiteSpace(item)))
            .ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }
}
