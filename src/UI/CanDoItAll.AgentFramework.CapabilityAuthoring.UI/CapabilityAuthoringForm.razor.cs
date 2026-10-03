using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI;

public partial class CapabilityAuthoringForm : IDisposable {
    [Parameter, EditorRequired] public CapabilityAuthoringOperations Operations { get; set; } = default!;
    [Parameter] public CapabilityAuthoringMode Mode { get; set; }
    [Parameter] public Guid? CapabilityId { get; set; }
    [Parameter] public CapabilityKind InitialKind { get; set; } = CapabilityKind.McpServer;
    [Parameter] public IReadOnlyList<string> TagSuggestions { get; set; } = [];
    [Parameter] public CancellationToken OwnerCancellationToken { get; set; }
    [Parameter] public EventCallback<Guid> Completed { get; set; }
    [Parameter] public EventCallback Cancelled { get; set; }
    [Inject] public NotificationService Notifications { get; set; } = default!;
    [Inject] public ILogger<CapabilityAuthoringForm> Logger { get; set; } = default!;
    private CapabilityAuthoringSession? session;
    private CapabilityAuthoringOperations? boundOperations;
    private (CapabilityAuthoringMode Mode, Guid? Id, CapabilityKind Kind, CancellationToken Token) boundTarget;
    private EditContext? observedContext;
    private int step;
    private int tab;
    private bool disposed;
    private string TestId => Mode == CapabilityAuthoringMode.Wizard ? "agents-capability-setup" : "agents-capability-details";

    protected override async Task OnParametersSetAsync() {
        if (disposed) {
            return;
        }
        var target = (Mode, CapabilityId, InitialKind, OwnerCancellationToken);
        if (session is not null && ReferenceEquals(boundOperations, Operations) && boundTarget == target) {
            return;
        }
        Retire();
        boundTarget = target;
        boundOperations = Operations;
        step = 0;
        tab = 0;
        var owner = new CapabilityAuthoringSession(Operations, Mode == CapabilityAuthoringMode.Details ? CapabilityId : null,
            InitialKind, OwnerCancellationToken, Notifications, Logger);
        session = owner;
        owner.StateChanged += SessionChanged;
        await owner.LoadAsync();
        if (IsCurrent(owner)) {
            Observe(owner.Draft.Context);
        }
    }

    private bool IsCurrent(CapabilityAuthoringSession owner) => !disposed && ReferenceEquals(session, owner) && owner.IsCurrent;
    private void SessionChanged() {
        if (!disposed) {
            _ = InvokeAsync(StateHasChanged);
        }
    }
    private void Observe(EditContext context) {
        if (observedContext is not null) {
            observedContext.OnFieldChanged -= DraftChanged;
            observedContext.OnValidationStateChanged -= ValidationChanged;
        }
        observedContext = context;
        observedContext.OnFieldChanged += DraftChanged;
        observedContext.OnValidationStateChanged += ValidationChanged;
    }

    private void DraftChanged(object? sender, FieldChangedEventArgs args) {
        if (!disposed && ReferenceEquals(sender, observedContext)) {
            StateHasChanged();
        }
    }

    private void ValidationChanged(object? sender, ValidationStateChangedEventArgs args) {
        if (!disposed && ReferenceEquals(sender, observedContext)) {
            StateHasChanged();
        }
    }

    private async Task RetryAsync(CapabilityAuthoringSession owner) {
        if (!IsCurrent(owner)) {
            return;
        }
        await owner.LoadAsync();
        if (IsCurrent(owner)) {
            Observe(owner.Draft.Context);
        }
    }

    private void SelectStep(CapabilityAuthoringSession owner, int value) {
        if (IsCurrent(owner)) {
            step = Math.Clamp(value, 0, 2);
        }
    }

    private void SelectTab(CapabilityAuthoringSession owner, int value) {
        if (IsCurrent(owner)) {
            tab = value;
        }
    }

    private void Next(CapabilityAuthoringSession owner) {
        if (IsCurrent(owner) && owner.ValidateStep(identityOnly: step == 0)) {
            step = Math.Min(2, step + 1);
        }
    }

    private async Task SaveAsync(CapabilityAuthoringSession owner) {
        if (!IsCurrent(owner)) {
            return;
        }
        if (Mode == CapabilityAuthoringMode.Wizard && step < 2) {
            Next(owner);
            return;
        }
        if (await owner.SaveAsync() && IsCurrent(owner)) {
            await CompleteAsync(owner);
        }
    }

    private async Task CompleteAsync(CapabilityAuthoringSession owner) {
        if (!IsCurrent(owner) || owner.AcceptedId is not { } id) {
            return;
        }
        try {
            await Completed.InvokeAsync(id);
        } catch (Exception error) when (IsCurrent(owner)) {
            owner.CompletionFailed();
            Logger.LogWarning("Capability parent refresh failed after save {CapabilityId}: {FailureType}", id, error.GetType().Name);
        }
    }

    private async Task CancelAsync(CapabilityAuthoringSession owner) {
        if (!IsCurrent(owner)) {
            return;
        }
        owner.Dispose();
        await Cancelled.InvokeAsync();
    }

    private static string ConfigurationSummary(CapabilityAuthoringDraft draft) => draft.Model.Kind switch {
        CapabilityKind.McpServer => $"{draft.Mcp.Transport}: {draft.Mcp.ServerName}",
        CapabilityKind.Skill => $"{draft.SkillMode}: {draft.Skill.InlineName}",
        CapabilityKind.Tool => $"{draft.Tool.ToolKind}: {draft.Tool.RuntimeToolName}",
        _ => draft.Model.Kind.ToString()
    };

    private void Retire() {
        if (observedContext is not null) {
            observedContext.OnFieldChanged -= DraftChanged;
            observedContext.OnValidationStateChanged -= ValidationChanged;
        }
        observedContext = null;
        if (session is not null) {
            session.StateChanged -= SessionChanged;
            session.Dispose();
        }
        session = null;
    }

    public void Dispose() {
        disposed = true;
        Retire();
    }
}
