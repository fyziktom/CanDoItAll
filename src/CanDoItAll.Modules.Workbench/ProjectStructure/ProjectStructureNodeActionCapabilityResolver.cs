using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureNodeActionCapabilityResolver
{
    public static ProjectStructureNodeActionCapabilities? Resolve(
        ProjectStructureNode node,
        IProjectStructureRuntimeLauncher runtimeLauncher,
        IProjectStructureLocalFileOpener localFileOpener)
    {
        var actions = new List<ProjectStructureNodeActionDescriptor>();
        var guidance = new List<string>();
        var runtimeResolution = runtimeLauncher.Resolve(node);
        var canLaunchRuntime = runtimeLauncher.IsAvailable && runtimeResolution.IsSuccess && runtimeResolution.Plan is not null;
        var canOpenInFileExplorer = localFileOpener.IsAvailable && localFileOpener.CanOpen(node);
        var canOpenInNewTab = IsIpfsBackedNode(node) && CanOpenNodeInNewTab(node);
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
            guidance.Add("Runtime nodes always expose both normal and administrator run actions when the launch plan resolves on this host.");
            guidance.Add("Runtime launch uses PowerShell in the resolved working directory and accepts workspace paths or explicit existing local drive paths.");

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
                Actions: BuildFileActions(actions, guidance, canOpenInFileExplorer, canOpenInNewTab),
                Guidance: guidance);
        }

        BuildFileActions(actions, guidance, canOpenInFileExplorer, canOpenInNewTab);
        if (actions.Count == 0 && string.IsNullOrWhiteSpace(storage.Provider))
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

    private static IReadOnlyList<ProjectStructureNodeActionDescriptor> BuildFileActions(
        List<ProjectStructureNodeActionDescriptor> actions,
        List<string> guidance,
        bool canOpenInFileExplorer,
        bool canOpenInNewTab)
    {
        if (canOpenInFileExplorer)
        {
            actions.Add(new ProjectStructureNodeActionDescriptor(
                "open-local",
                "Open in File Explorer",
                "Double-click quick-action dialog and node context menu",
                "Opens the trusted managed file, local file location, or folder in the system File Explorer."));
            guidance.Add("File Explorer launch is available for managed filesystem files plus explicit existing local file, repository folder, and infrastructure folder paths.");
            guidance.Add("The workbench opener blocks executable and script-like file extensions before launching File Explorer.");
        }

        if (canOpenInNewTab)
        {
            actions.Add(new ProjectStructureNodeActionDescriptor(
                "open-new-tab",
                "Open in New Tab",
                "Double-click quick-action dialog and node context menu",
                "Opens the IPFS-backed file route in a separate browser tab."));
            guidance.Add("IPFS-backed file nodes open in a browser tab instead of system File Explorer.");
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
