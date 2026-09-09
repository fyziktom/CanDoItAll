using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum DefinitionEditorScenario { Loading, Denied, New, Existing, ProviderPartial, Validation, Conflict, Adversarial }

public sealed class DefinitionEditorSandboxFixture {
    public static readonly Guid ProviderId = Guid.Parse("32000000-0000-0000-0000-000000000001");
    public DefinitionEditorScenario Scenario { get; private set; } = DefinitionEditorScenario.Existing;
    public DefinitionEditorPresentation Presentation { get; private set; } = Create(DefinitionEditorScenario.Existing);
    public string IntentLog { get; private set; } = "No intent";
    public bool IsOpen { get; private set; } = true;

    public void SetScenario(DefinitionEditorScenario scenario) {
        Scenario = scenario;
        Presentation = Create(scenario);
        IsOpen = true;
    }
    public void Apply(DefinitionEditorIntent intent) {
        IntentLog = intent.Action.ToString();
        if (intent.Action == DefinitionEditorAction.Cancel) {
            IsOpen = false;
        } else if (intent.Action == DefinitionEditorAction.Save) {
            Presentation = Presentation with { Source = intent.Submission, Failure = null };
        } else if (intent.Action == DefinitionEditorAction.Reload) {
            Presentation = Create(DefinitionEditorScenario.Existing);
        } else if (intent.Status is { } status && Presentation.Definition is { } definition) {
            Presentation = Presentation with { Definition = definition with { Status = status } };
        }
    }
    public static DefinitionEditorPresentation Create(DefinitionEditorScenario scenario) {
        var definition = DefinitionCatalogSandboxFixture.Card() with { Status = LlmChatDefinitionStatusFilter.Draft };
        var source = new DefinitionEditorValues {
            Name = definition.Name, Summary = definition.Summary, Tags = definition.Tags,
            ProviderProfileId = ProviderId, Model = "sample-model", SystemPrompt = "Summarize the supplied research.",
            Temperature = 0.2, ThinkingEffort = DefinitionEditorEffort.Low, TimeoutSeconds = 30,
            ModelParameterConfigurationJson = "{}", SchemaName = "answer", SchemaJson = "{\"type\":\"object\"}"
        };
        var provider = new DefinitionEditorProvider(new ConversationProviderOption(new(ProviderId.ToString("D")),
            "Synthetic provider", true, "sample-model", ["sample-model"]),
            [new("sample-model", DefinitionEditorEffortSupport.Supported, [DefinitionEditorEffort.Low, DefinitionEditorEffort.High])]);
        var presentation = new DefinitionEditorPresentation(1, definition.DefinitionId, DefinitionEditorPhase.Ready,
            source, definition, [provider]);
        return scenario switch {
            DefinitionEditorScenario.Loading => presentation with { Phase = DefinitionEditorPhase.Loading, Source = null },
            DefinitionEditorScenario.Denied => presentation with { Phase = DefinitionEditorPhase.Denied, Source = null },
            DefinitionEditorScenario.New => presentation with { Target = null, Definition = null, Source = new() },
            DefinitionEditorScenario.Existing => presentation,
            DefinitionEditorScenario.ProviderPartial => presentation with { Providers = [], ProvidersUnavailable = true },
            DefinitionEditorScenario.Validation => presentation with { Failure = DefinitionEditorFailure.Validation, ValidationMessage = "Enter a definition name." },
            DefinitionEditorScenario.Conflict => presentation with { Failure = DefinitionEditorFailure.Conflict },
            DefinitionEditorScenario.Adversarial => presentation with { Source = source with {
                Name = "<script id='editor-injected'>unsafe()</script> " + new string('界', 120),
                Summary = new string('W', 500), SystemPrompt = string.Join('\n', Enumerable.Repeat("Long prompt with <encoded> markup.", 35)),
                ResponseFormat = DefinitionEditorFormat.JsonSchema
            } },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }
}
