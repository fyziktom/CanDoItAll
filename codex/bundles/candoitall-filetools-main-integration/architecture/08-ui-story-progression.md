# UI Story Progression

## Shared Rules

- Run Components MCP library/recommend/component/usage/example discovery before choosing wrappers or CSS. Current transport failure is not permission to invent structure.
- Use existing BaseLib/CanvasLib/Radzen only where present. Prefer `PageScaffold`, tabs, dialog, empty/error/loading/status, stack/grid/cluster, toolbar, and `CanvasFloatingWindow` wrappers over raw structural markup.
- Parent pages hold only minimal state and callbacks. Session/source/content/save lifetimes live in focused coordinators/components and are disposed predictably.
- Primary viewport `1900x1200`; regression `1440x900`. Do not implement/test small, medium, tablet, or mobile.
- Choose the entry contract by semantic intent: known file -> direct FileInteraction; collection/container discovery -> FileBrowser. Never initialize both merely to show one file.
- UI state and rendered rows remain bounded to approved pages/session limits; cancellation is latest-request-wins.

## Pilot: SB10

One project, one authorized source set, project-files search, browse/navigation, and activation of one known Markdown/text file into read-only FileInteraction. Browsing owns the collection; after activation/reauthorization the resulting known-file interaction is session-independent. Prove bounded large-result behavior, loading, result, no-result, error/retry, unauthorized item, stale item, file replacement, open/close/disposal, keyboard activation, console/network, and inspected screenshots. No portfolio aggregate, canvas window, process run, resource promotion, or editing.

## Story 2: Projects Portfolio/Card

Extract a pure shared project filter/hierarchy projection. Cards and Files tab consume it. Add a focused project-card Files action/dialog. Source replacement uses ordered project IDs, hierarchy/include state, binding revisions, and catalog revision; an invalid current location is not retained.

## Story 3: Project Structure

Preserve current asset-node open/double-click behavior: images and PDFs open the existing focused dialog with direct FileInteraction and zero FileBrowser calls. Separately add a focused `ProjectStructureFileBrowserWindow` for toolbar/context actions whose intent is to browse a project/node collection, using explicit Compact/Minimal mode inside existing `CanvasFloatingWindow`. One results scroll owner; window chrome stays visible; menus/popovers are opened and checked for clipping/layering. Project aggregate and authorized supported node scopes are separate. `open-local` remains distinct.

## Story 4: Process Runs

Processes owns run-root policy and provider. Focused dialog browses managed/output/product roots with host/session cache Disabled. Dashboard owns only open/close/run ID. Mutation after open/refresh is visible.

## Story 5: Resources

Registry/Browse split uses a focused source catalog over authorized project/filesystem/IPFS/FTP sources. “Add as resource” is a host command that re-resolves/re-authorizes and persists stable binding/object locator through a generic storage-object connector. It never persists display path/handle as authority.

## Story 6: FileInteraction Migration

Incrementally replace known Workbench preview/edit flows. Start by characterizing image/PDF direct dialog behavior, then use direct FileInteraction without FileBrowser. Continue with existing built-ins/Markdown, register a Mermaid adapter around the retained Components wrapper, and add editor/save only for proven types. Preserve old path until each type passes positive, hostile-content, replacement, save/conflict, zero-browser-call, and browser proof; then delete duplicate logic.

## Visual Questions

For every UI pass record: Is the main task obvious? Are controls readable and aligned? Does exactly the intended region scroll? Are content/menus/dialogs/windows unclipped and above neighboring chrome? Are loading/empty/error/busy/selected/dirty/conflict states distinct? Is space used well at desktop? Are console/page/network failures absent or explained?
