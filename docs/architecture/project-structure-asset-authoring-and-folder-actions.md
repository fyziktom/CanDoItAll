# Project Structure asset authoring and folder actions

## Scope and evidence

This gate covers two related Project Structure interaction defects:

- projected process-run output folders do not expose the authorized file browser or a trusted desktop-folder action;
- text-based asset actions either persist content only as node notes or require an existing upload, so they cannot author a new stored file.

The scoped evidence pass used direct source and test inspection because the CodeAnalytics and shared-components MCP tools were unavailable in this session. The existing FileTools browser, storage-placement pipeline, BaseLib dialog, and BaseLib file-upload components remain the implementation authorities.

## Responsibility inventory

| Current owner | Current responsibility | Problem |
|---|---|---|
| `ProjectStructureProcessProjectionContributor` | Projects managed run-output folders | Emits an anonymous metadata shape and no file-collection capability contract. |
| `ProjectStructureFileActions` | Advertises collection browsing | Allows only projects and storage-backed infrastructure nodes. |
| `ProjectStructureFileScopeResolver` | Authorizes Project Structure FileTools scopes | Resolves projected known files, but not projected collections. |
| `ProjectStructureLocalFileOpener` | Resolves and launches trusted workspace files/folders | Cannot identify the anonymous process-run output metadata. |
| `ProjectStructurePage` | Dispatches canvas actions and default node activation | Falls back to a notes-oriented quick-action dialog for folders. |
| `ProjectStructureCanvasCatalog` | Describes create forms | Conflates upload availability with upload being mandatory. |
| `ProjectStructureCreateRequestComposer` | Maps submitted create forms to object requests | Creates media only when an existing file was uploaded. |
| `ProjectWorkbenchService` | Persists nodes and delegates media placement | Already provides the canonical path into storage placement and FileTools. |

## Target boundary map

```text
Folder projection
  -> typed File metadata + governed artifact identity
  -> node capability policy
  -> projected collection scope authorization
  -> FileTools browser / trusted desktop launcher

Text asset dialog
  -> typed source mode
  -> concrete text-content normalization policy
  -> ProjectObjectMediaPayload (name, trusted media type, bytes)
  -> text-asset creation coordinator
  -> ProjectWorkbenchService
  -> ProjectAssetStorageService
  -> IStoragePlacementService
  -> configured storage driver
```

Dependency direction remains UI -> Workbench application policy -> existing storage abstraction. Razor does not choose media types, validate JSON, encode base64, or write files. The content policy does not access storage, and the storage adapter does not depend on UI services or `IServiceProvider`.

## Pattern selection record: concrete text policy and storage adapter

### Forces

- extension growth: text, JSON, Markdown, Mermaid, and log files share one UTF-8 policy with subtype-specific descriptors;
- multiple implementations: none in the current scope;
- construction complexity: low;
- external SDK isolation: none is required for local text content;
- runtime selection: none; the caller already knows it is authoring a text-backed asset;
- testability: content production must be testable without Razor, filesystem, database, or storage;
- dependency direction: the concrete content policy produces neutral byte content and does not depend on persistence.

### Selected pattern

A concrete text-asset service owns trusted descriptors, UTF-8/JSON validation, and media-payload adaptation. A top-level storage adapter owns base64 decoding, typed boundary revalidation, storage classification, placement, and the saved-media descriptor. The UI coordinator owns both dialog submissions and direct canvas uploads, so the page remains a thin router.

### Rejected alternatives

- a switch in `ProjectStructurePage`: mixes format policy into presentation and grows the page;
- direct `File.WriteAllText`: bypasses configured storage, containment, revisions, and authorization;
- lazy conversion from notes when opened: mutates during navigation and creates two sources of truth;
- a new strategy, factory, or builder: there is one built-in text-content algorithm, so the existing generator extension point is sufficient;
- a new project: there is no independent deployment or dependency lifecycle yet;

The pre-existing public generator and resolver contracts remain available and remain the production extension path so this refactor does not break callers. The built-in text generator delegates to the same concrete format and content policies used by upload normalization.

### New types

| Type | Project | Responsibility |
|---|---|---|
| `ProjectAssetSourceMode` | Workbench | Strongly typed upload-versus-create choice. |
| `ProjectAssetCreationService` | Workbench | Validates and adapts Text, JSON, Markdown, Mermaid, and Log content to the existing media payload boundary. |
| `ProjectAssetStorageService` | Workbench | Revalidates managed media, classifies it, and delegates placement through the configured storage boundary. |
| text-asset dialog/coordinator | Workbench Pages | Collects UI input, owns direct-upload routing, and persists before closing the dialog. |

This is not fake separation: unit tests construct the concrete creation and storage services directly, `ProjectWorkbenchService` no longer owns media placement, and the Razor dialog does not contain filename, extension, MIME, JSON, or base64 policy.

## Node action audit

