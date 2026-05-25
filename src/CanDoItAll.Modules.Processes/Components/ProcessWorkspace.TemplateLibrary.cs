using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    [Inject]
    private ProcessTemplateLibraryService ProcessTemplateLibraryService { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    private bool templateLibraryOpen;
    private Guid? templateLibraryArtifactTargetStepId;

    private string TemplateLibraryScopeLabel
        => ProjectId.HasValue && !string.IsNullOrWhiteSpace(projectName)
            ? projectName
            : "Global process library";

    private IReadOnlyList<ProcessTemplateArtifactTargetOption> TemplateArtifactTargets
        => editor.Steps
            .OrderBy(step => step.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(step => step.Key, StringComparer.OrdinalIgnoreCase)
            .Select(step => new ProcessTemplateArtifactTargetOption(
                step.Id ?? Guid.Empty,
                step.Key,
                string.IsNullOrWhiteSpace(step.Title)
                    ? step.Key
                    : step.Title))
            .Where(option => option.StepId != Guid.Empty)
            .ToList();

    private Task OpenTemplateLibraryAsync()
    {
        EnsureTemplateArtifactTargetSelection();
        templateLibraryOpen = true;
        return Task.CompletedTask;
    }

    private Task CloseTemplateLibraryAsync()
    {
        templateLibraryOpen = false;
        return Task.CompletedTask;
    }

    private void EnsureTemplateArtifactTargetSelection()
    {
        if (SelectedCanvasDefinitionStep?.Id is Guid selectedStepId)
        {
            templateLibraryArtifactTargetStepId = selectedStepId;
            return;
        }

        if (templateLibraryArtifactTargetStepId.HasValue &&
            editor.Steps.Any(step => step.Id == templateLibraryArtifactTargetStepId.Value))
        {
            return;
        }

        templateLibraryArtifactTargetStepId = editor.Steps
            .FirstOrDefault(step => step.Id.HasValue)?
            .Id;
    }

    private async Task AddProcessTemplateAsync(string itemId)
    {
        var preview = ProcessTemplateLibraryService.GetPreview(ProcessTemplateLibraryCategory.Processes, itemId);
        var envelope = ProcessTemplateLibraryService.CreateProcessImportEnvelope(itemId, ProjectId);
        var result = await ProcessesService.ImportAsync(envelope);
        if (result.IsFailure)
        {
            NotifyTemplateImportFailure("Process import failed.", result.Errors.Select(error => error.Message));
            return;
        }

        definitions = await ProcessesService.ListDefinitionsAsync(ProjectId);
        await EnsureAnalyticsLoadedAsync(forceRefresh: true);
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Success,
            Summary = "Process imported",
            Detail = $"{preview.Title} was added to your process library.",
            Duration = 3200
        });

        await InvokeAsync(StateHasChanged);
    }

    private async Task AddRoleTemplateAsync(string itemId)
    {
        var preview = ProcessTemplateLibraryService.GetPreview(ProcessTemplateLibraryCategory.Roles, itemId);
        var draft = CreateUniqueRoleDraftFromTemplate(itemId, excludedRole: null);
        templateLibraryOpen = false;
        await EnsureWorkflowOptionsLoadedAsync();
        OpenRoleDialog(draft, target: null, isNew: true);

        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Info,
            Summary = "Role template loaded",
            Detail = $"{preview.Title} is ready to review. Save the role dialog to add it to the process draft.",
            Duration = 3200
        });

        await InvokeAsync(StateHasChanged);
    }

    private async Task AddArtifactTemplateAsync(string itemId)
    {
        var targetStep = ResolveTemplateArtifactTarget();
        if (targetStep is null)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = "Select a target step",
                Detail = "Artifact templates are step-scoped. Choose a process step before adding an artifact.",
                Duration = 3600
            });
            return;
        }

        var preview = ProcessTemplateLibraryService.GetPreview(ProcessTemplateLibraryCategory.Artifacts, itemId);
        var artifact = ProcessTemplateLibraryService.CreateArtifactExpectation(itemId);
        targetStep.ArtifactExpectations.Add(artifact);
        RefreshCanvasSurface();

        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Success,
            Summary = "Artifact added",
            Detail = $"{preview.Title} was added to {ResolveStepDisplayName(targetStep)}. Save the definition to persist the change.",
            Duration = 2800
        });

        await InvokeAsync(StateHasChanged);
    }

    private ProcessStepEditorModel? ResolveTemplateArtifactTarget()
    {
        EnsureTemplateArtifactTargetSelection();
        if (!templateLibraryArtifactTargetStepId.HasValue)
        {
            return null;
        }

        return editor.Steps.FirstOrDefault(step => step.Id == templateLibraryArtifactTargetStepId.Value);
    }

    private void NotifyTemplateImportFailure(string summary, IEnumerable<string> errors)
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = summary,
            Detail = string.Join(" ", errors),
            Duration = 4200
        });
    }

    private static string ResolveStepDisplayName(ProcessStepEditorModel step)
    {
        return string.IsNullOrWhiteSpace(step.Title)
            ? step.Key
            : step.Title;
    }
}
