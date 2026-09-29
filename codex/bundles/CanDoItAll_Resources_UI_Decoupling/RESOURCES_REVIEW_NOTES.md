# Resources: source review, architecture and impact

## Selection and scope

Choose complete Resources next: `/resources` Registry and Browse, including promotion,
reopen and host file actions. The next assignment is not a whole Workspace/Projects/
Workbench extraction. Resources has a bounded visible workspace and existing owners for
metadata, project admission and authorized file operations. Its dependencies are numerous,
so the slice is medium-sized, not a claim that it is the smallest remaining codebase.
Processes and Workbench stay later. [RS01, RS02]

No current Resources UI RCL or sandbox appears in the reviewed change inventory; the
current route still renders its implementation-owned children. Confirm the current checkout
before creating projects. Names below are suggested locations, not a forced naming quota.

```text
Resources module -> Resources presentation policy -> Resources.UI -> light contracts
Resources sandbox ------------------------------^            -> real shared UI
```

A separate presentation assembly is optional. It is useful for sharing state policy, not
for adding forwarding-only layers. Registry and Browse have different effect lifetimes;
compose them rather than growing a monolithic controller. No production HTTP hop is required.

## Inventory of the actual surface

| Surface | Current responsibility | Preserve/extract |
| --- | --- | --- |
| Registry route and code-behind | Route queries, list/filter/editor, project/party/secret reads, Save/Delete, Agent context | Thin module host; light real list/editor and explicit workspace state |
| Connector field child | SharedKernel configuration schema/state rendered by a component physically in Workspace, using Security picker values | Deliberate bounded shared seam or feature renderer; no Workspace implementation edge |
| Browse | Source classes, source opening, FileBrowser, promotion, preview, local/download actions | Same visible controls and real FileTools components; owners behind real ports |
| Promotion dialog | Selected object and target project admission, name/sensitivity, accepted result | Typed original dialog/operation lifetime, actual BaseLib dialog |
| Governed resource | Stable saved occurrence, read-only metadata, authorized reopen | No editable raw locator or hidden authority export |
| Assets/context | Browse scoped CSS, FileTools static assets, Agent navigation/source context | Move asset ownership with actual rendering; retain host context/navigation |

The page markup and code-behind were read in full, as were both meaningful Browse children
and their principal owner chains. Tests and some surrounding directories were sampled;
this is not an exhaustive audit of all FileTools/shared source. Coverage is explicit in
[SOURCES](SOURCES.md). [RS03, RS04, RS05, RS06, RS13, RS18]

## Current compilation boundary

`CanDoItAll.Modules.Resources.csproj` references BaseLib, FileBrowser Core/Components,
FileInteraction Core/Components, AgentFramework Components/Core/Models, Memory
Abstractions/Application, SharedKernel, Infrastructure, FileTools Integration and its
abstractions, AppComponents, Projects, Security and Workspace. Moving the route alone
would keep most of that graph attached to development UI. [RS02]

`ResourceModels.cs` mixes enums/config/editor/summary values with EF entities/mappings and
`ResourcesService`. `ResourceEditorModel.Configuration` is a Workspace `ConnectorConfigState`;
its project field carries a real `ProjectWriteAdmission`. `ConnectorConfigFieldEditor`
is another direct cross-module renderer dependency. Do not wholesale relocate mixed files
or recreate owner services in Contracts. [RS08, RS13]

Evaluate every proposed allowlisted dependency, including package-to-sibling-source
replacement. Public types, loaded child components and runtime registrations matter in
addition to csproj references. Keep private production wrappers private where possible;
map small source/health/options values and use legitimate neutral FileTools sessions/content
ports without importing concrete storage drivers or Infrastructure types.

## Registry findings to address in the extraction

### RE-A: the submitted clone is good; completion still replaces live edits

