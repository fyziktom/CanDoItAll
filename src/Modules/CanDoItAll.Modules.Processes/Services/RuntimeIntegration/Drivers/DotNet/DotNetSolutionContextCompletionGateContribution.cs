using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;
using static CanDoItAll.Modules.Processes.ProcessRuntimeFailureClassifier;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSolutionContextCompletionGateContribution(
    IWorkspaceFileService workspaceFiles) : IProcessCompletionGateContribution
{
    private const string PayloadReadFailedDiagnosticCode = "process.adapter.artifact_payload_read_failed";

    private readonly DotNetSolutionContextParser parser = new();

    public string ContributionKey => "dotnet.solution-context-payload";

    public int Order => 25;

    public ProcessCompletionGateContributionStage Stage => ProcessCompletionGateContributionStage.BeforeToolReceiptEvidence;

    public ProcessCompletionIssue? Validate(ProcessCompletionGateContext context)
    {
        if (context.Output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return null;
        }

        foreach (var descriptor in ResolveSchemaBoundOutputs(context))
        {
            var readResult = workspaceFiles.ReadTextFile(descriptor.PrimaryManagedRef, maxCharacters: 100000);
            if (!readResult.Succeeded)
            {
                return CreateReadFailureIssue(context.Assignment, descriptor, readResult.Message);
            }

            if (!parser.TryParse(readResult.Content, out _, out var parseIssue))
            {
                return CreateSchemaFailureIssue(context.Assignment, descriptor, parseIssue);
            }
        }

        return null;
    }

    private static IReadOnlyList<ProcessArtifactSlotDescriptor> ResolveSchemaBoundOutputs(
        ProcessCompletionGateContext context)
    {
        var producedSlotIds = context.Assignment.ProducedArtifactSlotIds.ToHashSet();
        return context.StepContract.ArtifactDescriptors
            .Where(descriptor =>
                producedSlotIds.Contains(descriptor.SlotId) &&
                string.Equals(descriptor.StepKey, context.Assignment.StepKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(descriptor.PayloadSchema, DotNetSolutionContextParser.Schema, StringComparison.Ordinal))
            .OrderBy(descriptor => descriptor.ArtifactExpectationKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ProcessCompletionIssue CreateReadFailureIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessArtifactSlotDescriptor descriptor,
        string detail)
    {
        var safeDetail = LimitDiagnosticText(detail);
        return new ProcessCompletionIssue(
            PayloadReadFailedDiagnosticCode,
            $"Step '{assignment.StepKey}' declared schema-bound artifact '{descriptor.ArtifactTitle}' ({descriptor.PayloadSchema}), but its managed artifact '{descriptor.PrimaryManagedRef}' could not be read for validation: {safeDetail}",
            $"{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:artifact-payload-read-failed:{descriptor.SlotId.Value:D}:{descriptor.PayloadSchema}:{descriptor.PrimaryManagedRef}:{safeDetail}",
            [descriptor.SlotId],
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    private static ProcessCompletionIssue CreateSchemaFailureIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessArtifactSlotDescriptor descriptor,
        string detail)
    {
        var safeDetail = LimitDiagnosticText(detail);
        return new ProcessCompletionIssue(
            ProcessCompletionDiagnosticCodes.ArtifactPayloadSchemaInvalid,
            $"Step '{assignment.StepKey}' declared schema-bound artifact '{descriptor.ArtifactTitle}' ({descriptor.PayloadSchema}), but its managed artifact does not satisfy that schema: {safeDetail} Revise the artifact in the current step before completing or launching dependent work.",
            $"{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:artifact-payload-schema-invalid:{descriptor.SlotId.Value:D}:{descriptor.PayloadSchema}:{descriptor.PrimaryManagedRef}:{safeDetail}",
            [descriptor.SlotId],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }
}
