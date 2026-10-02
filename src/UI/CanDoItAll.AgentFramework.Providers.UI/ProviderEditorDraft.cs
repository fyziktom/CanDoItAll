using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.Providers.UI;

public sealed class ProviderEditorDraft {
    private readonly Dictionary<FieldIdentifier, long> revisions = [];
    private readonly ValidationMessageStore validation;
    private IReadOnlyList<string> synchronizedModels;
    private string suggestedModelsText;

    private ProviderEditorDraft(EditContext context) {
        Context = context;
        Model = (ProviderProfileEditorModel)context.Model;
        synchronizedModels = Model.SuggestedModels.ToArray();
        suggestedModelsText = string.Join(Environment.NewLine, synchronizedModels);
        validation = new(context);
        context.OnFieldChanged += (_, args) => {
            revisions[args.FieldIdentifier] = Revision(args.FieldIdentifier) + 1;
            if (args.FieldIdentifier.Equals(new FieldIdentifier(Model, nameof(Model.ConfigurationJson)))) {
                ValidateConfiguration();
            }
        };
        context.OnValidationRequested += (_, _) => ValidateConfiguration();
    }

    public ProviderProfileEditorModel Model { get; }
    public EditContext Context { get; }
    public static ProviderEditorDraft For(EditContext context) {
        if (!context.Properties.TryGetValue(typeof(ProviderEditorDraft), out var value)) {
            value = new ProviderEditorDraft(context);
            context.Properties[typeof(ProviderEditorDraft)] = value;
        }
        return (ProviderEditorDraft)value;
    }

    public string SuggestedModelsText {
        get {
            if (!synchronizedModels.SequenceEqual(Model.SuggestedModels, StringComparer.Ordinal)) {
                synchronizedModels = Model.SuggestedModels.ToArray();
                suggestedModelsText = string.Join(Environment.NewLine, synchronizedModels);
            }
            return suggestedModelsText;
        }
        set {
            suggestedModelsText = value;
            Model.SuggestedModels = value.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            synchronizedModels = Model.SuggestedModels.ToArray();
            Notify(nameof(Model.SuggestedModels));
        }
    }

    public long Revision(FieldIdentifier field) => revisions.GetValueOrDefault(field);
    public IReadOnlyDictionary<FieldIdentifier, long> CaptureRevisions() => new Dictionary<FieldIdentifier, long>(revisions);
    public void Notify(string property) => Context.NotifyFieldChanged(new(Model, property));
    public bool Validate() => Context.Validate();

    public void ChangeKind(ProviderKind kind) {
        if (Model.Kind == kind) {
            return;
        }
        Model.Kind = kind;
        Model.BaseUrl = string.Empty;
        Model.ApiKeyEnvironmentVariable = string.Empty;
        Model.DefaultModel = string.Empty;
        Model.ConfigurationJson = "{}";
        Model.SuggestedModels = [];
        Model.ModelPrices = [];
        Model.ModelThinkingEffortCapabilities = [];
        Model.Tags = [];
        Model.IsPrivateProvider = ProviderPricingDefaults.IsPrivateProvider(kind);
        Model.Transport = kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi
            ? ProviderTransportKind.Responses : ProviderTransportKind.ChatCompletions;
        Model.SupportsBackgroundResponses = false;
        Notify(nameof(Model.Kind));
        Notify(nameof(Model.ConfigurationJson));
    }

    private void ValidateConfiguration() {
        validation.Clear();
        if (!string.IsNullOrWhiteSpace(Model.ConfigurationJson)) {
            try {
                using var document = JsonDocument.Parse(Model.ConfigurationJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object) {
                    validation.Add(new FieldIdentifier(Model, nameof(Model.ConfigurationJson)), "Configuration must be a JSON object.");
                }
            } catch (JsonException) {
                validation.Add(new FieldIdentifier(Model, nameof(Model.ConfigurationJson)), "Configuration must contain valid JSON. Your text is retained.");
            }
        }
        Context.NotifyValidationStateChanged();
    }

    public static ProviderProfileEditorModel Copy(ProviderProfileEditorModel source) => new() {
        Id = source.Id,
        ExpectedConcurrencyToken = source.ExpectedConcurrencyToken,
        Name = source.Name,
        Kind = source.Kind,
        BaseUrl = source.BaseUrl,
        ApiKeyEnvironmentVariable = source.ApiKeyEnvironmentVariable,
        DefaultModel = source.DefaultModel,
        Transport = source.Transport,
        Purpose = source.Purpose,
        IsEnabled = source.IsEnabled,
        SupportsStreaming = source.SupportsStreaming,
        SupportsTools = source.SupportsTools,
        PreferFrameworkManagedChatHistory = source.PreferFrameworkManagedChatHistory,
        SupportsBackgroundResponses = source.SupportsBackgroundResponses,
        ConfigurationJson = source.ConfigurationJson,
        Notes = source.Notes,
        IsPrivateProvider = source.IsPrivateProvider,
        SuggestedModels = source.SuggestedModels.ToList(),
        ModelPrices = ProviderPricingDefaults.ToEditorModels(ProviderPricingDefaults.FromEditorModels(source.ModelPrices)),
        Tags = source.Tags.ToList(),
        ModelThinkingEffortCapabilities = source.ModelThinkingEffortCapabilities?.Select(capability => capability with { AllowedEfforts = capability.AllowedEfforts.ToArray() }).ToList()
    };

}
