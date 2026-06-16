using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeIntegrationMetadataTests
{
    [Fact]
    public void Process_execution_metadata_maps_project_launch_context_to_trusted_scope()
    {
        var projectId = Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9");
        var assignment = CreateAssignment(projectId);

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        var scope = ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run);
        var launchAgent = ExecutionInvocationMetadata.ResolveProjectStructureLaunchAgent(run);
        var allowedOperations = ExecutionInvocationMetadata.ResolveProcessStepAllowedOperations(run);
        var writableAliases = ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(run);

        Assert.NotNull(scope);
        Assert.Equal(WorkspaceScopeKind.Project, scope!.Kind);
        Assert.Equal(projectId.ToString("D"), scope.Key);
        Assert.NotNull(launchAgent);
        Assert.Equal("codex-process-e2e", launchAgent!.AgentId);
        Assert.Equal("Codex Process E2E", launchAgent.AgentName);
        Assert.Equal("LUCYSPOWER", launchAgent.MachineName);
        Assert.Equal(@"C:\programovani\dotnet\output", launchAgent.RepositoryRoot);
        Assert.Equal("main", launchAgent.BranchName);
        Assert.Equal("codex-process-e2e-session", launchAgent.SessionId);
        Assert.Contains(ProcessOperationContractNames.ReadProjectStructure, allowedOperations);
        Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, allowedOperations);
        Assert.Contains("external-target/C/programovani/dotnet/output", writableAliases);
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(Guid projectId)
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "resolve-blazor-contract",
            "blazor-engineer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Blazor engineer",
            "Resolve the project contract.",
            "sha256:readiness",
            "Resolved from live profile.",
            [ArtifactSlotId.New()],
            [],
            [
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.MutateProductTarget
            ],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProjectId"] = projectId.ToString("D"),
                ["AgentId"] = "codex-process-e2e",
                ["AgentName"] = "Codex Process E2E",
                ["MachineName"] = "LUCYSPOWER",
                ["RepositoryRoot"] = @"C:\programovani\dotnet\output",
                ["BranchName"] = "main",
                ["SessionId"] = "codex-process-e2e-session"
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static string BuildProcessExecutionMetadata(ProcessRuntimeStepAssignment assignment)
    {
        var adapterType = typeof(ProcessesModuleServiceCollectionExtensions)
            .Assembly
            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessExecutionAdapter")
            ?? throw new InvalidOperationException("Process execution adapter type was not found.");
        var method = adapterType.GetMethod(
            "BuildProcessExecutionMetadata",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Process execution metadata builder was not found.");

        return Assert.IsType<string>(method.Invoke(null, [assignment]));
    }

    private static ExecutionRunRecord CreateTrustedProcessRun(string metadataJson)
    {
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Process step",
            SourceKind: "process-step",
            SourceId: "resolve-blazor-contract",
            CorrelationId: "run-001",
            CausationId: "step-001",
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: "Input",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: "run-001",
            ProcessStepId: "step-001");
    }
}
