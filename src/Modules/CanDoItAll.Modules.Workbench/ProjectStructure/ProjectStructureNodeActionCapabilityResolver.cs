using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureNodeActionCapabilityResolver
{
    public static ProjectStructureNodeActionCapabilities? Resolve(
        ProjectStructureNode node,
        IProjectStructureRuntimeLauncher runtimeLauncher,
        IProjectStructureLocalFileOpener localFileOpener,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        var actions = new List<ProjectStructureNodeActionDescriptor>();
        var guidance = new List<string>();
        var isRuntimeCapable = IsRuntimeCapable(node);
        var runtimeResolution = isRuntimeCapable
            ? runtimeLauncher.Resolve(
                node.ObjectType,
                node.ObjectSubtype,
                node.Notes,
                node.MetadataJson,
                pathAuthorityMode)
            : new ProjectStructureRuntimeLaunchResolution(null, string.Empty);
        var canLaunchRuntime = isRuntimeCapable &&
                               runtimeLauncher.IsAvailable &&
                               runtimeResolution.IsSuccess &&
                               runtimeResolution.Plan is not null;
        var canOpenInFileExplorer = localFileOpener.IsAvailable && localFileOpener.CanOpen(node);
        var canOpenInNewTab = IsIpfsBackedNode(node) && CanOpenNodeInNewTab(node);
        var canBrowseFiles = ProjectStructureFileActions.CanBrowseFiles(node);
        var storage = ResolveStorage(node);
        if (canLaunchRuntime && runtimeResolution.Plan is { } runtimePlan)
        {
            actions.Add(new ProjectStructureNodeActionDescriptor(
                "runtime:open",
                "Run normally",
                "Double-click quick-action dialog and node context menu",
                "Launches the resolved workspace command in a normal PowerShell window."));
            actions.Add(new ProjectStructureNodeActionDescriptor(
                "runtime:admin",
                "Run as administrator",
                "Double-click quick-action dialog and node context menu",
                "Launches the same resolved workspace command in an elevated PowerShell window."));
            guidance.Add("Runtime nodes expose normal and administrator shell-handoff actions only when the launch plan and target resolve on this host.");
            guidance.Add("A shell handoff is not proof that the application started; verify the terminal output after launch.");

            return new ProjectStructureNodeActionCapabilities(
                CanRunNormally: true,
                CanRunAsAdministrator: true,
                CanOpenInFileExplorer: canOpenInFileExplorer,
                CanOpenInNewTab: canOpenInNewTab,
                RuntimeDisplayName: runtimePlan.DisplayName,
                RuntimeDisplayCommand: runtimePlan.DisplayCommand,
                RuntimeWorkingDirectory: runtimePlan.WorkingDirectory,
                OpenInNewTabRoute: canOpenInNewTab ? node.Route : string.Empty,
                StorageProvider: storage.Provider,
                StorageLocatorKind: storage.LocatorKind,
                StorageLocator: storage.Locator,
                Actions: BuildFileActions(actions, guidance, canOpenInFileExplorer, canOpenInNewTab, canBrowseFiles),
                Guidance: guidance);
        }

        BuildFileActions(actions, guidance, canOpenInFileExplorer, canOpenInNewTab, canBrowseFiles);
        if (isRuntimeCapable)
        {
            guidance.Add($"Runtime launch is unavailable: {runtimeResolution.Message}");
        }

        if (actions.Count == 0 &&
            string.IsNullOrWhiteSpace(storage.Provider) &&
            !isRuntimeCapable)
        {
            return null;
        }

        return new ProjectStructureNodeActionCapabilities(
            CanRunNormally: false,
            CanRunAsAdministrator: false,
            CanOpenInFileExplorer: canOpenInFileExplorer,
            CanOpenInNewTab: canOpenInNewTab,
            RuntimeDisplayName: string.Empty,
            RuntimeDisplayCommand: string.Empty,
            RuntimeWorkingDirectory: string.Empty,
            OpenInNewTabRoute: canOpenInNewTab ? node.Route : string.Empty,
            StorageProvider: storage.Provider,
            StorageLocatorKind: storage.LocatorKind,
            StorageLocator: storage.Locator,
            Actions: actions,
            Guidance: guidance);
    }

    private static bool IsRuntimeCapable(ProjectStructureNode node)
        => node.ObjectType switch
        {
            ProjectObjectType.Script or ProjectObjectType.Environment => true,
            ProjectObjectType.Infrastructure =>
                ProjectNodeKindRegistry.ResolveInfrastructureKind(node.ObjectSubtype) ==
                ProjectInfrastructureKind.DockerMode,
            _ => false
        };

    private static IReadOnlyList<ProjectStructureNodeActionDescriptor> BuildFileActions(
        List<ProjectStructureNodeActionDescriptor> actions,
        List<string> guidance,
        bool canOpenInFileExplorer,
        bool canOpenInNewTab,
        bool canBrowseFiles)
    {
        if (canBrowseFiles)
        {
            actions.Add(ProjectStructureFileActions.CreateDescriptor());
            guidance.Add("Collection browsing uses the authorized canvas file window and remains separate from direct asset preview and local folder launch.");
        }

        if (canOpenInFileExplorer)
        {
            actions.Add(new ProjectStructureNodeActionDescriptor(
                "open-local",
                "Show in folder",
                "Double-click quick-action dialog and node context menu",
                "Opens the trusted file location in the system file browser."));
            guidance.Add("Local folder launch is available only for existing files and folders inside the configured workspace root.");
        }

        if (canOpenInNewTab)
        {
            actions.Add(new ProjectStructureNodeActionDescriptor(
                "open-new-tab",
                "Open in New Tab",
                "Double-click quick-action dialog and node context menu",
                "Opens the IPFS-backed file route in a separate browser tab."));
            guidance.Add("IPFS-backed file nodes open in a browser tab instead of the system file browser.");
        }

        return actions;
    }

    private static bool CanOpenNodeInNewTab(ProjectStructureNode node)
        => !string.IsNullOrWhiteSpace(node.Route) &&
           !node.Route.EndsWith("/structure", StringComparison.OrdinalIgnoreCase);

    private static bool IsIpfsBackedNode(ProjectStructureNode node)
    {
        if (StorageJson.TryParseReference(node.StorageObjectReferenceJson, out var storageReference) &&
            storageReference is not null &&
            storageReference.ProviderKind == StorageProviderKind.Ipfs)
        {
            return true;
        }

        return node.Route.Contains("/ipfs/", StringComparison.OrdinalIgnoreCase) ||
               (Uri.TryCreate(node.Route, UriKind.Absolute, out var routeUri) &&
                routeUri.Host.Contains("ipfs", StringComparison.OrdinalIgnoreCase));
    }

    private static (string Provider, string LocatorKind, string Locator) ResolveStorage(ProjectStructureNode node)
    {
        if (!StorageJson.TryParseReference(node.StorageObjectReferenceJson, out var storageReference) ||
            storageReference is null)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        return (
            storageReference.ProviderKind.ToString(),
            storageReference.LocatorKind.ToString(),
            storageReference.Locator);
    }
}
