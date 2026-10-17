using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class FileSandboxWorkspacePreparedValues {
    internal static SandboxWorkspaceCatalog Freeze(SandboxWorkspaceCatalog value) {
        ArgumentNullException.ThrowIfNull(value);
        return value with {
            Agents = Copy(value.Agents, Freeze),
            Providers = Copy(value.Providers, Freeze),
            Capabilities = Copy(value.Capabilities, capability => capability with { Tags = Copy(capability.Tags) }),
            Memory = Copy(value.Memory),
            AgentTeams = Copy(value.AgentTeams, team => team with { AgentIds = Copy(team.AgentIds) }),
            AgentPackageImportOperations = Copy(value.AgentPackageImportOperations, operation => operation with {
                Receipt = operation.Receipt is null ? null! : operation.Receipt with {
                    UnresolvedPrerequisites = Copy(operation.Receipt.UnresolvedPrerequisites),
                    Warnings = Copy(operation.Receipt.Warnings)
                }
            }),
            AgentExternalBindings = Copy(value.AgentExternalBindings),
            AgentExternalProvisioningOperations = Copy(value.AgentExternalProvisioningOperations, operation => operation with {
                Receipt = operation.Receipt is null ? null! : operation.Receipt with { Warnings = Copy(operation.Receipt.Warnings) }
            })
        };
    }

    internal static SandboxWorkspaceDocument Freeze(SandboxWorkspaceDocument value) {
        ArgumentNullException.ThrowIfNull(value);
        var catalog = Freeze(value.ToCatalog());
        var execution = Freeze(value.ToExecutionState());
        return value with {
            Agents = catalog.Agents,
            Providers = catalog.Providers,
            Capabilities = catalog.Capabilities,
            Memory = catalog.Memory,
            AgentTeams = catalog.AgentTeams,
            AgentPackageImportOperations = catalog.AgentPackageImportOperations,
            AgentExternalBindings = catalog.AgentExternalBindings,
            AgentExternalProvisioningOperations = catalog.AgentExternalProvisioningOperations,
            ChatSessions = execution.ChatSessions,
            ExecutionLog = execution.ExecutionLog,
            Metrics = execution.Metrics,
            ExecutionRuns = execution.ExecutionRuns,
            ExecutionApprovals = execution.ExecutionApprovals,
            ExecutionArtifacts = execution.ExecutionArtifacts,
            ExecutionWorkflowCheckpoints = execution.ExecutionWorkflowCheckpoints,
            ToolExecutionReceipts = execution.ToolExecutionReceipts,
            ProviderUsageObservations = execution.ProviderUsageObservations
        };
    }

    internal static SandboxWorkspaceExecutionState Freeze(SandboxWorkspaceExecutionState value) {
        ArgumentNullException.ThrowIfNull(value);
        return value with {
            ChatSessions = Copy(value.ChatSessions, Freeze),
            ExecutionLog = Copy(value.ExecutionLog),
            Metrics = Copy(value.Metrics),
            ExecutionRuns = Copy(value.ExecutionRuns, Freeze),
            ExecutionApprovals = Copy(value.ExecutionApprovals),
            ExecutionArtifacts = Copy(value.ExecutionArtifacts),
            ExecutionWorkflowCheckpoints = Copy(value.ExecutionWorkflowCheckpoints, Freeze),
            ToolExecutionReceipts = Copy(value.ToolExecutionReceipts),
            ProviderUsageObservations = Copy(value.ProviderUsageObservations)
        };
    }

    internal static ExecutionRunDetail Freeze(ExecutionRunDetail value) {
        ArgumentNullException.ThrowIfNull(value);
        return value with {
            Run = value.Run is null ? null! : Freeze(value.Run),
            ChatSession = value.ChatSession is null ? null : Freeze(value.ChatSession),
            ExecutionLog = Copy(value.ExecutionLog),
            Metrics = Copy(value.Metrics),
            Approvals = Copy(value.Approvals),
            Artifacts = Copy(value.Artifacts),
            Checkpoints = Copy(value.Checkpoints, Freeze),
            ToolReceipts = Copy(value.ToolReceipts),
            UsageObservations = Copy(value.UsageObservations)
        };
    }

    internal static ExecutionRunRecord Freeze(ExecutionRunRecord value) {
        ArgumentNullException.ThrowIfNull(value);
        return value with { PendingApprovals = Copy(value.PendingApprovals) };
    }

    internal static ChatSessionRecord Freeze(ChatSessionRecord value) {
        ArgumentNullException.ThrowIfNull(value);
        return value with {
            Messages = Copy(value.Messages),
            Compatibility = value.Compatibility is null ? null : value.Compatibility with {
                PendingApprovals = Copy(value.Compatibility.PendingApprovals)
            }
        };
    }

    private static AgentDefinition Freeze(AgentDefinition value) => value with {
        Permissions = value.Permissions is null ? null! : value.Permissions with {
            AllowedSecrets = value.Permissions.AllowedSecrets is null ? null : Copy(value.Permissions.AllowedSecrets)
        },
        Capabilities = Copy(value.Capabilities),
        Tags = Copy(value.Tags)
    };

    private static ProviderProfile Freeze(ProviderProfile value) => value with {
        SuggestedModels = Copy(value.SuggestedModels),
        ModelCatalog = Copy(value.ModelCatalog),
        ModelPrices = Copy(value.ModelPrices),
        Tags = Copy(value.Tags),
        ModelThinkingEffortCapabilities = Copy(value.ModelThinkingEffortCapabilities,
            capability => capability with { AllowedEfforts = Copy(capability.AllowedEfforts) })
    };

    private static ExecutionWorkflowCheckpointRecord Freeze(ExecutionWorkflowCheckpointRecord value) => value with {
        PendingApprovalIds = Copy(value.PendingApprovalIds)
    };

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values) =>
        values is null ? null! : values.ToImmutableArray();

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values, Func<T, T> freeze) where T : class =>
        values is null ? null! : values.Select(value => value is null ? null! : freeze(value)).ToImmutableArray();
}