`SaveAsync` takes `CloneEditor(editor)`, and the owner also assigns a confirmed ID after
commit. Preserve those protections. The page then loads the saved editor and assigns
`editor = loadedEditor` when its single page load generation still matches. Ordinary typing
does not advance that generation. Thus later name/config text can be lost on successful
read-back even though the original request was correctly captured. A direct duplicate
submit is not independently admitted by the handler. [RS04, RS08]

Use actual pre-blur inputs, stable draft/EditContext, separate mutation admission and
submission-relative reconciliation. Project selection changes are not equivalent to
ordinary text edits: never apply an earlier lifetime's ID/admission to a new target.
Existing known-commit warnings remain associated with the original resource even if
navigation or Reset created another editor. [RS14]

### RE-B: separate availability from fallback

The page's `SelectedResourceManifest` and `NormalizeResourceEditor` fall back to the first
manifest. Normalization assigns its key/version and filters configuration. That is not a
safe implicit response to a missing exact connector. The owner may have explicit legacy
resolution rules; keep those rules, but do not silently migrate a missing connector at the
presentation layer. An unavailable resource from `GetAsync` can also appear as a fresh empty
editor unless the caller verifies the requested ID. [RS04, RS08]

Explicit missing resource/project/connector/party/secret states need visible feedback.
Do not fabricate current project labels for retired bindings or select a convenient secret.
Reference loaders may fail independently without discarding the editor or making another
section unavailable. Failed results and empty results are distinct.

### RE-C: keep precise project and route meaning

A resource-only route may load historical metadata; resource+project requires the same
project lifetime. An explicit missing project keeps Agent context failed. A project editor
selection captures current admission deliberately; a reload does not rebind automatically.
Owner mutations keep serializable scope, sorted lock keys and true transaction enlistment;
Workbench mutation projection reads retain their coordination. [RS01, RS07, RS08, RS17]

No schema/idempotency/permission redesign is needed. The stored lifetime is provenance,
not actor authorization. Exact cleanup remains possible under the existing retirement
policy. Deleting resource metadata does not authorize deleting stored file bytes.

## Browse findings and required lifetime model

### BR-A: a catalog refresh can restore an obsolete source selection

`RefreshCatalogAsync` captures `reopen` before awaiting catalog load, then calls
`OpenSourceAsync(reopen)`. Catalog reads lack their own latest-request fence. A newer
selection can therefore be overwritten by an earlier refresh. Existing `OpenSourceAsync`
checks its own generation and disposes stale newly opened workspaces; preserve that and
extend ownership to the upstream refresh. [RS05, RS15]

Test source A -> refresh held -> select B -> refresh completes, A-B-A, missing source,
failed catalog with retained data, and replacement/disposal. Test current Agent context
publication, not just the displayed title.

### BR-B: accepted promotion is not permission to navigate back

`HandlePromotionCompletedAsync` accepts the original result, awaits a parent callback,
then unconditionally opens the completed source. The operator may now be on another source.
A known saved result must remain reviewable without hijacking that selection or reopening
its dialog. Keep operation receipt, source/item origin, parent notification and any optional
refresh/navigation as separate decisions. [RS05]

The dialog's `Open` can replace its live fields; `SaveAsync` reads `sourceKey` again after
awaiting the owner, then calls Reset before the parent callback. If that callback fails,
the catch path reads reset nullable source fields. Use immutable submitted origin/result
and guarded completion, so an observer failure neither loses the identity nor creates a
second null-state exception. [RS06]

### BR-C: preview acquisition and release must own the same instance

`OpenPromotedResourceAsync` assigns the opened interaction after an await without a separate
preview origin check. `DisposeInteractionAsync` awaits release then clears the shared field.
The old release/open can interfere with a successor. Capture/detach exact instances,
fence stale opens and release their handles. Never suppress handle-leak evidence merely
because the current page stopped rendering that preview. [RS05, RS11]

