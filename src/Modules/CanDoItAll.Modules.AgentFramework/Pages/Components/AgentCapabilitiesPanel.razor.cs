using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentCapabilitiesPanel : IDisposable {
    [Parameter] public Guid? PreferredAgentId { get; set; }
    [Parameter] public EventCallback<AgentDefinition?> SelectedAgentChanged { get; set; }
    [Parameter] public EventCallback<AgentChatContextAccessState> ContextAccessStateChanged { get; set; }
    [Inject] public AgentCapabilityOperations Operations { get; set; } = default!;
    [Inject] public IAgentCapabilitiesReads Reads { get; set; } = default!;
    [Inject] public IAgentCapabilitySetupFlowService CapabilitySetupFlowService { get; set; } = default!;
    [Inject] public CapabilityCuratorLaunch CuratorLaunch { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Inject] public DialogService DialogService { get; set; } = default!;

    private AgentCapabilitiesSession session = default!;
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    private AgentCapabilityOperationState? lastOperation;
    private long lastOperationGeneration;
    private AgentChatContextAccessState? publishedAccessState;
    private AgentDefinition? publishedAgent;
    private bool hasPublishedAgent;
    private Guid? appliedPreferredAgentId;
    private bool preferredAgentApplied;
    private long? previewGeneration;
    private CancellationTokenSource? previewCancellation;
    private long previewOwnerGeneration;
    private bool wizardOpen;
    private Guid? detailsOpen;
    private AgentCapabilityPreview? preview;

    private AgentCapabilitiesSnapshot Snapshot => session.Snapshot with {
        IsBusy = Operations.Find(session.TargetAgentId) is not null,
        Operation = Operations.Find(session.TargetAgentId) ?? (lastOperationGeneration == session.Generation ? lastOperation : null),
        IsAccessPreviewBusy = previewCancellation is not null && previewOwnerGeneration == session.Generation,
        IsOpeningCurator = CuratorLaunch.Status is CapabilityCuratorLaunchStatus.Pending or CapabilityCuratorLaunchStatus.Unconfirmed,
        CuratorLaunchStatus = CuratorLaunch.Status,
        IsOpeningWizard = wizardOpen,
        Preview = previewGeneration == session.Generation ? preview : null
    };

    protected override void OnInitialized() {
        session = new(Reads);
        Operations.Changed += HandleOperationsChanged;
        CuratorLaunch.Changed += HandleOperationsChanged;
    }

    private void HandleOperationsChanged() {
        if (!disposed) {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    protected override Task OnParametersSetAsync() {
        if (preferredAgentApplied && appliedPreferredAgentId == PreferredAgentId) {
            return Task.CompletedTask;
        }

        var initial = !preferredAgentApplied;
        preferredAgentApplied = true;
        appliedPreferredAgentId = PreferredAgentId;
        if (!initial && hasPublishedAgent && PreferredAgentId == publishedAgent?.Id) {
            return Task.CompletedTask;
        }

        return RunReadAsync(() => initial ? session.LoadAsync(PreferredAgentId) : session.SelectAsync(PreferredAgentId));
    }

    private async Task<bool> RunReadAsync(Func<Task<bool>> read, Action<long>? started = null) {
        CancelPreview();
        var pending = read();
        var generation = session.Generation;
        started?.Invoke(generation);
        await PublishAccessStateAsync(AgentChatContextAccessState.Loading);
        var applied = await pending;
        if (!applied || !session.IsCurrent(generation)) {
            return false;
        }

        if (!hasPublishedAgent || !ReferenceEquals(publishedAgent, session.SelectedAgent)) {
            hasPublishedAgent = true;
            publishedAgent = session.SelectedAgent;
            await SelectedAgentChanged.InvokeAsync(publishedAgent);
            if (!session.IsCurrent(generation)) {
                return false;
            }
        }

        await PublishAccessStateAsync(session.LoadState == AgentCapabilitiesLoadState.Ready
            ? AgentChatContextAccessState.Ready : AgentChatContextAccessState.Failed);
        return session.IsCurrent(generation);
    }

    private async Task PublishAccessStateAsync(AgentChatContextAccessState state) {
        if (publishedAccessState == state) {
            return;
        }

        publishedAccessState = state;
        await ContextAccessStateChanged.InvokeAsync(state);
    }

    private Task HandleIntentAsync(AgentCapabilitiesIntent intent) => intent switch {
        AgentCapabilitiesIntent.SelectAgent selected => RunReadAsync(() => session.SelectAsync(selected.AgentId)),
        AgentCapabilitiesIntent.ToggleAssignment assignment => ToggleCapabilityAsync(assignment.CapabilityId),
        AgentCapabilitiesIntent.VerifyCapability verification => VerifyCapabilityAsync(verification.CapabilityId),
        AgentCapabilitiesIntent.OpenDetails details => OpenCapabilityDetailsDialogAsync(details.CapabilityId),
        AgentCapabilitiesIntent.CreateCapability create => OpenCapabilityWizardAsync(create.Kind),
        AgentCapabilitiesIntent.PreviewAccess access => PreviewAccessAsync(access.Draft),
        AgentCapabilitiesIntent.OpenCurator => OpenCapabilityCuratorAsync(),
        AgentCapabilitiesIntent.RetryLoad => RunReadAsync(session.RefreshAsync),
        AgentCapabilitiesIntent.RecoverOperation => RecoverOperationAsync(),
        AgentCapabilitiesIntent.RetryAssignment => RetryAssignmentAsync(),
        AgentCapabilitiesIntent.AdoptCurrent => RecoverOperationAsync(adoptCurrent: true),
        _ => throw new ArgumentOutOfRangeException(nameof(intent))
    };

    private async Task ToggleCapabilityAsync(Guid capabilityId) {
        if (session.Draft is not { } draft || Snapshot.IsBusy) {
            return;
        }
        var generation = session.Generation;
        var outcome = await Operations.AssignAsync(draft, capabilityId, lifetime.Token);
        await ApplyAssignmentOutcomeAsync(outcome, generation);
    }

    private async Task ApplyAssignmentOutcomeAsync(AgentCapabilityOperationState? outcome, long generation) {
        if (outcome is null || !session.IsCurrent(generation) || session.TargetAgentId != outcome.AgentId) {
            return;
        }
        lastOperation = outcome;
        lastOperationGeneration = generation;
        if (outcome.CanReconcile) {
            await ReconcileOperationAsync(outcome, adoptCurrent: false);
        }
    }

    private async Task RetryAssignmentAsync() {
        if (Operations.Find(session.TargetAgentId) is not { CanRetry: true } current) {
            return;
        }
        var generation = session.Generation;
        var outcome = await Operations.RetryAsync(current.AgentId, current.AttemptId, lifetime.Token);
        await ApplyAssignmentOutcomeAsync(outcome, generation);
    }

    private async Task RecoverOperationAsync(bool adoptCurrent = false) {
        if (Operations.Find(session.TargetAgentId) is not { IsActive: false } current) {
            return;
        }
        var generation = session.Generation;
        if (current.CanVerify) {
            current = await Operations.VerifyAsync(current.AgentId, current.AttemptId, lifetime.Token);
        }
        if (current is null || !session.IsCurrent(generation) || session.TargetAgentId != current.AgentId) {
            return;
        }
        lastOperation = current;
        lastOperationGeneration = generation;
        if (current.CanReconcile || current.CanAdopt) {
            await ReconcileOperationAsync(current, adoptCurrent);
        }
    }

    private async Task ReconcileOperationAsync(AgentCapabilityOperationState outcome, bool adoptCurrent) {
        if (session.TargetAgentId != outcome.AgentId || disposed) {
            return;
        }
        var applied = await RunReadAsync(session.RefreshAsync);
        if (!applied || session.LoadState != AgentCapabilitiesLoadState.Ready || session.Selection.AgentId != outcome.AgentId) {
            return;
        }
        var completed = Operations.CompleteReconciliation(outcome.AgentId, outcome.AttemptId, adoptCurrent);
        lastOperation = completed ? outcome with {
            Status = AgentCapabilityOperationStatus.Reconciled,
            Message = outcome.Status == AgentCapabilityOperationStatus.CommittedWithWarning
                ? "Assignment saved and refreshed; directory projection still needs attention."
                : "Authoritative capability state refreshed. No mutation was replayed."
        } : outcome;
        lastOperationGeneration = session.Generation;
    }

    private async Task VerifyCapabilityAsync(Guid capabilityId) {
        if (session.Selection.AgentId is not { } agentId || Snapshot.IsBusy) {
            return;
        }
        var generation = session.Generation;
        var outcome = await Operations.DiagnoseAsync(agentId, capabilityId, lifetime.Token);
        await ApplyAssignmentOutcomeAsync(outcome, generation);
    }

    private IReadOnlyList<string> AvailableCapabilityTags => session.Snapshot.Capabilities
        .SelectMany(capability => capability.Tags)
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private async Task OpenCapabilityDetailsDialogAsync(Guid capabilityId) {
        if (disposed || detailsOpen.HasValue) {
            return;
        }
        detailsOpen = capabilityId;
        var capability = session.Snapshot.Capabilities.FirstOrDefault(item => item.Id == capabilityId);
        try {
            var result = await DialogService.OpenAsync<CapabilityDetailsDialog>(
                capability?.Name ?? "Capability details",
                new Dictionary<string, object?> {
                    [nameof(CapabilityDetailsDialog.CapabilityId)] = capabilityId,
                    [nameof(CapabilityDetailsDialog.TagSuggestions)] = AvailableCapabilityTags,
                    [nameof(CapabilityDetailsDialog.OwnerCancellationToken)] = lifetime.Token
                },
                new DialogOptions {
                    Eyebrow = "Capability metadata",
                    Subtitle = "Inspect and edit capability tags, identity, and type-specific configuration.",
                    Size = ModalSize.Wide, DenseChrome = true, AriaLabel = "Capability details",
                    TestId = "agents-capability-details-dialog"
                }, lifetime.Token);
            if (!disposed && result is CapabilityDetailsDialogResult) {
                await RunReadAsync(session.RefreshAsync);
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        } catch (Exception) {
            if (!disposed) {
                NotificationService.Error("Capability dialog unavailable", "Refresh the capability catalog before continuing.");
            }
        } finally {
            detailsOpen = null;
        }
    }

    private async Task OpenCapabilityWizardAsync(CapabilityKind initialKind) {
        if (disposed || Snapshot.IsBusy || wizardOpen) {
            return;
        }
        wizardOpen = true;
        try {
            var result = await DialogService.OpenAsync<CapabilitySetupWizardDialog>(
                initialKind switch {
                    CapabilityKind.McpServer => "New MCP server",
                    CapabilityKind.Tool => "New tool",
                    _ => "New skill"
                },
                new Dictionary<string, object?> {
                    [nameof(CapabilitySetupWizardDialog.InitialKind)] = initialKind,
                    [nameof(CapabilitySetupWizardDialog.TagSuggestions)] = AvailableCapabilityTags,
                    [nameof(CapabilitySetupWizardDialog.OwnerCancellationToken)] = lifetime.Token
                },
                new DialogOptions {
                    Eyebrow = "Capability setup",
                    Subtitle = "Create a skill, tool, or MCP capability for assignment to technical agents.",
                    Size = ModalSize.Wide, DenseChrome = true, AriaLabel = "Capability setup wizard",
                    TestId = "agents-capability-setup-dialog"
                }, lifetime.Token);
            if (!disposed && result is CapabilityDetailsDialogResult && await RunReadAsync(session.RefreshAsync)) {
                NotificationService.Success("Ready", "Capability created.");
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        } catch (Exception) {
            if (!disposed) {
                NotificationService.Error("Capability wizard unavailable", "Refresh the capability catalog before continuing.");
            }
        } finally {
            wizardOpen = false;
        }
    }

    private async Task OpenCapabilityCuratorAsync() {
        if (disposed || !Snapshot.Curator.CanLaunch || Snapshot.IsBusy || Snapshot.IsOpeningCurator) {
            return;
        }
        var started = await CuratorLaunch.OpenAsync(lifetime.Token);
        if (!disposed && started && CuratorLaunch.Status == CapabilityCuratorLaunchStatus.Opened) {
            NotificationService.Success("Capability Curator ready", "Opened a new managed capability chat.");
        }
    }

    private void CancelPreview() {
        var cancellation = previewCancellation;
        previewCancellation = null;
        cancellation?.Cancel();
    }

    private async Task PreviewAccessAsync(AgentCapabilityAccessDraft draft) {
        if (disposed) {
            return;
        }
        CancelPreview();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        previewCancellation = cancellation;
        var owner = session.Generation;
        previewOwnerGeneration = owner;
        try {
            var result = await CapabilitySetupFlowService.PreviewAccessAsync(new CapabilityAccessPreviewRequest {
                CapabilityIds = session.Snapshot.SelectedCapabilityIds,
                Policy = new CapabilityAccessPolicyTemplateDto {
                    DefaultEffect = "inherit",
                    Rules = [new CapabilityAccessRuleTemplateDto {
                        Id = "ui-preview-rule",
                        Effect = ProtocolToken(draft.Effect),
                        Scope = ProtocolToken(draft.Scope),
                        Selector = new CapabilitySelectorTemplateDto {
                            Kind = ProtocolToken(draft.Selector),
                            Value = draft.Value,
                            ServerKey = draft.ServerKey
                        },
                        Reason = draft.Reason
                    }]
                }
            }, cancellation.Token);
            if (!session.IsCurrent(owner) || !ReferenceEquals(previewCancellation, cancellation) || cancellation.IsCancellationRequested) {
                return;
            }

            previewGeneration = owner;
            preview = new(result.ValidationResult.IsValid, result.EffectiveSet.AllowedCapabilities.Count,
                result.EffectiveSet.Diagnostics.Count,
                result.ValidationResult.Issues.Take(4).Select(issue =>
                    new AgentCapabilityNotice(issue.FieldPath, issue.Message, issue.RepairHint)).ToImmutableArray(),
                result.EffectiveSet.Diagnostics.Take(4).Select(diagnostic =>
                    new AgentCapabilityNotice(diagnostic.Identity.Key.Value, diagnostic.Reason, diagnostic.RepairHint)).ToImmutableArray());
            if (preview.IsValid) {
                NotificationService.Success("Access preview ready", "Capability access policy preview completed.");
            } else {
                NotificationService.Warning("Access preview has validation issues", "Review the policy diagnostics.");
            }
        } catch (Exception) {
            if (session.IsCurrent(owner) && ReferenceEquals(previewCancellation, cancellation) && !cancellation.IsCancellationRequested) {
                NotificationService.Error("Access preview failed", "The preview could not be completed. Its draft is preserved.");
            }
        } finally {
            if (ReferenceEquals(previewCancellation, cancellation)) {
                previewCancellation = null;
            }
        }
    }

    private static string ProtocolToken<T>(T value) where T : struct, Enum {
        if (!Enum.IsDefined(value)) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Operations.Changed -= HandleOperationsChanged;
        CuratorLaunch.Changed -= HandleOperationsChanged;
        CancelPreview();
        lifetime.Cancel();
        lifetime.Dispose();
        session.Dispose();
    }
}
