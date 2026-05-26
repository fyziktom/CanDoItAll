using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal static class ProcessInvocationMetadataBuilder
    {
        internal static string Build(
            DispatchCandidate candidate,
            ExecutionInvocationPolicy processInvocationPolicy,
            string? projectStructureGroundingSummary,
            string? artifactInspectionGroundingSummary)
        {
            var targetGroundings = ProcessTargetGroundingLedgerBuilder.ResolveExternalTargetGroundings(
                candidate,
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary);
            var operationContract = ProcessStepOperationContractResolver.Resolve(candidate);
            var executionBoundary = ProcessStepOperationContractResolver.ResolveExecutionBoundary(candidate, operationContract);
            var allowExternalTargetMutation = AllowsExternalTargetMutation(
                candidate,
                executionBoundary,
                operationContract,
                projectStructureGroundingSummary);
            var allowedExternalTargetAliases = allowExternalTargetMutation
                ? ProcessTargetGroundingLedgerBuilder.ResolveMutableExternalTargetAliases(candidate, targetGroundings)
                : [];
            var browserProofGroundingText = string.Join(
                ' ',
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary);
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey] = RequiresConcreteBrowserProof(candidate, browserProofGroundingText),
                [ExecutionInvocationMetadata.ProcessStepExecutionBoundaryMetadataKey] = executionBoundary.Boundary.ToString(),
                [ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey] = operationContract.AllowedOperations.Select(item => item.ToString()).ToArray(),
                [ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey] = operationContract.TargetScope.ToString(),
                [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = operationContract.AllowsProductMutation,
                [ExecutionInvocationMetadata.ProcessGroundedTargetAliasLedgerMetadataKey] = ProcessTargetGroundingLedgerBuilder.BuildGroundedTargetAliasLedger(
                    targetGroundings,
                    allowedExternalTargetAliases)
            };
            if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
            {
                metadata[ExecutionInvocationMetadata.ProcessScaffoldToolOnlyMetadataKey] = true;
            }

            if (allowedExternalTargetAliases.Count > 0)
            {
                metadata[ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey] = allowedExternalTargetAliases;
            }

            var readOnlyExternalTargetAliases = ProcessTargetGroundingLedgerBuilder.ResolveReadOnlyExternalTargetAliases(
                candidate,
                targetGroundings,
                allowedExternalTargetAliases,
                allowExternalTargetMutation);
            if (readOnlyExternalTargetAliases.Count > 0)
            {
                metadata[ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] = readOnlyExternalTargetAliases;
            }

            var baseMetadataJson = metadata.Count == 0
                ? null
                : JsonSerializer.Serialize(metadata, AgentOutputJson.SerializerOptions);
            baseMetadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
                baseMetadataJson,
                ResolveContextWorkspaceScope(candidate));
            var cooperationMetadataJson = ExecutionInvocationMetadata.ApplyProcessCooperation(
                baseMetadataJson,
                ResolveBoundaryAwareCooperationMetadata(candidate.CooperationMetadata, executionBoundary));
            return ExecutionInvocationMetadata.Build(cooperationMetadataJson, processInvocationPolicy);
        }
    }
}
