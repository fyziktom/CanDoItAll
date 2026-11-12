using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AppComponents;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class ExternalWorkspaceRootSelectionField {
    private const string InvalidCandidateMessage =
        "Enter a native absolute path or a canonical external-target/v1/... alias.";

    [Inject]
    public IExternalTargetPathRegistryFactory ExternalTargetPathRegistryFactory { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<string> AllowedAliases { get; set; } = [];

    [Parameter, EditorRequired]
    public IReadOnlyList<ExternalTargetRootBinding> RootBindings { get; set; } = [];

    [Parameter, EditorRequired]
    public EventCallback<ExternalWorkspaceRootSelection> SelectionChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string DataTestId { get; set; } = "agents-catalog-workspace-external-roots";

    private IReadOnlyList<string> selectedAliases = [];
    private IReadOnlyList<ExternalTargetRootBinding> selectedBindings = [];
    private IExternalTargetPathRegistry? pathRegistry;
    private string? registryLoadError;
    [Parameter] public AgentRootEntry Entry { get; set; } = new();
    [Parameter] public CancellationToken OwnerLifetime { get; set; }
    [Parameter] public string EntryFormId { get; set; } = string.Empty;
    private string? validationMessage { get => Entry.ValidationMessage; set => Entry.ValidationMessage = value; }
    private string candidate { get => Entry.Candidate; set => Entry.Candidate = value; }
    private bool initialized;

    private string InputId => $"{DataTestId}-input";

    private string HintId => $"{DataTestId}-hint";

    private string ValidationId => $"{DataTestId}-validation-message";

    private bool HasValidationMessage => !string.IsNullOrWhiteSpace(validationMessage);

    private string InputDescriptionIds => HasValidationMessage
        ? $"{HintId} {ValidationId}"
        : HintId;

    private IReadOnlyList<SelectedReferenceItem<string>> SelectedRoots => selectedAliases
        .Select(CreateReferenceItem)
        .ToList();

    protected override void OnParametersSet() {
        var normalized = NormalizeSelection(AllowedAliases, RootBindings);
        if (initialized && selectedAliases.SequenceEqual(normalized.AllowedAliases) && selectedBindings.SequenceEqual(normalized.RootBindings)) {
            return;
        }
        initialized = true;
        selectedAliases = normalized.AllowedAliases;
        selectedBindings = normalized.RootBindings;
        CreatePathRegistry();
    }

    private void HandleCandidateInput(string value) {
        if (OwnerLifetime.IsCancellationRequested) {
            return;
        }
        candidate = value;
        validationMessage = null;
    }

    private async Task AddCandidateAsync() {
        if (Disabled || OwnerLifetime.IsCancellationRequested) {
            return;
        }

        var input = candidate.Trim();
        if (string.IsNullOrWhiteSpace(input) || pathRegistry is null) {
            validationMessage = InvalidCandidateMessage;
            return;
        }

        string? alias;
        try {
            alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(input, pathRegistry);
        }
        catch (InvalidOperationException) {
            validationMessage = "The root could not be bound on this host. Check the native path or existing alias.";
            return;
        }

        if (string.IsNullOrWhiteSpace(alias)) {
            validationMessage = InvalidCandidateMessage;
            return;
        }

        if (selectedAliases.Contains(alias, ExternalTargetAliasCodec.EqualityComparer)) {
            validationMessage = "This external workspace root is already selected.";
            return;
        }

        var nextBindings = selectedBindings
            .Concat(pathRegistry.ExportBindings([alias]))
            .ToList();
        var next = NormalizeSelection(selectedAliases.Append(alias), nextBindings);
        ApplySelection(next);
        candidate = string.Empty;
        validationMessage = null;
        await SelectionChanged.InvokeAsync(next);
    }

    private async Task RemoveAsync(string alias) {
        if (Disabled || OwnerLifetime.IsCancellationRequested) {
            return;
        }

        var next = NormalizeSelection(
            selectedAliases.Where(selected =>
                !ExternalTargetAliasCodec.EqualityComparer.Equals(selected, alias)),
            selectedBindings);
        ApplySelection(next);
        validationMessage = null;
        await SelectionChanged.InvokeAsync(next);
    }

    private SelectedReferenceItem<string> CreateReferenceItem(string alias) {
        var physicalPath = string.Empty;
        var resolutionMessage = string.Empty;
        var resolution = pathRegistry is null
            ? ExternalTargetAliasResolutionKind.Unbound
            : pathRegistry.TryResolve(alias, out physicalPath, out resolutionMessage);
        if (resolution == ExternalTargetAliasResolutionKind.Resolved) {
            return new SelectedReferenceItem<string>(alias, physicalPath, alias) {
                TestId = BuildRowTestId(alias),
                CanRemove = true
            };
        }

        var detail = registryLoadError ?? resolutionMessage;
        if (string.IsNullOrWhiteSpace(detail)) {
            detail = "This alias does not have a root binding that can be opened on the current host.";
        }

        return new SelectedReferenceItem<string>(alias, "Path unavailable on this host", alias) {
            DetailText = detail,
            StatusText = "Unresolved",
            StatusTone = SelectedReferenceStatusTone.Warning,
            TestId = BuildRowTestId(alias),
            CanRemove = true
        };
    }

    private void ApplySelection(ExternalWorkspaceRootSelection selection) {
        selectedAliases = selection.AllowedAliases;
        selectedBindings = selection.RootBindings;
        CreatePathRegistry();
    }

    private void CreatePathRegistry() {
        registryLoadError = null;
        try {
            pathRegistry = ExternalTargetPathRegistryFactory.Create(selectedBindings);
        }
        catch (InvalidOperationException) {
            registryLoadError = "The saved workspace root bindings could not be resolved on this host.";
            pathRegistry = null;
        }
    }

    private ExternalWorkspaceRootSelection NormalizeSelection(
        IEnumerable<string> aliases,
        IEnumerable<ExternalTargetRootBinding> bindings) {
        var normalized = AgentWorkspaceToolAccessMetadata.Normalize(new AgentWorkspaceToolAccessSettings {
            AllowedExternalTargetAliases = aliases.ToList(),
            ExternalTargetRootBindings = bindings.ToList()
        });
        return new ExternalWorkspaceRootSelection(
            normalized.AllowedExternalTargetAliases.ToArray(),
            normalized.ExternalTargetRootBindings.ToArray());
    }

    private string BuildRowTestId(string alias) {
        var safeAlias = new string(alias
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray());
        var key = string.Join(
            '-',
            safeAlias.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return $"{DataTestId}-row-{key}";
    }
}