File action handlers already capture a current workspace for the owner request, but their
later messages use shared fields. Preserve the admitted original action and prevent stale
UI/effects from changing a successor. Current local-launch support must be checked; raw
file names, source titles and paths are not launch authority. Do not roll back a completed
external action by claiming cancellation after the fact.

### BR-D: promotion has post-commit effects outside its writer

The writer validates project admission and deduplicates an exact stable config under the
project lifetime/transaction. After it returns an ID, promotion publishes scope change,
reads revision, logs and revokes temporary authority. Some of those failures currently
prevent a result from reaching the UI. Preserve the smallest typed confirmed fact before
these secondary effects, and expose unavailable revision/cleanup separately. [RS10]

Similarly, ResourcesService logs in the catch that is supposed to wrap a known commit;
a throwing diagnostic sink must not replace the confirmed ResourceId with an unknown
failure. Test this owner boundary as a fault case and change only what the reproduction
requires. Do not invent a new distributed transaction or outbox. [RS08]

An acknowledgement failure at the real durable boundary is still Unknown. Do not infer
commit from an attempted SaveChanges, a preallocated Guid, a matching name, or a later
coincidental row. A found duplicate must keep `Created == false`; retrying a read must not
publish another scope revision or repeat promotion.

## File authority and limits to retain

Sources are only currently authorized project/filesystem/IPFS/FTP sources. Source catalog
resolution uses canonical keys and storage fingerprints; the total catalog cap is 512.
Keys are not opaque grants. Preserve their strict prefix and canonical GUID semantics.
[RS12]

Browse uses page size 50 and no retained session state. Search budgets: 32 containers,
2,000 items, five seconds, one concurrent request, 200 matches and 2 MiB retained bytes.
Preview has a 16 MiB cap. Do not make unbounded scans or caches to compensate for a heavier
UI graph. Avoid per-render owner reads and rehydrating full snapshots on every keystroke.
[RS05, RS09]

Promotion re-resolves source, activates the exact item, checks current actor access and
scope/storage consistency, and writes only stable occurrence metadata with the target
project admission. Governed reopen creates fresh current authority and releases it through
FileTools. Opaque handles and authorization URLs must not enter persisted Resource config,
query strings, logs or test artifacts. [RS10, RS11]

The baseline tests distinguish supported internal preview, unsupported pointer-double-click
preferred-app behavior, keyboard promotion and read-only download/local action policy.
Retain them. The sandbox may expose harmless simulated effects with counters, not launch
real applications or connect to a user's FTP/IPFS endpoint. [RS15]

## Testing and consumer consequences

Existing `OwnerPostcommitPageTests` are shared with TestLab. Adapt only necessary Resource
accessors after the seam move; retain other owner cases. Tests that assert a private field
or old assembly path may need re-targeting to the public behavior, but their behavioral
obligations are not waived. `ResourceFileBrowsePaneTests` contain a virtual test host for
controlled source opening: replace it with a real explicit owner seam rather than dropping
its stale-resource disposal test. [RS14, RS15]

Retain and expand real PostgreSQL/resource admission, current file authorization and
`ResourceStorageObjectIntegrationTests`. The existing latter journey saves a harmless
fixture file, promotes it, verifies stored stable metadata/revision and reopens actual
content. A simulated sandbox success does not replace it. [RS16]

Discover all remaining Resource connector/schema, historical admission, Memory source,
Workbench projection, navigation, FileTools action and HTTP consumers on the actual
checkout. If the narrow shared configuration renderer moves, include its actual Workspace
callers and validation. The reviewer did not execute or enumerate every such suite; no
invented fixed count is supplied.

## Deliberate non-goals

No API-only UI conversion, new endpoint/auth architecture, new provider capabilities,
new database schema/migrations, resource runtime execution, generic form engine, global
concurrency framework, default file write/delete permissions, Workbench/Projects/Workspace
module extraction or broad sibling refactor. Preserve production storage and ordinary app.
