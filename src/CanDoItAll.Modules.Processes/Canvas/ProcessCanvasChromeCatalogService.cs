using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed record ProcessCanvasChromeCatalog(
    IReadOnlyList<CanvasWorkbenchAction> DefinitionQuickCreateActions,
    IReadOnlyList<CanvasWorkbenchAction> DefinitionGroupContextActions);

public sealed class ProcessCanvasChromeCatalogService
{
    private readonly ProcessTemplatePackLoader packLoader;

    public ProcessCanvasChromeCatalogService(ProcessTemplatePackLoader packLoader)
    {
        this.packLoader = packLoader;
    }

    public ProcessCanvasChromeCatalog GetDefinitionChrome()
    {
        var pack = packLoader.Load();
        var chromeCatalogPath = ResolveChromeCatalogPath(pack);
        return new ProcessCanvasChromeCatalog(
            ResolveActions(
                pack.ChromeActions.DefinitionQuickCreateActions,
                chromeCatalogPath,
                "DefinitionQuickCreateActions",
                ResolveDefinitionQuickCreateAction),
            ResolveActions(
                pack.ChromeActions.DefinitionGroupContextActions,
                chromeCatalogPath,
                "DefinitionGroupContextActions",
                ResolveDefinitionGroupContextAction));
    }

    private static IReadOnlyList<CanvasWorkbenchAction> ResolveActions(
        IReadOnlyList<string> actionIds,
        string chromeCatalogPath,
        string sectionName,
        Func<string, CanvasWorkbenchAction> resolver)
    {
        if (actionIds.Count == 0)
        {
            throw new InvalidOperationException($"Chrome action catalog section '{sectionName}' in '{chromeCatalogPath}' is empty.");
        }

        var orderedActions = new List<CanvasWorkbenchAction>(actionIds.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var actionId in actionIds)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new InvalidOperationException($"Chrome action catalog section '{sectionName}' in '{chromeCatalogPath}' contains a blank action id.");
            }

            if (!seen.Add(actionId))
            {
                throw new InvalidOperationException($"Chrome action catalog section '{sectionName}' in '{chromeCatalogPath}' contains duplicate action id '{actionId}'.");
            }

            orderedActions.Add(resolver(actionId));
        }

        return orderedActions;
    }

    private static CanvasWorkbenchAction ResolveDefinitionQuickCreateAction(string actionId)
    {
        return actionId switch
        {
            ProcessCanvasActionIds.OpenDefinitionToolbox => BuildAction(
                actionId,
                "Open toolbox",
                "Open toolbox",
                "Open the floating process templates toolbox.",
                "inventory_2",
                "neutral"),
            ProcessCanvasActionIds.CreateRoleProductOwner => BuildAction(
                actionId,
                "Product owner",
                "Add product owner role",
                "Seed the process with the accountable value owner for scope and outcome.",
                "person_add",
                "neutral"),
            ProcessCanvasActionIds.CreateRoleSolutionArchitect => BuildAction(
                actionId,
                "Architect role",
                "Add architect role",
                "Add the architecture authority role template for cross-boundary design decisions.",
                "architecture",
                "neutral"),
            ProcessCanvasActionIds.CreateStepIntake => BuildAction(
                actionId,
                "Intake step",
                "Add intake step",
                "Add a structured intake step that captures the scope boundary and dependencies.",
                "playlist_add_check",
                "accent"),
            ProcessCanvasActionIds.CreateStepArchitecture => BuildAction(
                actionId,
                "Architecture step",
                "Add architecture step",
                "Add a design review step with decision-record defaults.",
                "hub",
                "accent"),
            ProcessCanvasActionIds.CreateStepImplementation => BuildAction(
                actionId,
                "Implementation",
                "Add implementation step",
                "Add a standard execution step with proof-oriented defaults.",
                "add_circle",
                "accent"),
            ProcessCanvasActionIds.CreateStepQa => BuildAction(
                actionId,
                "QA gate",
                "Add QA gate",
                "Add a regression and release-confidence step.",
                "fact_check",
                "accent"),
            ProcessCanvasActionIds.CreateStepReleaseApproval => BuildAction(
                actionId,
                "Approval step",
                "Add approval step",
                "Add an explicit approval gate.",
                "check",
                "warn"),
            _ => throw CreateUnknownActionException("DefinitionQuickCreateActions", actionId)
        };
    }

    private static CanvasWorkbenchAction ResolveDefinitionGroupContextAction(string actionId)
    {
        return actionId switch
        {
            ProcessCanvasActionIds.EditDefinitionStep => BuildAction(
                actionId,
                "Edit step",
                "Edit step",
                "Edit the selected step in the floating inspector.",
                "draw",
                "accent"),
            ProcessCanvasActionIds.AddDependentStep => BuildAction(
                actionId,
                "Add step",
                "Add dependent step",
                "Add a step that depends on the current selection.",
                "add_circle",
                "accent"),
            ProcessCanvasActionIds.AddRoleBinding => BuildAction(
                actionId,
                "Add role binding",
                "Add role binding",
                "Attach a role contract to the selected step.",
                "people",
                "neutral"),
            ProcessCanvasActionIds.AddArtifactExpectation => BuildAction(
                actionId,
                "Add artifact expectation",
                "Add artifact expectation",
                "Attach a typed artifact contract to the selected step.",
                "description",
                "neutral"),
            ProcessCanvasActionIds.RemoveDefinitionStep => BuildAction(
                actionId,
                "Remove step",
                "Remove step",
                "Remove the selected step from the process map.",
                "delete",
                "danger"),
            _ => throw CreateUnknownActionException("DefinitionGroupContextActions", actionId)
        };
    }

    private static CanvasWorkbenchAction BuildAction(
        string actionId,
        string label,
        string menuLabel,
        string description,
        string icon,
        string tone)
    {
        return new CanvasWorkbenchAction
        {
            ActionId = actionId,
            Label = label,
            MenuLabel = menuLabel,
            Description = description,
            Icon = icon,
            Tone = tone
        };
    }

    private static InvalidOperationException CreateUnknownActionException(string sectionName, string actionId)
    {
        return new InvalidOperationException($"Chrome action catalog section '{sectionName}' contains unsupported action id '{actionId}'.");
    }

    private static string ResolveChromeCatalogPath(ProcessTemplatePack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.Manifest.Toolbox.ChromeActionsPath))
        {
            return Path.Combine(pack.RootPath, "toolbox", "chrome-actions.json");
        }

        return Path.GetFullPath(Path.Combine(pack.RootPath, pack.Manifest.Toolbox.ChromeActionsPath));
    }
}