| Node capability | Default activation | Inspector/context actions | Edit behavior |
|---|---|---|---|
| related project | open related structure | open structure | owning workspace only |
| web link | embedded/external preview | open link | persisted nodes only |
| previewable stored file | preview | preview, preferred app, containing folder | persisted nodes only |
| file collection | authorized FileTools browser | browse files, trusted Explorer action when local | no generic edit for projected folders |
| runtime | quick launch surface | normal/admin launch | owning workspace only |
| Mermaid source | Mermaid viewer | view diagram | persisted nodes only |
| projected read model without an owning editor | meaningful primary action or details | capability-specific actions | hidden/disabled; never navigate back to the same structure route |

The systemic correction is capability-based action visibility: do not advertise Edit for a system-managed projection unless it has a distinct authoring route.

### Remaining action and form backlog

| Priority | Node area | Current mismatch | Recommended boundary/action |
|---|---|---|---|
| P0 | Text/JSON/Markdown/Mermaid editing | Creation stores real media, while generic Edit cannot yet replace media. This change removes the legacy Mermaid source field from generic Edit so it cannot mutate Notes as fake file content. | Label the current action `Edit metadata` and add a versioned content-edit action through `ProjectAssetCreationService` and storage replacement. |
| P1 | Generated images | Generator-only provider/model/size/quality fields are now hidden from generic Edit because it cannot persist them. | Keep metadata-only Edit and add a separate driver-backed `Regenerate / Create version` action. |
| P1 | Other system-managed projections | Universal reconnect/disconnect/progress/priority/delete actions can target records that cannot be mutated; delete actually hides or detaches some projections. | Centralize a node-action capability policy for catalog, inspector, quick dialog, and handler authorization; call projection removal `Hide / Detach`. |
| P1 | Universal Open/Test | Actions are shown for content where they have no useful semantic target; Test opens project-level Test Lab regardless of node. | Select primary actions per kind: preview assets, open links, launch runtimes, and expose linked testing only for testable nodes. |
| P1 | Milestones | Target is free text despite typed scheduling support. | Add a typed target date/range form using the scheduling mutation boundary. |
| P1 | Meeting/recording/transcript/work-item relationships | Create can choose relationships that generic Edit deliberately removes. | Add a dedicated relationship editor backed by assignment/relation services, following the task-dialog pattern. |
| P1 | Recordings | Only free-text source/storage references are captured; transcript creation is an empty scaffold. | Add managed media or connector source plus player; distinguish `Add transcript scaffold` from provider-backed `Transcribe`. |
| P1 | Test plans/evidence | Forms cannot attach tested nodes, runs, screenshots, logs, or results. | Author through Test Lab services with plan/result fields, relations, attachments, and direct routes. |
| P1 | Repository/deployment folders | Folder-shaped nodes cannot use the in-app browser. | Extend the governed collection-scope strategy to authorized repository and deployment-folder paths. |
| P2 | Asset Folder field | Subtitle is labelled Folder, but storage placement ignores it and uses a managed path. | Relabel to `Folder context`; add a typed storage target only when placement is supported. |
| P2 | Connectors | Generic strings are collected without typed connector metadata or meaningful actions. | Add connector kind/provider/direction/configuration reference plus capability-driven Configure/Test/Open actions. |
| P2 | Team hierarchy | Participant creation has no parent-team selector although the composer supports the relation. | Add parent-team selection now and relationship editing later. |

Strong existing interactions to preserve are dedicated task create/edit, secret-reference picking/editing, attachment preview, safe web preview, runtime launch gating, process/workflow start dialogs, and project hierarchy dialogs.

## Acceptance and testability contract

- Double-clicking a projected process-run folder opens the authorized FileTools collection rooted at that managed run path.
- The folder exposes an Explorer action only when the desktop launcher and trusted workspace path are available.
- Projected collection scope identifiers round-trip and are revalidated against node key, run id, artifact kind, typed metadata path, and managed-root policy.
- The generic Edit action is not offered for a projected run folder with no owning editor.
- Text, JSON, Markdown, Mermaid, and Log actions bypass the generic canvas composer and open one dialog offering `Create new` and `Upload existing` modes.
- Generated filename extensions and media types come from trusted descriptors; path-bearing names, mismatches, malformed JSON, and oversized content fail explicitly.
- The persistence boundary reapplies those typed invariants so API and runtime callers cannot bypass them.
- Generated content becomes real stored media through `ProjectWorkbenchService`; Notes remain descriptive metadata.
- The dialog remains open with the user's content when storage or node persistence fails.
- Unit tests cover valid mappings and negative cases without filesystem/database/network dependencies.
- Component tests cover source-mode fields and submission.
- Existing upload behavior remains covered.
- Workbench and relevant test projects build successfully; the 5032 app is restarted and probed after implementation.

## Risks and follow-ups

- Existing notes-only text nodes are not migrated implicitly. A future explicit recovery action may create a backing file from notes with user confirmation.
- Storage placement still occurs before the database commit, so a later database failure can orphan a stored object. Transactional storage compensation is a separate reliability change.
- The broader audit should continue replacing universal actions with capability policies; this change fixes the dead Edit action and the high-value folder/text-asset paths without widening into unrelated node refactors.
