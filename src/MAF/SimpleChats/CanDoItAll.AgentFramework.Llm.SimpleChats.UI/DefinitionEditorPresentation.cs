using System.Collections.Immutable;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.UI;

public enum DefinitionEditorSection { Identity, Runtime, Output }
public enum DefinitionEditorFormat { Text, Json, JsonSchema }
public enum DefinitionEditorEffort { None, Minimal, Low, Medium, High, ExtraHigh, Max }
public enum DefinitionEditorEffortSupport { Supported, Unsupported, Unknown }
public enum DefinitionEditorPhase { Loading, Ready, Denied, Failed }
public enum DefinitionEditorFailure { Authorization, Unavailable, InvalidTarget, Validation, Conflict }
public enum DefinitionEditorAction { Save, Cancel, Reload, ChangeStatus }

public sealed record DefinitionEditorValues {
    public string Name { get; init; } = "";
    public string Summary { get; init; } = "";
    public string AvatarImageUrl { get; init; } = "";
    public string SystemPrompt { get; init; } = "";
    public Guid? ProviderProfileId { get; init; }
    public string Model { get; init; } = "";
    public double? Temperature { get; init; }
    public DefinitionEditorEffort? ThinkingEffort { get; init; }
    public string ModelParameterConfigurationJson { get; init; } = "";
    public double? TimeoutSeconds { get; init; }
    public DefinitionEditorFormat ResponseFormat { get; init; }
    public string SchemaJson { get; init; } = "";
    public string SchemaName { get; init; } = "";
    public string SchemaDescription { get; init; } = "";
    public string RevisionReason { get; init; } = "";
    public ImmutableArray<string> Tags { get; init; } = [];
}

public sealed record DefinitionEditorModel(string Model, DefinitionEditorEffortSupport Support,
    ImmutableArray<DefinitionEditorEffort> AllowedEfforts);
public sealed record DefinitionEditorProvider(ConversationProviderOption Option, ImmutableArray<DefinitionEditorModel> Models);
public sealed record DefinitionEditorIntent(long Generation, DefinitionEditorAction Action,
    DefinitionEditorValues? Submission = null, LlmChatDefinitionStatusFilter? Status = null);
public sealed record DefinitionEditorAvatarContext(DefinitionEditorValues Values, ConversationAvatarPresentation Avatar,
    EventCallback<string> Changed, bool IsBusy);

public sealed record DefinitionEditorPresentation(long Generation, Guid? Target, DefinitionEditorPhase Phase,
    DefinitionEditorValues? Source = null, DefinitionCatalogCard? Definition = null,
    ImmutableArray<DefinitionEditorProvider> Providers = default, bool ProvidersLoading = false,
    bool ProvidersUnavailable = false, bool IsSaving = false, DefinitionEditorFailure? Failure = null,
    string ValidationMessage = "") {
    public static DefinitionEditorPresentation Initial { get; } = new(0, null, DefinitionEditorPhase.Loading);
    public string Title => Target.HasValue ? "Edit definition" : "New definition";
    public string Error => Failure switch {
        DefinitionEditorFailure.Authorization => "Manage Simple Chats permission is required to edit reusable definitions.",
        DefinitionEditorFailure.Unavailable => "The definition request could not be completed. Reload or reopen the editor to try again.",
        DefinitionEditorFailure.InvalidTarget => "Select a valid Simple Chat definition.",
        DefinitionEditorFailure.Validation => ValidationMessage,
        DefinitionEditorFailure.Conflict => "The definition changed after it was opened. Reload it before saving again.",
        _ => ""
    };
    public string ProviderError => ProvidersUnavailable ? "Provider choices could not be loaded. Existing values are retained." : "";
}

public static class DefinitionEditorTransitions {
    public static ImmutableArray<LlmChatDefinitionStatusFilter> From(LlmChatDefinitionStatusFilter status) => status switch {
        LlmChatDefinitionStatusFilter.Draft => [LlmChatDefinitionStatusFilter.Active, LlmChatDefinitionStatusFilter.Archived],
        LlmChatDefinitionStatusFilter.Active => [LlmChatDefinitionStatusFilter.Suspended, LlmChatDefinitionStatusFilter.Archived],
        LlmChatDefinitionStatusFilter.Suspended => [LlmChatDefinitionStatusFilter.Active, LlmChatDefinitionStatusFilter.Archived],
        LlmChatDefinitionStatusFilter.Archived => [],
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
    public static string Label(LlmChatDefinitionStatusFilter status) => status switch {
        LlmChatDefinitionStatusFilter.Active => "Activate",
        LlmChatDefinitionStatusFilter.Suspended => "Suspend",
        LlmChatDefinitionStatusFilter.Archived => "Archive",
        LlmChatDefinitionStatusFilter.Draft => "Move to draft",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
