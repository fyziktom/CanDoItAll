# Storage Selection: current code and migration risks

## Current family and ownership

The Workspace field renders a SelectedReferenceTable and opens a StorageCatalogSelectionDialog. The dialog renders the real ResourceCardPicker with a bounded results viewport. Its source interface returns `StorageCatalogSummary` from Workspace; that type is unsuitable as a light seam because it belongs to the backend module and names Infrastructure storage values. [WS01–WS05]

The confirmed production caller is the Agent details Storage-access section. It passes `AllowedStorageCatalogIds`, `AllowAllStorageCatalogs` and a callback capturing `renderedSession`. The parent callback checks `IsCurrent(owner)`, normalizes IDs and invokes its existing access normalization. This existing guard must survive. The parent, not the chooser, owns Agent persistence and effective Storage permissions. [WS07, WS08]

Default-branch search was used to locate candidate paths; all evidence entries are pinned reads. Codex must discover additional current-branch consumers before deleting aliases, adapters or project references.

## Baseline semantics to preserve

| Behavior | Current source/tests |
|---|---|
| Current catalog on each new dialog open | Actual dialog initialization; two-open test |
| Saved ID labels may resolve without opening | Field parameter handling and saved-ID test |
| Empty field does not eagerly list | Empty-then-selected test |
| Missing and already selected disabled IDs stay removable | Dialog option building and tests |
| Disabled IDs cannot be newly added; read-only IDs may be chosen | Toggle policy and tests |
| Apply returns normalized selected IDs; Cancel returns no selection | Dialog result/cancel paths |
| AllowAll suppresses editing but preserves explicit IDs | Field and tests |
| Real parent handles Save, not picker Apply | Agent call site and guarded callback |

These are declared/source-inspected behaviors, not reviewer-executed tests. The existing component test file contains eight Fact declarations; derive actual discovery from the current test assembly. [WS03, WS04, WS06, WS08]

## Inherited weaknesses to address in this extraction

### S1: dialog result is not tied to the opening owner

Field OpenPickerAsync calls DialogService.OpenAsync without a cancellation token. After the await it compares against current Value and invokes current ValueChanged. Parameters can have been replaced, the field can be disposed, AllowAll/Disabled can have changed or the source can belong to a different profile. The outer Agent callback's captured session is useful, but a child reused with a new callback must not deliver an old result through that new callback. This is a source-derived risk that needs a controlled parent-rebinding reproduction. [WS01, WS07, WS08]

Do not infer parent identity solely from the selected GUID list. Supply an explicit opaque editor/source lifetime seam where necessary. Equal values rendered repeatedly are a no-op; equal values in a new owner lifetime are not. Changes away and back need a revision, not value equality alone.

### S2: metadata can cross read or profile lifetimes

Field metadata loads and dialog loads publish results and callback effects without checking a request generation/current owner after await. The field's `catalogsHaveLoaded` cache is not keyed to profile/source context. An older detail fetch can overwrite a newer chooser snapshot; a disposed or replaced caller must not receive that publication. [WS01, WS03]

Use separate ownership of read results and selected IDs. A failed refresh leaves selected IDs intact and metadata explicitly stale/unavailable. Only a successful current snapshot can establish that an ID is absent. Do not silently label data from another context as current.

### S3: ready/selection policy exists partly only in markup

Apply is disabled during loading/error in Razor, but ConfirmAsync itself does not check that state. Toggle only explicitly refuses an unselected disabled catalog; an arbitrary ID absent from current options can enter through a direct handler call. Harden the actual admitted selection transition, including duplicates and retired contexts, not only the button attributes. [WS03, WS04]

This does not establish a backend authorization bypass: the picker stages data and the runtime keeps its own checks. The objective is reliable UI intent and defense of the declared selection contract.

### S4: cleanup and diagnostics need bounded ownership

Both existing components cancel/dispose a single CTS without idempotence or an explicit owned-dialog close. List failures concatenate arbitrary exception messages. Ensure cleanup works for cancellation-ignoring owners and that one field cannot close another field's dialog. Use safe diagnostics and never resolve secret payloads merely to display a catalog. [WS01, WS03]

## Generic components: do not invent their placement

The current ResourceCardPicker is still in AppComponents; it was not already moved to AppComponents.RecordBrowsing. AppComponents references several neutral component families including Canvas, FileTools and conversations. Reuse the real components and evaluate the actual graph. A feature-neutral broader graph is not automatically a backend violation; it is also not a five-project sandbox. [WS09–WS11]

Do not make AppComponents reference StorageSelection. Do not duplicate ResourceCardPicker/SelectedReferenceTable into the new feature or replace them with toy controls. A later intentional shared-family optimization may reduce that neutral closure, but it is not automatically part of this assignment.

## Production integration pitfalls

Keep the existing parent IsCurrent check. Ensure changing parent lifetime or permissions while a nested dialog is open invalidates its result even if visible IDs are unchanged. Opening/closing the picker must not reset Agent instructions, capabilities, project access, secret grants or the parent EditContext. Do not replace the complete AgentDetailsDialog while extracting its Storage child.

Preserve `CanReadStorage`, `CanWriteStorage`, `AllowAllStorageCatalogs` and exact allowed IDs as separate concepts. Runtime listing/disclosure/browse already filter by access and owner facts; the UI does not replace those checks. [WS08, WS12]

Changing read-source DI must not register a fixture owner in production, capture a scoped canonical profile in a singleton, or require a new database/vault root in the sandbox. The existing Workspace adapter can remain a composition point behind the new narrow selection seam where justified. [ST15, WS05]
