using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework.Pages.Authoring;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class WorkflowCanvasEditor : IDisposable {
    [Inject] public IWorkflowCatalogService CatalogService { get; set; } = default!;
    [Inject] public IWorkflowExecutorCatalog ExecutorCatalog { get; set; } = default!;
    [Inject] public IWorkflowRuntimeBackendCatalog RuntimeBackendCatalog { get; set; } = default!;
    [Inject] public IWorkflowComponentLibraryService ComponentLibrary { get; set; } = default!;
    [Inject] public IWorkflowTestRunner TestRunner { get; set; } = default!;
    [Inject] public IWorkflowStructureAuthorityFactory StructureAuthority { get; set; } = default!;
    [Inject] public IProjectStructureRuntimeGateway ProjectStructureGateway { get; set; } = default!;
    [Inject] public SecretService SecretService { get; set; } = default!;
    [Inject] public IPromptGalleryService PromptGallery { get; set; } = default!;
    [Inject] public ILogger<WorkflowCanvasEditor> Logger { get; set; } = default!;
    [Parameter] public WorkflowDefinition? Definition { get; set; }
    [Parameter] public IReadOnlyList<LlmCallComponent> Components { get; set; } = [];
    [Parameter] public IReadOnlyList<WorkflowProviderOption> ProviderOptions { get; set; } = [];
    [Parameter] public CancellationToken OwnerToken { get; set; }
    [Parameter] public EventCallback NewDraftRequested { get; set; }
    [Parameter] public EventCallback<WorkflowDefinition> DefinitionSaved { get; set; }
    [Parameter] public EventCallback<WorkflowRunSnapshot> PreviewRunCompleted { get; set; }
    [Parameter] public EventCallback ComponentLibraryChanged { get; set; }
    [Parameter] public EventCallback<WorkflowAgentChatNodeSelection?> SelectedNodeChanged { get; set; }

    public WorkflowDocumentOwner DocumentOwner { get; private set; } = default!;
    public WorkflowPromptBindingOwner BindingOwner { get; private set; } = default!;
    public WorkflowPreviewOwner PreviewOwner { get; private set; } = default!;
    private CancellationTokenSource lifetime = default!;
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<WorkflowSecretOption> secretOptions = [];
    private string secretLoadError = string.Empty;
    private bool ready;
    private bool disposed;

    protected override async Task OnInitializedAsync() {
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(OwnerToken);
        DocumentOwner = new(CatalogService, Logger);
        BindingOwner = new(PromptGallery, ComponentLibrary, Logger);
        PreviewOwner = new(TestRunner, StructureAuthority, ProjectStructureGateway, ExecutorCatalog, Logger);
        try {
            var result = await SecretService.ListForPickerAsync();
            if (lifetime.IsCancellationRequested) {
                return;
            }
            secrets = result;
            secretOptions = result.Select(secret => new WorkflowSecretOption(secret.Id, secret.Name, secret.Kind.ToString())).ToArray();
        } catch (Exception error) {
            if (lifetime.IsCancellationRequested) {
                return;
            }
            secretLoadError = "Secrets could not be loaded for executor settings.";
            Logger.LogWarning("Workflow secret metadata unavailable: {FailureType}", error.GetType().Name);
        }
        ready = true;
    }

    private Task HandleSelectedNodeChangedAsync(WorkflowCanvasNodeSelection? selection)
        => lifetime.IsCancellationRequested ? Task.CompletedTask : SelectedNodeChanged.InvokeAsync(selection is null ? null
            : new(selection.WorkflowId, selection.NodeId, selection.Name, selection.Kind));

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
