using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class DotNetSolutionContextCompletionGateContributionTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(
        Path.GetTempPath(),
        $"CanDoItAll.DotNetSolutionContextGate.{Guid.NewGuid():N}");

    [Fact]
    public void Validate_accepts_a_valid_declared_solution_context_schema()
    {
        var workspaceFiles = CreateWorkspaceFiles();
        var (context, artifactRef) = CreateContext(DotNetSolutionContextParser.Schema);
        WriteArtifact(workspaceFiles, artifactRef, """
            Status: Completed

            ```json
            {
              "schema": "dotnet.solution-context/v1",
              "provisioningMode": "verify-existing",
              "solution": {
                "file": "WorkLogger.sln",
                "candidateFiles": []
              },
              "requiredProjectFiles": ["src/WorkLogger/WorkLogger.csproj"],
              "testProjectFiles": []
            }
            ```
            """);

        var contribution = new DotNetSolutionContextCompletionGateContribution(workspaceFiles);

        var issue = contribution.Validate(context);

        Assert.Null(issue);
        Assert.Equal(ProcessCompletionGateContributionStage.BeforeToolReceiptEvidence, contribution.Stage);
    }

    [Fact]
    public void Validate_returns_bounded_retry_issue_when_initialize_context_omits_its_initialization_plan()
    {
        var workspaceFiles = CreateWorkspaceFiles();
        var (context, artifactRef) = CreateContext(DotNetSolutionContextParser.Schema);
        WriteArtifact(workspaceFiles, artifactRef, """
            Status: Completed

            ```json
            {
              "schema": "dotnet.solution-context/v1",
              "provisioningMode": "initialize",
              "solution": {
                "file": "WorkLogger.sln",
                "candidateFiles": []
              },
              "requiredProjectFiles": ["src/WorkLogger/WorkLogger.csproj"],
              "testProjectFiles": []
            }
            ```
            """);

        var issue = new DotNetSolutionContextCompletionGateContribution(workspaceFiles).Validate(context);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.artifact_payload_schema_invalid", issue.Code);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, issue.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, issue.Idempotency);
        Assert.Contains("requires object 'initialization'", issue.Summary, StringComparison.Ordinal);
        Assert.Single(issue.RequestedArtifactSlotIds);
    }

    [Fact]
    public void Validate_ignores_artifacts_without_the_declared_dotnet_schema()
    {
        var workspaceFiles = CreateWorkspaceFiles();
        var (context, _) = CreateContext("application/json");

        var issue = new DotNetSolutionContextCompletionGateContribution(workspaceFiles).Validate(context);

        Assert.Null(issue);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private IWorkspaceFileService CreateWorkspaceFiles()
    {
        Directory.CreateDirectory(workspaceRoot);
        return TestWorkspaceServices.CreateFileService(workspaceRoot);
    }

    private static (ProcessCompletionGateContext Context, string ArtifactRef) CreateContext(string payloadSchema)
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var slotId = ArtifactSlotId.New();
        var artifactRef = $"artifacts/process-runs/{runId.Value:D}/steps/slice-architecture-check.md";
        var assignment = new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            stepId,
            "slice-architecture-check",
            "solution-architect",
            "solution-architect",
            "Solution architect",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Solution architect",
            "Produce the architecture decision.",
            "sha256:readiness",
            "Unit test assignment.",
            [slotId],
            [],
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.Ordinal),
            BranchGate: null,
            DateTimeOffset.UtcNow);
        var descriptor = new ProcessArtifactSlotDescriptor(
            slotId,
            "slice-architecture-check:dotnet-solution-context",
            assignment.StepKey,
            "dotnet-solution-context",
            ".NET solution context",
            "Decision",
            artifactRef,
            ProcessArtifactMaterializationMode.AgentWritten)
        {
            PayloadSchema = payloadSchema
        };
        var stepContract = new ProcessStepExecutionContract(
            [],
            [new ExpectedProducedArtifactRef(slotId)],
            [],
            "sha256:dotnet-solution-context")
        {
            ArtifactDescriptors = [descriptor]
        };
        return (
            new ProcessCompletionGateContext(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Architecture decision completed."
                },
                [],
                Guid.NewGuid())
            {
                StepContract = stepContract
            },
            artifactRef);
    }

    private static void WriteArtifact(
        IWorkspaceFileService workspaceFiles,
        string artifactRef,
        string content)
    {
        var result = workspaceFiles.WriteTextFile(artifactRef, content, overwrite: true);
        Assert.True(result.Succeeded, result.Message);
    }
}
