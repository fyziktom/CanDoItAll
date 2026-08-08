using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Deterministically classifies the relationship between a conversation's
/// last accepted context binding and a newly captured observation. The
/// classification uses typed source, view, and selection identities only; it
/// never inspects free-form model or user text and never grants authority.
/// </summary>
public static class AgentContextTransitionClassifier
{
    public static AgentContextTransition Classify(
        AgentConversationContextBinding binding,
        AgentChatContextSnapshot observation)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(observation);

        var currentSource = observation.Scope.Source;
        var currentView = observation.Scope.SurfacePosition?.View ?? string.Empty;
        var currentSelectionId = observation.Scope.SurfacePosition?.PrimarySelection?.Id;

        if (binding.Mode == AgentConversationContextMode.Detached)
        {
            return new AgentContextTransition(
                AgentContextTransitionKind.ContextDetached,
                AgentContextTransitionDecision.Detached,
                AgentContextEpochBehavior.NewEpoch,
                summary: "The conversation is detached from the application surface.");
        }

        if (!binding.IsFollowing)
        {
            // First adoption for this conversation: no transition to explain.
            return new AgentContextTransition(
                AgentContextTransitionKind.None,
                AgentContextTransitionDecision.AutoAdopted,
                AgentContextEpochBehavior.KeepEpoch,
                currentSourceKind: currentSource.Kind,
                currentSourceId: currentSource.Id,
                currentView: currentView);
        }

        var previousSourceKind = binding.SourceKind!.Value;
        var previousSourceId = binding.SourceId!.Value;

        if (previousSourceKind != currentSource.Kind)
        {
            return new AgentContextTransition(
                AgentContextTransitionKind.SourceKindChanged,
                AgentContextTransitionDecision.AutoAdopted,
                AgentContextEpochBehavior.NewEpoch,
                previousSourceKind,
                previousSourceId,
                binding.LastView,
                currentSource.Kind,
                currentSource.Id,
                currentView,
                binding.Revision.Value,
                summary: $"{binding.DisplayNameOrKind()} -> {observation.Scope.DisplayName}");
        }

        if (previousSourceId != currentSource.Id)
        {
            return new AgentContextTransition(
                AgentContextTransitionKind.SourceEntityChanged,
                AgentContextTransitionDecision.AutoAdopted,
                AgentContextEpochBehavior.NewEpoch,
                previousSourceKind,
                previousSourceId,
                binding.LastView,
                currentSource.Kind,
                currentSource.Id,
                currentView,
                binding.Revision.Value,
                summary: $"{binding.DisplayNameOrKind()} -> {observation.Scope.DisplayName}");
        }

        if (!string.Equals(binding.LastView, currentView, StringComparison.OrdinalIgnoreCase) &&
            (binding.LastView.Length > 0 || currentView.Length > 0))
        {
            return new AgentContextTransition(
                AgentContextTransitionKind.ViewChanged,
                AgentContextTransitionDecision.Kept,
                AgentContextEpochBehavior.KeepEpoch,
                previousSourceKind,
                previousSourceId,
                binding.LastView,
                currentSource.Kind,
                currentSource.Id,
                currentView,
                binding.Revision.Value,
                summary: FormatViewTransition(binding.LastView, currentView));
        }

        if (!string.Equals(
                binding.LastSelectionId,
                currentSelectionId ?? string.Empty,
                StringComparison.Ordinal) &&
            (binding.LastSelectionId.Length > 0 || !string.IsNullOrEmpty(currentSelectionId)))
        {
            return new AgentContextTransition(
                AgentContextTransitionKind.SelectionChanged,
                AgentContextTransitionDecision.Kept,
                AgentContextEpochBehavior.KeepEpoch,
                previousSourceKind,
                previousSourceId,
                binding.LastView,
                currentSource.Kind,
                currentSource.Id,
                currentView,
                binding.Revision.Value,
                summary: "The selection changed inside the followed source.");
        }

        return new AgentContextTransition(
            AgentContextTransitionKind.None,
            AgentContextTransitionDecision.Kept,
            AgentContextEpochBehavior.KeepEpoch,
            previousSourceKind,
            previousSourceId,
            binding.LastView,
            currentSource.Kind,
            currentSource.Id,
            currentView,
            binding.Revision.Value);
    }

    private static string FormatViewTransition(string previousView, string currentView)
    {
        var previous = string.IsNullOrWhiteSpace(previousView) ? "(none)" : previousView;
        var current = string.IsNullOrWhiteSpace(currentView) ? "(none)" : currentView;
        return $"{Capitalize(previous)} -> {Capitalize(current)}";
    }

    private static string Capitalize(string value)
        => value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
}

internal static class AgentConversationContextBindingExtensions
{
    public static string DisplayNameOrKind(this AgentConversationContextBinding binding)
        => string.IsNullOrWhiteSpace(binding.DisplayName)
            ? binding.SourceKind?.Value ?? "(unknown)"
            : binding.DisplayName;
}
