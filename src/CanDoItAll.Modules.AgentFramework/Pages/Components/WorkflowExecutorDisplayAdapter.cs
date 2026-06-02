using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record WorkflowExecutorDisplayBadge(string Text, string Tone);

public static class WorkflowExecutorDisplayAdapter
{
    public static WorkflowExecutorDisplayBadge BuildAvailabilityBadge(WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new WorkflowExecutorDisplayBadge(
            ResolveAvailabilityLabel(descriptor.Availability.Kind),
            ResolveAvailabilityTone(descriptor.Availability.Kind));
    }

    public static WorkflowExecutorDisplayBadge BuildSideEffectBadge(WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new WorkflowExecutorDisplayBadge(
            ResolveSideEffectLabel(descriptor.SideEffects),
            ResolveSideEffectTone(descriptor.SideEffects));
    }

    public static WorkflowExecutorDisplayBadge? BuildRetrySafetyBadge(
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(policy);

        if (!descriptor.SideEffects.WritesExternalState || policy.MaxRetryAttempts == 0)
        {
            return null;
        }

        return WorkflowExecutorSideEffectPolicy.IsRetryPolicySafe(descriptor, policy)
            ? new WorkflowExecutorDisplayBadge("Retry safe", "success")
            : new WorkflowExecutorDisplayBadge("Unsafe retries", "danger");
    }

    public static string BuildAvailabilityDescription(WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return string.IsNullOrWhiteSpace(descriptor.Availability.Message)
            ? descriptor.CanExecute ? "Executor is runnable in this host." : "Executor is not runnable in this host."
            : descriptor.Availability.Message;
    }

    public static string BuildSideEffectDescription(WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var sideEffects = descriptor.SideEffects;
        var parts = new List<string>
        {
            ResolveSideEffectLabel(sideEffects)
        };
        if (sideEffects.ExternalMutationKind != WorkflowExecutorExternalMutationKind.None)
        {
            parts.Add($"Mutation: {ResolveExternalMutationLabel(sideEffects.ExternalMutationKind)}");
        }

        if (sideEffects.SupportsPreview)
        {
            parts.Add("preview");
        }

        if (sideEffects.SupportsDryRun)
        {
            parts.Add("dry run");
        }

        if (sideEffects.SupportsCommit)
        {
            parts.Add("commit");
        }

        if (sideEffects.RequiresCommitIdempotencyKey)
        {
            parts.Add(string.IsNullOrWhiteSpace(sideEffects.IdempotencyKeyJsonPath)
                ? "idempotency key required"
                : $"idempotency key {sideEffects.IdempotencyKeyJsonPath}");
        }

        if (sideEffects.AllowsIdempotentRetry)
        {
            parts.Add("idempotent retry");
        }

        return string.Join(" / ", parts);
    }

    public static string BuildRetrySafetyDescription(
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(policy);

        if (WorkflowExecutorSideEffectPolicy.IsRetryPolicySafe(descriptor, policy))
        {
            return policy.MaxRetryAttempts == 0
                ? "Retries are disabled for this executor policy."
                : "Retry policy is allowed by the executor side-effect contract.";
        }

        return "Retries are unsafe because this executor writes external state without an idempotent retry-safe side-effect contract.";
    }

    public static IReadOnlyList<string> BuildSummaryBadges(WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var badges = new List<string>
        {
            BuildAvailabilityBadge(descriptor).Text,
            BuildSideEffectBadge(descriptor).Text
        };
        if (descriptor.PermissionPolicy.RequiresApproval)
        {
            badges.Add("Approval required");
        }

        if (descriptor.DeterministicTestMode.IsSupported)
        {
            badges.Add("Deterministic preview");
        }

        if (BuildRetrySafetyBadge(descriptor, descriptor.DefaultPolicy) is { } retryBadge)
        {
            badges.Add(retryBadge.Text);
        }

        return badges;
    }

    private static string ResolveAvailabilityLabel(WorkflowExecutorAvailabilityKind kind)
    {
        return kind switch
        {
            WorkflowExecutorAvailabilityKind.Available => "Available",
            WorkflowExecutorAvailabilityKind.Planned => "Planned",
            WorkflowExecutorAvailabilityKind.Disabled => "Disabled",
            WorkflowExecutorAvailabilityKind.Unavailable => "Unavailable",
            WorkflowExecutorAvailabilityKind.Incompatible => "Incompatible",
            _ => kind.ToString()
        };
    }

    private static string ResolveAvailabilityTone(WorkflowExecutorAvailabilityKind kind)
    {
        return kind switch
        {
            WorkflowExecutorAvailabilityKind.Available => "success",
            WorkflowExecutorAvailabilityKind.Planned => "neutral",
            WorkflowExecutorAvailabilityKind.Disabled => "warning",
            WorkflowExecutorAvailabilityKind.Unavailable => "danger",
            WorkflowExecutorAvailabilityKind.Incompatible => "danger",
            _ => "neutral"
        };
    }

    private static string ResolveSideEffectLabel(WorkflowExecutorSideEffectDescriptor sideEffects)
    {
        return sideEffects.Kind switch
        {
            WorkflowExecutorSideEffectKind.None => "No side effects",
            WorkflowExecutorSideEffectKind.WorkspaceRead => "Workspace read",
            WorkflowExecutorSideEffectKind.WorkspaceWrite => "Workspace write",
            WorkflowExecutorSideEffectKind.ExternalRead => "External read",
            WorkflowExecutorSideEffectKind.ExternalWrite => sideEffects.ExternalMutationKind == WorkflowExecutorExternalMutationKind.ProcessedMarker
                ? "Processed marker write"
                : "External write",
            _ => sideEffects.Kind.ToString()
        };
    }

    private static string ResolveSideEffectTone(WorkflowExecutorSideEffectDescriptor sideEffects)
    {
        return sideEffects.Kind switch
        {
            WorkflowExecutorSideEffectKind.None => "neutral",
            WorkflowExecutorSideEffectKind.WorkspaceRead => "info",
            WorkflowExecutorSideEffectKind.WorkspaceWrite => "warning",
            WorkflowExecutorSideEffectKind.ExternalRead => "info",
            WorkflowExecutorSideEffectKind.ExternalWrite => sideEffects.AllowsIdempotentRetry ? "warning" : "danger",
            _ => "neutral"
        };
    }

    private static string ResolveExternalMutationLabel(WorkflowExecutorExternalMutationKind kind)
    {
        return kind switch
        {
            WorkflowExecutorExternalMutationKind.None => "none",
            WorkflowExecutorExternalMutationKind.ProcessedMarker => "processed marker",
            _ => kind.ToString()
        };
    }
}
