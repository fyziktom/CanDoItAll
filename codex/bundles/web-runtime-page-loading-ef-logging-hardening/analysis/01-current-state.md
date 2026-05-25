# Current State

## Processes

`ProcessWorkspace.LoadWorkspaceAsync` eagerly loads definition data plus runtime overview, executor options, workflow definition options, manager-agent options, analytics, improvements, and party options during page initialization. Runtime pane loading is already partially guarded by `ShouldLoadRuntimePaneData`, but several option and analytics calls still happen before their tabs or dialogs are opened.

Role template options are obtained through `ProcessTemplateLibraryService.ListItems`, which loads the process template pack. This is acceptable when the role dialog or template library is opened, but not as part of ordinary workspace initialization.

## Project Structure

`ProjectStructurePage.CreateObjectAsync` persists the created node, optional links, optional follow-up move requests, and then calls `ReloadSurfaceAsync(created.Id)`. That reload fetches the complete structure and storage catalog before the new node is shown, which creates user-visible delay after add-node operations.

The page already has local-patch patterns for inline node updates through `ApplySurfaceNodeUpdatesAsync`; the create path should use the same model for the normal existing-surface case.

## Workflows

`WorkflowsPage.OnInitializedAsync` calls `ExampleCatalogSeedService.EnsureSeededAsync` before refreshing the page. `WorkflowExampleCatalogSeedService.EnsureSeededAsync` can load the template pack, provider options, components, and definitions, then save or inspect templates. This makes ordinary page navigation pay template/catalog costs even when the user is not opening templates.

`WorkflowsPage.LoadPageAsync` also eagerly loads components and provider options. Those collections are only required by the editor, templates, analytics, and starter workflow paths.

## EF Logging

`DatabaseOptions` only contains provider and connection string settings. The web host does not currently expose a strongly typed EF console logging switch with a default-off policy.
