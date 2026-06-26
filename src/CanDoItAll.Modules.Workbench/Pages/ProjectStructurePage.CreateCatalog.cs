using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private IReadOnlyList<ProjectStructureNodeFact> SelectedNodeFacts
        => selectedNode is null
            ? []
            : ProjectStructureNodeDescriptor.BuildFacts(selectedNode);

    private string SelectedNodeLeadText
        => selectedNode is null
            ? string.Empty
            : ResolveSelectedNodeLeadText(selectedNode);

    private IReadOnlyList<ProjectStructureInspectorCreateGroup> HydrateCreateGroups(IReadOnlyList<ProjectStructureInspectorCreateGroup> groups)
        => groups.Select(HydrateCreateGroup).ToList();

    private ProjectStructureInspectorCreateGroup HydrateCreateGroup(ProjectStructureInspectorCreateGroup group)
        => group with
        {
            Actions = group.Actions.Select(HydrateCreateAction).ToList()
        };

    private CanvasWorkbenchAction HydrateCreateAction(CanvasWorkbenchAction action)
    {
        var useSecretReferenceDialog = IsSecretReferenceCreateAction(action.ActionId);
        return new CanvasWorkbenchAction
        {
            ActionId = action.ActionId,
            Label = action.Label,
            Description = action.Description,
            Icon = action.Icon,
            MenuLabel = action.MenuLabel,
            MenuSize = action.MenuSize,
            SubmenuLayout = action.SubmenuLayout,
            Tone = action.Tone,
            RequiresInput = useSecretReferenceDialog ? false : action.RequiresInput,
            CreateMode = useSecretReferenceDialog ? "secret-reference-picker" : action.CreateMode,
            ObjectSubtype = action.ObjectSubtype,
            TitleLabel = action.TitleLabel,
            TitlePlaceholder = action.TitlePlaceholder,
            SubtitleLabel = action.SubtitleLabel,
            SubtitlePlaceholder = action.SubtitlePlaceholder,
            NotesLabel = action.NotesLabel,
            NotesPlaceholder = action.NotesPlaceholder,
            ShowDefaultTextFields = useSecretReferenceDialog ? false : action.ShowDefaultTextFields,
            SubmitLabel = action.SubmitLabel,
            RequiresFile = useSecretReferenceDialog ? false : action.RequiresFile,
            AcceptedFileTypes = useSecretReferenceDialog ? string.Empty : action.AcceptedFileTypes,
            FilePrompt = useSecretReferenceDialog ? string.Empty : action.FilePrompt,
            SupportsDragDrop = useSecretReferenceDialog ? false : action.SupportsDragDrop,
            InputFields = useSecretReferenceDialog ? [] : action.InputFields.Select(HydrateInputField).ToList(),
            DefaultInputValues = action.DefaultInputValues
                .Select(value => new CanvasWorkbenchInputValue
                {
                    Key = value.Key,
                    Value = value.Value
                })
                .ToList(),
            Children = action.Children.Select(HydrateCreateAction).ToList()
        };
    }

    private CanvasWorkbenchInputField HydrateInputField(CanvasWorkbenchInputField field)
    {
        var options = ResolveDynamicOptions(field.Key);
        return new CanvasWorkbenchInputField
        {
            Key = field.Key,
            Label = field.Label,
            Placeholder = field.Placeholder,
            InputMode = field.InputMode,
            IsRequired = field.IsRequired,
            Options = (options ?? field.Options)
                .Select(option => new CanvasWorkbenchInputOption
                {
                    Value = option.Value,
                    Label = option.Label
                })
                .ToList()
        };
    }

    private IReadOnlyList<CanvasWorkbenchInputOption>? ResolveDynamicOptions(string key)
        => key switch
        {
            "participantRef" or "assigneeRef" or "parentParticipantRef" => BuildNodeOptions(ProjectObjectType.Participant),
            "repositoryRef" => BuildNodeOptions(ProjectObjectType.Repository),
            "meetingRef" => BuildNodeOptions(ProjectObjectType.Meeting),
            "recordingRef" => BuildNodeOptions(ProjectObjectType.Recording),
            "secretRef" => BuildNodeOptions(ProjectObjectType.SecretReference),
            "storageCatalogId" => BuildStorageCatalogOptions(),
            ProjectStructureCanvasCatalog.ImageProviderProfileFieldKey => BuildImageGenerationProviderOptions(),
            _ => null
        };

    private static bool IsSecretReferenceCreateAction(string? actionId)
        => string.Equals(actionId, "add-secret-reference", StringComparison.Ordinal);

    private IReadOnlyList<CanvasWorkbenchInputOption> BuildNodeOptions(params ProjectObjectType[] objectTypes)
    {
        if (surface is null || objectTypes.Length == 0)
        {
            return [];
        }

        var allowed = objectTypes.ToHashSet();
        return surface.Nodes
            .Where(node => allowed.Contains(node.ObjectType))
            .OrderBy(node => ProjectStructureCanvasCatalog.ResolveNodeLabel(node), StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
            .Select(node => new CanvasWorkbenchInputOption
            {
                Value = node.Id,
                Label = $"{node.Title} ({ProjectStructureCanvasCatalog.ResolveNodeLabel(node)})"
            })
            .DistinctBy(option => option.Value)
            .ToList();
    }

    private static string ResolveSelectedNodeLeadText(ProjectStructureNode node)
    {
        var candidates =
            new[]
            {
                node.Subtitle,
                ProjectStructureNodeDescriptor.BuildLeadText(node),
                node.Notes,
                node.Status
            };

        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
