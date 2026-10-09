using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.UI;

public static class ProcessAuthoringReadReconciler {
    public static (ProcessDefinitionEditorProjection Editor, bool Stale) Reconcile(
        ProcessDefinitionEditorProjection current, ProcessDefinitionEditorProjection observed) {
        var known = current.Observation ?? throw new ArgumentException("The current native authoring observation is required.");
        if (observed.Observation is not { } incoming || incoming != known && !incoming.Supersedes(known)) {
            return (current, true);
        }
        return (PreserveReceipts(current, observed), false);
    }

    public static ProcessDefinitionEditorProjection PreserveReceipts(ProcessDefinitionEditorProjection current, ProcessDefinitionEditorProjection observed) {
        var roles = observed.RoleEditor ?? (current.RoleEditor?.Observation == observed.Observation ? current.RoleEditor : null);
        var steps = observed.StepEditor ?? (current.StepEditor?.Observation == observed.Observation ? current.StepEditor : null);
        var canvas = observed.Canvas ?? (current.Canvas?.Observation == observed.Observation ? current.Canvas : null);
        var templates = observed.TemplateCatalog ?? (current.TemplateCatalog?.Observation == observed.Observation ? current.TemplateCatalog : null);
        return observed with {
            LastCommandReceipt = observed.LastCommandReceipt ?? current.LastCommandReceipt,
            RoleEditor = roles is null ? null : roles with { LastCommandReceipt = roles.LastCommandReceipt ?? current.RoleEditor?.LastCommandReceipt },
            StepEditor = steps is null ? null : steps with { LastCommandReceipt = steps.LastCommandReceipt ?? current.StepEditor?.LastCommandReceipt },
            Canvas = canvas is null ? null : canvas with { LastCommandReceipt = canvas.LastCommandReceipt ?? current.Canvas?.LastCommandReceipt },
            TemplateCatalog = templates is null ? null : templates with { LastImportReceipt = templates.LastImportReceipt ?? current.TemplateCatalog?.LastImportReceipt }
        };
    }
}
