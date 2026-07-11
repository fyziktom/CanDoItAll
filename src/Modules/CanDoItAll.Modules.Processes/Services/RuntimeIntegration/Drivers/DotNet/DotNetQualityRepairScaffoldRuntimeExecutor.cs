using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessRuntimeOwnedToolReceiptFactory;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetQualityRepairScaffoldRuntimeExecutor(
    IWorkspaceFileService workspaceFiles,
    IWorkspaceCommandExecutionService workspaceCommands,
    DotNetScaffoldResidueInspector residueInspector) : IProcessRuntimeOwnedStepExecutor
{
    internal const string ScaffoldResidueDiagnosisMarker = "Repair driver: dotnet-blazor-scaffold-residue";
    private const string DefinitionKey = "dotnet-quality-repair";
    private const string AppliedBranchOutcomeKey = "product-repair-applied";
    private const int MaximumDiagnosisCharacters = 100000;
    private static readonly HashSet<string> SupportedStepKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "implement-quality-repair",
        "implement-bughunt-repair"
    };

    public async ValueTask<ProcessRuntimeOwnedStepExecutionResult?> TryExecuteAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (!IsSupportedAssignment(assignment) ||
            !IsScaffoldResidueRepairSelected(assignment) ||
            !DotNetQualityRepairScaffoldInputs.TryResolve(assignment, out var inputs))
        {
            return null;
        }

        var executionRunId = Guid.NewGuid();
        var receipts = new List<ToolExecutionReceiptRecord>();
        var before = residueInspector.Read(inputs, executionRunId, receipts);
        if (!before.HasStockScaffoldResidue)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var scriptWrite = workspaceFiles.WriteTextFile(inputs.ScriptRef, inputs.Script, overwrite: true);
        receipts.Add(From(executionRunId, scriptWrite));
        if (!scriptWrite.Succeeded)
        {
            return Failure(assignment, executionRunId, receipts, $"Could not write deterministic scaffold repair helper '{inputs.ScriptRef}': {scriptWrite.Message}");
        }

        var scriptStat = workspaceFiles.StatPath(inputs.ScriptRef);
        receipts.Add(From(executionRunId, scriptStat));
        if (!scriptStat.Succeeded || !scriptStat.Exists || !string.Equals(scriptStat.PathKind, "file", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(assignment, executionRunId, receipts, $"Could not verify deterministic scaffold repair helper '{inputs.ScriptRef}': {scriptStat.Message}");
        }

        var scriptRun = await workspaceCommands.PowerShellRunScript(
                inputs.ScriptRef,
                arguments: null,
                outputPaths: [BuildToolOutputRef(assignment)],
                workingDirectory: inputs.ProductRootAlias,
                timeoutSeconds: 300,
                sideEffectManifest: inputs.SideEffectManifest)
            .ConfigureAwait(false);
        receipts.Add(From(executionRunId, scriptRun));
        if (!scriptRun.Succeeded)
        {
            return Failure(assignment, executionRunId, receipts, $"Deterministic scaffold repair helper failed: {scriptRun.Message}");
        }

        var after = residueInspector.Read(inputs, executionRunId, receipts);
        if (after.HasStockScaffoldResidue)
        {
            return Failure(
                assignment,
                executionRunId,
                receipts,
                $"Deterministic scaffold repair readback still found stock template residue: {string.Join("; ", after.DescribeResidue())}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var restore = await workspaceCommands.DotnetRestore(
                inputs.SolutionFileAlias,
                inputs.ProductRootAlias,
                timeoutSeconds: 600)
            .ConfigureAwait(false);
        receipts.Add(From(executionRunId, restore));
        if (!restore.Succeeded)
        {
            return Failure(assignment, executionRunId, receipts, $"Restore failed after deterministic scaffold repair: {restore.Message}");
        }

        var build = await workspaceCommands.DotnetBuild(
                inputs.SolutionFileAlias,
                configuration: "Debug",
                noRestore: true,
                workingDirectory: inputs.ProductRootAlias,
                timeoutSeconds: 600)
            .ConfigureAwait(false);
        receipts.Add(From(executionRunId, build));
        if (!build.Succeeded)
        {
            return Failure(assignment, executionRunId, receipts, $"Build failed after deterministic scaffold repair: {build.Message}");
        }

        var test = await workspaceCommands.DotnetTest(
                inputs.TestProjectFileAlias,
                configuration: "Debug",
                filter: null,
                noBuild: true,
                noRestore: true,
                workingDirectory: inputs.ProductRootAlias,
                timeoutSeconds: 300)
            .ConfigureAwait(false);
        receipts.Add(From(executionRunId, test));
        if (!test.Succeeded)
        {
            return Failure(assignment, executionRunId, receipts, $"Tests failed after deterministic scaffold repair: {test.Message}");
        }

        var primaryArtifactRef = ProcessManagedArtifactEvidence.BuildManagedStepArtifactPath(assignment);
        var artifact = workspaceFiles.WriteTextFile(
            primaryArtifactRef,
            BuildArtifact(assignment, before, inputs),
            overwrite: true);
        receipts.Add(From(executionRunId, artifact));
        if (!artifact.Succeeded)
        {
            return Failure(assignment, executionRunId, receipts, $"Could not write deterministic scaffold repair evidence: {artifact.Message}");
        }

        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The isolated .NET scaffold repair driver removed only fingerprint-matched stock Blazor template residue and completed current restore, build, test, and source readback proof.",
            BranchOutcomeKey = AppliedBranchOutcomeKey,
            EvidenceRefs = [primaryArtifactRef],
            NextActions = ["Independently reproduce the original quality proof against the repaired product."],
            HumanReadableSummaryMarkdown = $"Deterministic .NET scaffold repair completed for `{assignment.StepKey}`. The driver removed only fingerprint-matched stock template residue and validated the repaired solution."
        };
        return new ProcessRuntimeOwnedStepExecutionResult(
            true,
            output,
            receipts,
            executionRunId,
            "Deterministic .NET scaffold repair completed.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:dotnet-quality-scaffold-repair:succeeded:{string.Join("|", receipts.Select(receipt => $"{receipt.ToolName}:{receipt.ExitSummary}"))}");
    }

    private static bool IsSupportedAssignment(ProcessRuntimeStepAssignment assignment)
        => SupportedStepKeys.Contains(assignment.StepKey) &&
           assignment.LaunchVariables.TryGetValue(ProcessRuntimeLaunchVariables.ProcessDefinitionKey, out var definitionKey) &&
           string.Equals(definitionKey, DefinitionKey, StringComparison.OrdinalIgnoreCase);

    private bool IsScaffoldResidueRepairSelected(ProcessRuntimeStepAssignment assignment)
    {
        var diagnosisStepKey = assignment.StepKey switch
        {
            "implement-quality-repair" => "diagnose-quality-failure",
            "implement-bughunt-repair" => "diagnose-persistent-failure",
            _ => string.Empty
        };
        if (diagnosisStepKey.Length == 0)
        {
            return false;
        }

        var diagnosisRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/steps/{diagnosisStepKey}.md";
        var diagnosis = workspaceFiles.ReadTextFile(diagnosisRef, MaximumDiagnosisCharacters);
        return diagnosis.Succeeded &&
               !diagnosis.IsTruncated &&
               diagnosis.Content.Contains(ScaffoldResidueDiagnosisMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildArtifact(
        ProcessRuntimeStepAssignment assignment,
        DotNetScaffoldState before,
        DotNetQualityRepairScaffoldInputs inputs)
        => $"""
        Status: Completed
        Branch outcome key: {AppliedBranchOutcomeKey}

        # Deterministic .NET scaffold repair

        The isolated .NET quality-repair driver found fingerprint-matched stock Blazor template residue and executed the bounded scaffold repair helper before finalizing this step.

        Removed residue:
        {string.Join(Environment.NewLine, before.DescribeResidue().Select(item => $"- {item}"))}

        Product boundary: {inputs.ProductRootAlias}
        Validation: current restore, build, and test commands succeeded after product readback confirmed the stock residue was removed.
        Step: {assignment.StepKey}
        """;

    private static ProcessRuntimeOwnedStepExecutionResult Failure(
        ProcessRuntimeStepAssignment assignment,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        string summary)
        => new(
            false,
            null,
            receipts,
            executionRunId,
            summary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:dotnet-quality-scaffold-repair:failed:{summary}");

    private static string BuildToolOutputRef(ProcessRuntimeStepAssignment assignment)
        => $"artifacts/process-runs/{assignment.RunId.Value:D}/tool-runs/{assignment.StepKey}.runtime-owned-scaffold-repair.json";

}
