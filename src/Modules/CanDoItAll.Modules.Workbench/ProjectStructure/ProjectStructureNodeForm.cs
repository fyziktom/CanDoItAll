using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureNodeForm(StructureNodeFormSchema schema, ProjectStructureCreateLeafDefinition? definition) {
    public StructureNodeFormSchema Schema { get; } = schema;

    public StructureNodeFormValues Values(ProjectStructureNode node) => Schema.Read(node.Title, node.Subtitle, node.Notes,
        definition is null ? [] : ProjectStructureNodeEditor.BuildInputValues(definition, node));

    public StructureNodeFormValues Read(CanvasWorkbenchComposerDraft draft, ProjectStructureNode baseline) {
        if (Schema.InlineNote && draft.EditRequest is { } edit && edit.NodeId == baseline.Id && draft.CreateRequest is null) {
            var title = edit.HasExplicitTitle ? edit.Title : edit.Notes == baseline.Notes
                ? baseline.Title : ProjectStructureNodeEditor.BuildSimpleNoteTitle(edit.Notes);
            return Schema.Read(title, baseline.Subtitle, edit.Notes, []);
        }
        if (!Schema.InlineNote && draft.CreateRequest is { PlacementKind: "edit" } create &&
            create.SourceNodeId == baseline.Id && draft.EditRequest is null) {
            return Schema.Read(Schema.ShowDefaultFields ? create.Title : baseline.Title,
                Schema.ShowDefaultFields ? create.Subtitle : baseline.Subtitle,
                Schema.ShowDefaultFields ? create.Notes : baseline.Notes, create.InputValues);
        }
        throw new ArgumentException("The original node editor shape changed. Reopen it without discarding the retained text.");
    }

    public bool MatchesOpening(CanvasWorkbenchComposerOpening opening, ProjectStructureNode baseline) {
        if (Schema.InlineNote) {
            return opening.EditRequest?.NodeId == baseline.Id && opening.EditRequest.Notes == baseline.Notes;
        }
        var original = Values(baseline);
        var opened = Read(new(opening.OpeningId, 0, false, true, opening.CreateRequest, opening.EditRequest), baseline);
        return original.Title == opened.Title && original.Subtitle == opened.Subtitle && original.Notes == opened.Notes &&
            original.Fields.All(field => Equals(field.Value, opened.Fields[field.Key]));
    }

    public CanvasWorkbenchComposerApplyRequest Patch(CanvasWorkbenchComposerDraft draft, StructureNodeFormValues values) =>
        new(draft.OpeningId, draft.Revision,
            draft.CreateRequest is { } create ? create with {
                Title = Schema.ShowDefaultFields ? values.Title : create.Title,
                Subtitle = Schema.ShowDefaultFields ? values.Subtitle : create.Subtitle,
                Notes = Schema.ShowDefaultFields ? values.Notes : create.Notes,
                InputValues = Schema.Write(values)
            } : null,
            draft.EditRequest is { } edit ? edit with { Title = values.Title, Notes = values.Notes, HasExplicitTitle = true } : null);

    public ProjectObjectEditRequest Compose(ProjectStructureNode baseline, CanvasWorkbenchCreateActionRequest submitted) {
        if (definition is null) {
            throw new InvalidOperationException("An inline note does not have a full node form.");
        }
        var original = Values(baseline);
        var local = Schema.Read(Schema.ShowDefaultFields ? submitted.Title : baseline.Title,
            Schema.ShowDefaultFields ? submitted.Subtitle : baseline.Subtitle,
            Schema.ShowDefaultFields ? submitted.Notes : baseline.Notes, submitted.InputValues);
        return ProjectStructureNodeEditor.ComposeUpdate(definition, baseline, submitted with {
            Title = local.Title, Subtitle = local.Subtitle, Notes = local.Notes,
            InputValues = Schema.Write(local).Where(value => !Equals(original.Fields[new(value.Key)], local.Fields[new(value.Key)])).ToArray()
        });
    }
}
