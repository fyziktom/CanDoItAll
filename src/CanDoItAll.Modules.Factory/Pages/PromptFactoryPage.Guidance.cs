using System.Text.Json;
using CanDoItAll.ComponentKit.Components;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private const string SupportLaneTabCanvas = "canvas";
    private const string SupportLaneTabSetup = "setup";
    private const string SupportLaneTabGovernance = "governance";
    private const string SupportLaneTabAssembly = "assembly";
    private const string SupportLaneTabReview = "review";
    private const string SetupAttachmentKind = "setup-profile";
    private const string SetupCanvasNodeId = "selection:setup";
    private const int BulkSelectionConfirmationThreshold = 8;
    private static readonly IReadOnlyList<string> SetupIntentOptions =
    [
        "Programming",
        "Business",
        "Marketing",
        "Product",
        "Research",
        "Operations",
        "Support",
        "Mixed / other"
    ];

    private static readonly IReadOnlyList<string> SetupProgrammingLanguageOptions =
    [
        "C#",
        "TypeScript",
        "JavaScript",
        "Python",
        "Java",
        "Go",
        "Rust",
        "PHP",
        "SQL",
        "Other"
    ];

    private static readonly IReadOnlyList<string> SetupApplicationStateOptions =
    [
        "Existing app",
        "New app",
        "Existing module inside active app",
        "Greenfield exploration",
        "Migration or refactor"
    ];

    private static readonly JsonSerializerOptions SetupProfileSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private string supportLaneTab = SupportLaneTabCanvas;
    private PromptSessionSetupProfile sessionSetup = new();
    private ProjectEditorModel? selectedProjectDetails;
    private bool showImpactDialog;
    private string impactDialogTitle = string.Empty;
    private string impactDialogBody = string.Empty;
    private string impactDialogConfirmLabel = "Continue";
    private Func<Task>? pendingImpactAction;

    private IReadOnlyList<PromptSessionAttachmentSummary> VisibleSessionAttachments
        => editor.SessionAttachments
            .Where(item => !IsSetupAttachment(item))
            .ToList();

    private PromptSessionAttachmentSummary? SetupAttachment
        => editor.SessionAttachments.FirstOrDefault(IsSetupAttachment);

    private int MissingSetupFieldCount => CountMissingSetupFields(sessionSetup);

    private bool SetupIsReady => MissingSetupFieldCount == 0;

    private bool ShowProgrammingLanguageField
        => string.Equals(sessionSetup.IntentCategory, "Programming", StringComparison.OrdinalIgnoreCase)
           || string.Equals(sessionSetup.IntentCategory, "Mixed / other", StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<SecondaryTabItem> SupportLaneTabs =>
    [
        new(SupportLaneTabCanvas, "Canvas", null, "Keep the lower lane closed while the canvas and floating inspector stay primary."),
        new(SupportLaneTabSetup, "Setup", SetupIsReady ? "Ready" : MissingSetupFieldCount.ToString(), "Frame the prompt intent, language, app state, and repositories."),
        new(SupportLaneTabGovernance, "Governance", editor.SelectedBlockIds.Count.ToString(), "Curate the working prompt-component set."),
        new(SupportLaneTabAssembly, "Assembly", $"{VisibleSessionAttachments.Count} + {editor.SelectedResourceIds.Count}", "Manage prompt inputs, files, and resources."),
        new(SupportLaneTabReview, "Review", editor.Warnings.Count.ToString(), "Check readiness, inspect prompt output, and prepare delivery.")
    ];

    private string ResolveSetupStatusLabel()
        => SetupIsReady
            ? "Setup ready"
            : $"{MissingSetupFieldCount} field(s) still missing";

    private string ResolveSetupSummaryLine()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sessionSetup.IntentCategory))
        {
            parts.Add(sessionSetup.IntentCategory);
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup.MainLanguage))
        {
            parts.Add(sessionSetup.MainLanguage);
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup.ApplicationState))
        {
            parts.Add(sessionSetup.ApplicationState);
        }

        return parts.Count == 0 ? "Choose the prompt intent and working frame." : string.Join(" | ", parts);
    }

    private string ResolveSetupLeadCopy()
    {
        if (!string.IsNullOrWhiteSpace(sessionSetup.GuidanceNotes))
        {
            return sessionSetup.GuidanceNotes;
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup.ProjectSnapshot))
        {
            return sessionSetup.ProjectSnapshot;
        }

        return "Define the prompt category, technical stack, and working repositories so later prompt decisions stay grounded.";
    }

    private string ResolveSupportLaneTitle()
        => supportLaneTab switch
        {
            SupportLaneTabSetup => "Session setup",
            SupportLaneTabGovernance => "Governance workspace",
            SupportLaneTabAssembly => "Assembly workspace",
            SupportLaneTabReview => "Review workspace",
            _ => "Prompt flow canvas"
        };

    private string ResolveSupportLaneDescription()
        => supportLaneTab switch
        {
            SupportLaneTabSetup => "Define the prompt intent, stack, repository frame, and working context in a dedicated setup workspace.",
            SupportLaneTabGovernance => "Curate reusable prompt components in a focused governance workspace without keeping the canvas open.",
            SupportLaneTabAssembly => "Pack files, notes, and project resources into the current prompt session in one assembly workspace.",
            SupportLaneTabReview => "Check readiness, inspect the generated prompt, and finish delivery actions in the review workspace.",
            _ => "Use the canvas tab for graph editing. The floating inspector stays contextual to the selected node inside the canvas, while the later tabs replace the canvas with focused workspaces."
        };

    private bool IsSupportLaneTab(string value)
        => string.Equals(supportLaneTab, value, StringComparison.OrdinalIgnoreCase);

    private async Task SetSupportLaneTabAsync(string value)
    {
        supportLaneTab = NormalizeSupportLaneTab(value);
        switch (supportLaneTab)
        {
            case SupportLaneTabSetup:
                if (CurrentWizardStep != 0)
                {
                    await MoveToStepAsync(0);
                }

                break;
            case SupportLaneTabGovernance:
                if (CurrentWizardStep != 1)
                {
                    await MoveToStepAsync(1);
                }

                break;
            case SupportLaneTabAssembly:
                if (CurrentWizardStep != 2)
                {
                    await MoveToStepAsync(2);
                }

                break;
            case SupportLaneTabReview:
                if (CurrentWizardStep != 3)
                {
                    await MoveToStepAsync(3);
                }

                break;
        }

        if (string.Equals(supportLaneTab, SupportLaneTabCanvas, StringComparison.Ordinal))
        {
            RefreshCanvasSurface();
        }
    }

    private static string NormalizeSupportLaneTab(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            SupportLaneTabSetup => SupportLaneTabSetup,
            SupportLaneTabGovernance => SupportLaneTabGovernance,
            SupportLaneTabAssembly => SupportLaneTabAssembly,
            SupportLaneTabReview => SupportLaneTabReview,
            _ => SupportLaneTabCanvas
        };

    private async Task LoadProjectContextAsync()
    {
        selectedProjectDetails = editor.ProjectId.HasValue
            ? await ProjectsService.GetAsync(editor.ProjectId.Value)
            : null;

        sessionSetup = SetupAttachment is { MetadataJson.Length: > 0 } existingSetup
            ? ParseSetupProfile(existingSetup.MetadataJson)
            : new PromptSessionSetupProfile();

        PrefillSetupProfile();
        UpsertSetupAttachment();
    }

    private bool ShouldAutoFocusSetupNode()
    {
        var sessionLooksFresh =
            editor.Nodes.Count == 0 &&
            !editor.BlueprintId.HasValue &&
            !editor.FlowTemplateId.HasValue &&
            editor.SelectedBlockIds.Count == 0 &&
            VisibleSessionAttachments.Count == 0;

        return sessionLooksFresh && !SetupIsReady;
    }

    private void EnsureSetupFocusForNewSession()
    {
        if (!ShouldAutoFocusSetupNode())
        {
            return;
        }

        selectedCanvasNodeIds = [SetupCanvasNodeId];
        editor.SelectedNodeId = null;
        supportLaneTab = SupportLaneTabCanvas;
    }

    private void PrefillSetupProfile()
    {
        if (string.IsNullOrWhiteSpace(sessionSetup.WorkRepository) && !string.IsNullOrWhiteSpace(editor.RepositoryName))
        {
            sessionSetup.WorkRepository = editor.RepositoryName.Trim();
        }

        if (string.IsNullOrWhiteSpace(sessionSetup.ApplicationState) && editor.ProjectId.HasValue)
        {
            sessionSetup.ApplicationState = "Existing app";
        }

        if (selectedProjectDetails is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sessionSetup.ProjectSnapshot))
        {
            var summary = new List<string> { selectedProjectDetails.Name };
            if (!string.IsNullOrWhiteSpace(selectedProjectDetails.CurrentPhase))
            {
                summary.Add($"Phase: {selectedProjectDetails.CurrentPhase}");
            }

            if (!string.IsNullOrWhiteSpace(selectedProjectDetails.Description))
            {
                summary.Add(selectedProjectDetails.Description);
            }

            if (!string.IsNullOrWhiteSpace(selectedProjectDetails.Objective))
            {
                summary.Add($"Objective: {selectedProjectDetails.Objective}");
            }

            sessionSetup.ProjectSnapshot = string.Join(" | ", summary);
        }
    }

    private static PromptSessionSetupProfile ParseSetupProfile(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PromptSessionSetupProfile();
        }

        try
        {
            return JsonSerializer.Deserialize<PromptSessionSetupProfile>(json, SetupProfileSerializerOptions)
                ?? new PromptSessionSetupProfile();
        }
        catch
        {
            return new PromptSessionSetupProfile();
        }
    }

    private static bool IsSetupAttachment(PromptSessionAttachmentSummary? attachment)
        => attachment is not null
            && string.Equals(attachment.Kind, SetupAttachmentKind, StringComparison.OrdinalIgnoreCase);

    private void UpsertSetupAttachment()
    {
        var title = "Session setup";
        var subtitle = ResolveSetupStatusLabel();
        var notes = ResolveSetupLeadCopy();
        var metadataJson = JsonSerializer.Serialize(sessionSetup, SetupProfileSerializerOptions);

        var existing = SetupAttachment;
        if (existing is null)
        {
            editor.SessionAttachments.Add(new PromptSessionAttachmentSummary
            {
                Kind = SetupAttachmentKind,
                Title = title,
                Subtitle = subtitle,
                Notes = notes,
                MetadataJson = metadataJson
            });

            return;
        }

        existing.Title = title;
        existing.Subtitle = subtitle;
        existing.Notes = notes;
        existing.MetadataJson = metadataJson;
    }

    private async Task SaveSetupProfileAsync()
    {
        RememberHistoryCheckpoint();
        NormalizeSetupProfile();
        UpsertSetupAttachment();

        if (editor.SessionId.HasValue)
        {
            var result = await PromptFactoryService.SaveSessionStateAsync(editor);
            if (result.IsFailure)
            {
                SetError(result.Errors);
                return;
            }

            editor = result.Value!;
            await LoadResourcesAsync();
            await LoadProjectContextAsync();
            HydrateCanvasSelection(SetupCanvasNodeId);
        }

        RefreshCanvasSurface();
        SetMessage(SetupIsReady
            ? "Session setup saved."
            : $"Session setup saved with {MissingSetupFieldCount} field(s) still missing.");
    }

    private void NormalizeSetupProfile()
    {
        sessionSetup.IntentCategory = sessionSetup.IntentCategory?.Trim() ?? string.Empty;
        sessionSetup.MainLanguage = sessionSetup.MainLanguage?.Trim() ?? string.Empty;
        sessionSetup.SecondaryLanguages = sessionSetup.SecondaryLanguages?.Trim() ?? string.Empty;
        sessionSetup.ApplicationState = sessionSetup.ApplicationState?.Trim() ?? string.Empty;
        sessionSetup.WorkRepository = sessionSetup.WorkRepository?.Trim() ?? string.Empty;
        sessionSetup.SourceRepositories = sessionSetup.SourceRepositories?.Trim() ?? string.Empty;
        sessionSetup.GuidanceNotes = sessionSetup.GuidanceNotes?.Trim() ?? string.Empty;
        sessionSetup.ProjectSnapshot = sessionSetup.ProjectSnapshot?.Trim() ?? string.Empty;
    }

    private async Task SelectSetupNodeAsync(bool openSetupTab = false)
    {
        UpsertSetupAttachment();
        await SelectCanvasNodeAsync(SetupCanvasNodeId);
        if (openSetupTab)
        {
            await SetSupportLaneTabAsync(SupportLaneTabSetup);
        }
    }

    private static int CountMissingSetupFields(PromptSessionSetupProfile profile)
    {
        var missing = 0;
        if (string.IsNullOrWhiteSpace(profile.IntentCategory))
        {
            missing++;
        }

        if (string.Equals(profile.IntentCategory, "Programming", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(profile.MainLanguage))
        {
            missing++;
        }

        if (string.IsNullOrWhiteSpace(profile.ApplicationState))
        {
            missing++;
        }

        if (string.IsNullOrWhiteSpace(profile.WorkRepository))
        {
            missing++;
        }

        return missing;
    }

    private void OpenImpactDialog(string title, string body, string confirmLabel, Func<Task> confirmAction)
    {
        showImpactDialog = true;
        impactDialogTitle = title;
        impactDialogBody = body;
        impactDialogConfirmLabel = confirmLabel;
        pendingImpactAction = confirmAction;
    }

    private Task CloseImpactDialogAsync()
    {
        showImpactDialog = false;
        impactDialogTitle = string.Empty;
        impactDialogBody = string.Empty;
        impactDialogConfirmLabel = "Continue";
        pendingImpactAction = null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task ConfirmImpactDialogAsync()
    {
        var confirmedAction = pendingImpactAction;
        await CloseImpactDialogAsync();
        if (confirmedAction is not null)
        {
            await confirmedAction();
        }
    }

    private bool TryBuildSelectionImpactMessage(
        IReadOnlyList<Guid> incomingBlockIds,
        string actionLabel,
        out string dialogBody)
    {
        var existing = editor.SelectedBlockIds.ToHashSet();
        var proposed = incomingBlockIds.ToHashSet();
        var added = proposed.Except(existing).Count();
        var removed = existing.Except(proposed).Count();

        if (Math.Max(added, removed) < BulkSelectionConfirmationThreshold)
        {
            dialogBody = string.Empty;
            return false;
        }

        dialogBody = $"{actionLabel} will add {added} block(s) and remove {removed} block(s) from the current working set. Continue?";
        return true;
    }
}
