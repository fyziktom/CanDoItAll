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
    [Inject] public IAgentCapabilitiesReads Reads { get; set; } = default!;
    [Inject] public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;
    [Inject] public IAgentCapabilitySetupFlowService CapabilitySetupFlowService { get; set; } = default!;
    [Inject] public IAgentChatLauncher AgentChatLauncher { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Inject] public DialogService DialogService { get; set; } = default!;

    private AgentCapabilitiesSession session = default!;
    private AgentChatContextAccessState? publishedAccessState;
    private AgentDefinition? publishedAgent;
    private bool hasPublishedAgent;
    private Guid? appliedPreferredAgentId;
    private bool preferredAgentApplied;
    private long? busyGeneration;
    private long? previewGeneration;
    private long? previewBusyGeneration;
    private long? curatorGeneration;
    private long? wizardGeneration;
    private AgentCapabilityPreview? preview;

    private AgentCapabilitiesSnapshot Snapshot => session.Snapshot with {
        IsBusy = busyGeneration == session.Generation,
        IsAccessPreviewBusy = previewBusyGeneration == session.Generation,
        IsOpeningCurator = curatorGeneration == session.Generation,
        IsOpeningWizard = wizardGeneration == session.Generation,
        Preview = previewGeneration == session.Generation ? preview : null
    };

    protected override void OnInitialized() => session = new(Reads);

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
        _ => throw new ArgumentOutOfRangeException(nameof(intent))
    };

    private async Task ToggleCapabilityAsync(Guid capabilityId) {
        if (session.Draft is not { } draft || Snapshot.IsBusy) {
            return;
        }

        var owner = session.Generation;
        busyGeneration = owner;
        try {
            var ids = draft.SelectedCapabilityIds.ToList();
            if (!ids.Remove(capabilityId)) {
                ids.Add(capabilityId);
            }

            draft.SelectedCapabilityIds = ids;
            await WorkspaceService.SaveAgentAsync(draft);
            if (session.IsCurrent(owner) && await RunReadAsync(session.RefreshAsync, next => {
                owner = next;
                busyGeneration = next;
            })) {
                NotificationService.Success("Ready", "Capability assignment updated.");
            }
        } catch (Exception exception) when (session.IsCurrent(owner)) {
            NotificationService.Error("Attention", exception.Message);
        } catch (Exception) when (!session.IsCurrent(owner)) {
        } finally {
            if (busyGeneration == owner) {
                busyGeneration = null;
            }
        }
    }

    private async Task VerifyCapabilityAsync(Guid capabilityId) {
        if (session.Selection.AgentId is not { } agentId || Snapshot.IsBusy) {
            return;
        }

        var owner = session.Generation;
        busyGeneration = owner;
        try {
            await WorkspaceService.VerifyCapabilityAsync(agentId, capabilityId);
            if (session.IsCurrent(owner) && await RunReadAsync(session.RefreshAsync, next => {
                owner = next;
                busyGeneration = next;
            })) {
                NotificationService.Success("Ready", "Capability verification completed.");
            }
        } catch (Exception exception) when (session.IsCurrent(owner)) {
            NotificationService.Error("Attention", exception.Message);
        } catch (Exception) when (!session.IsCurrent(owner)) {
        } finally {
            if (busyGeneration == owner) {
                busyGeneration = null;
            }
        }
    }

    private IReadOnlyList<string> AvailableCapabilityTags => session.Snapshot.Capabilities
        .SelectMany(capability => capability.Tags)
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private async Task OpenCapabilityDetailsDialogAsync(Guid capabilityId) {
        var owner = session.Generation;
        var capability = session.Snapshot.Capabilities.FirstOrDefault(item => item.Id == capabilityId);
        try {
            preview = null;
            var result = await DialogService.OpenAsync<CapabilityDetailsDialog>(
                capability?.Name ?? "Capability details",
                new Dictionary<string, object?> {
                    [nameof(CapabilityDetailsDialog.CapabilityId)] = capabilityId,
                    [nameof(CapabilityDetailsDialog.TagSuggestions)] = AvailableCapabilityTags
                },
                new DialogOptions {
                    Eyebrow = "Capability metadata",
                    Subtitle = "Inspect and edit capability tags, identity, and type-specific configuration.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Capability details",
                    TestId = "agents-capability-details-dialog"
                });
            if (session.IsCurrent(owner) && result is CapabilityDetailsDialogResult) {
                await RunReadAsync(session.RefreshAsync);
            }
        } catch (Exception exception) when (session.IsCurrent(owner)) {
            NotificationService.Error("Capability dialog failed", exception.Message);
        } catch (Exception) when (!session.IsCurrent(owner)) {
        }
    }

    private async Task OpenCapabilityWizardAsync(CapabilityKind initialKind) {
        if (Snapshot.IsBusy || Snapshot.IsOpeningWizard) {
            return;
        }

        var owner = session.Generation;
        wizardGeneration = owner;
        try {
            var result = await DialogService.OpenAsync<CapabilitySetupWizardDialog>(
                initialKind switch {
                    CapabilityKind.McpServer => "New MCP server",
                    CapabilityKind.Tool => "New tool",
                    _ => "New skill"
                },
                new Dictionary<string, object?> {
                    [nameof(CapabilitySetupWizardDialog.InitialKind)] = initialKind,
                    [nameof(CapabilitySetupWizardDialog.TagSuggestions)] = AvailableCapabilityTags
                },
                new DialogOptions {
                    Eyebrow = "Capability setup",
                    Subtitle = "Create a skill, tool, or MCP capability for assignment to technical agents.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Capability setup wizard",
                    TestId = "agents-capability-setup-dialog"
                });
            if (session.IsCurrent(owner) && result is CapabilityDetailsDialogResult &&
                await RunReadAsync(session.RefreshAsync, next => {
                    owner = next;
                    wizardGeneration = next;
                })) {
                NotificationService.Success("Ready", "Capability created.");
            }
        } catch (Exception exception) when (session.IsCurrent(owner)) {
            NotificationService.Error("Capability wizard failed", exception.Message);
        } catch (Exception) when (!session.IsCurrent(owner)) {
        } finally {
            if (wizardGeneration == owner) {
                wizardGeneration = null;
            }
        }
    }

    private async Task OpenCapabilityCuratorAsync() {
        if (!Snapshot.Curator.CanLaunch || Snapshot.IsBusy || Snapshot.IsOpeningCurator) {
            return;
        }

        var owner = session.Generation;
        curatorGeneration = owner;
        try {
            await AgentChatLauncher.StartNewChatAsync(CapabilityCuratorAgentIdentity.AgentId);
            if (session.IsCurrent(owner)) {
                NotificationService.Success("Capability Curator ready", "Opened a new managed capability chat.");
            }
        } catch (Exception exception) when (session.IsCurrent(owner)) {
            NotificationService.Error("Unable to open Capability Curator", exception.Message);
        } catch (Exception) when (!session.IsCurrent(owner)) {
        } finally {
            if (curatorGeneration == owner) {
                curatorGeneration = null;
            }
        }
    }

    private async Task PreviewAccessAsync(AgentCapabilityAccessDraft draft) {
        if (Snapshot.IsAccessPreviewBusy) {
            return;
        }

        var owner = session.Generation;
        previewBusyGeneration = owner;
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
            });
            if (!session.IsCurrent(owner)) {
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
        } catch (Exception exception) when (session.IsCurrent(owner)) {
            NotificationService.Error("Access preview failed", exception.Message);
        } catch (Exception) when (!session.IsCurrent(owner)) {
        } finally {
            if (previewBusyGeneration == owner) {
                previewBusyGeneration = null;
            }
        }
    }

    private static string ProtocolToken<T>(T value) where T : struct, Enum {
        if (!Enum.IsDefined(value)) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
    }

    public void Dispose() => session.Dispose();
}
