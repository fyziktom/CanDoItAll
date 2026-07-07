using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    private static string BuildProcessExecutionMetadata(ProcessRuntimeStepAssignment assignment)
    {
        var allowedOperations = NormalizeOperations(assignment.AllowedOperations);
        var targetScope = string.IsNullOrWhiteSpace(assignment.OperationTargetScope)
            ? string.Empty
            : assignment.OperationTargetScope.Trim();
        var allowsProductMutation = AllowsProductMutation(allowedOperations, targetScope);
        var allowsBrowserProof = AllowsBrowserRuntimeProof(allowedOperations);
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey] = allowsBrowserProof,
            [ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey] = allowedOperations,
            [ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey] = targetScope,
            [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = allowsProductMutation
        };
        var trustedAliases = ResolveTrustedExternalTargetAliases(assignment.LaunchVariables);
        if (trustedAliases.Count > 0 && ShouldGroundExternalTargetAliases(allowedOperations, targetScope))
        {
            metadata[allowsProductMutation
                ? ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey
                : ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] = trustedAliases;
        }

        var metadataJson = ApplyLaunchContextMetadata(
            JsonSerializer.Serialize(metadata, AgentOutputJson.SerializerOptions),
            assignment.LaunchVariables);
        metadataJson = ExecutionInvocationMetadata.ApplyRuntimeCapabilityScopeOverride(
            metadataJson,
            AgentFrameworkProcessCapabilityScopeTranslator.Translate(assignment.CapabilityScope));
        return ExecutionInvocationMetadata.Build(
            metadataJson,
            new ExecutionInvocationPolicy(
                FinalizerMode: AgentFinalizerMode.Required,
                MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts));
    }

    private static string ApplyLaunchContextMetadata(
        string metadataJson,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            metadataJson,
            ResolveProjectWorkspaceScope(launchVariables));
        metadataJson = ExecutionInvocationMetadata.ApplyProjectStructureLaunchAgent(
            metadataJson,
            ResolveProjectStructureLaunchAgent(launchVariables));
        return ExecutionInvocationMetadata.ApplyProjectStructureProcessNodeContext(
            metadataJson,
            ResolveProjectStructureProcessNodeContext(launchVariables));
    }

    private static WorkspaceScopeDescriptor? ResolveProjectWorkspaceScope(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return TryResolveLaunchGuid(launchVariables, ProcessLaunchVariableNames.ProjectId, out var projectId)
            ? WorkspaceScopeDescriptor.Project(projectId.ToString("D"))
            : null;
    }

    private static ProjectStructureAgentIdentityDescriptor? ResolveProjectStructureLaunchAgent(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var descriptor = new ProjectStructureAgentIdentityDescriptor(
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.AgentId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.AgentName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.MachineName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.RepositoryRoot),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.BranchName),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.SessionId));
        return descriptor.HasLeaseOwnerIdentity ? descriptor : null;
    }

    private static ProjectStructureProcessNodeContextDescriptor? ResolveProjectStructureProcessNodeContext(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var descriptor = new ProjectStructureProcessNodeContextDescriptor(
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.CurrentProcessRunNodeId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.ProcessRunNodeId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.ParentProcessRunNodeId),
            ResolveLaunchVariable(launchVariables, ProcessLaunchVariableNames.TargetProcessRunNodeId));
        return descriptor.HasAnyProcessRunNode ? descriptor : null;
    }

    private static bool TryResolveLaunchGuid(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        out Guid value)
    {
        value = Guid.Empty;
        return launchVariables.TryGetValue(key, out var rawValue) &&
               Guid.TryParse(rawValue, out value) &&
               value != Guid.Empty;
    }

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
    {
        return launchVariables.TryGetValue(key, out var value)
            ? value.Trim()
            : string.Empty;
    }

    private static IReadOnlyList<string> NormalizeOperations(IReadOnlyList<string> operations)
    {
        return operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(operation => operation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool AllowsProductMutation(
        IReadOnlyList<string> allowedOperations,
        string targetScope)
    {
        return allowedOperations.Contains(ProcessOperationContractNames.MutateProductTarget, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetMutable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ManagedOutputProduct, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsBrowserRuntimeProof(IReadOnlyList<string> allowedOperations)
    {
        return allowedOperations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase);
    }

    private static bool UsesExternalProductTarget(string targetScope)
    {
        return string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetMutable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetReadOnly, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldGroundExternalTargetAliases(
        IReadOnlyList<string> allowedOperations,
        string targetScope)
    {
        return UsesExternalProductTarget(targetScope) ||
            string.Equals(targetScope, ProcessOperationContractNames.ExternalActionControlled, StringComparison.OrdinalIgnoreCase) ||
            allowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveTrustedExternalTargetAliases(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return launchVariables
            .Where(item => TrustedExternalTargetVariableNames.Contains(item.Key))
            .Select(item => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(item.Value))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => item.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static readonly HashSet<string> TrustedExternalTargetVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExternalTargetAlias",
        "ExternalTargetRoot",
        "OutputFolder",
        "OutputRoot",
        "OutputRootAlias",
        "ProductRoot",
        "ProductRootAlias",
        "WorkspaceAlias"
    };

}
