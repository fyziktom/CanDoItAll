# Execute: Storage catalog reselection repair, then Storage Selection UI decoupling

You are the senior C#/.NET and Blazor engineer implementing this assignment with Codex GPT-6 Astra Max. Work on the **current CanDoItAll checkout**. Implement, integrate and validate the work; do not stop after a plan or after the prerequisite repair.

This is a compatible external Behavioral bundle, not a demand to recreate the canonical workflow-bundle folder schema. Read this complete package, the current repository instructions and the maintained architecture. You may choose the smallest coherent implementation. The required boundaries, observable behavior and evidence are not optional; particular helper names or a fixed number of classes are not prescribed.

## Read first and establish the actual baseline

Read `AGENTS.md`, `.github/copilot-instructions.md`, the complete current `docs/testing.md`, `.github/workflows/ci.yml`, `docs/architecture/ui-component-seams.md`, applicable SharedInfo guidance, and the maintained Workspace Core/API/Storage boundary records. Then read [the shared foundation](shared/README.md), [review](STORAGE_REVIEW.md), [selection inventory](SELECTION_REVIEW_NOTES.md), [architecture](ARCHITECTURE.md), [validation](VALIDATION_MATRIX.md), [application regression matrix](APPLICATION_REGRESSION_MATRIX.md), [sensitive state](SENSITIVE_STATE.md), [development loop](DEV_LOOP.md) and [proof limits](PROOF_STATUS.md).

The reviewed implementation is `cbb135c7c8d76ff50a624c142f12faf8c9b55f91` on `components-decoupling`, titled “Extract Workspace Storage catalog UI and repair API read lifetimes.” The preceding `73053bd4fe723d56043df410c322c0651f6cea4c` archives the previous input. These are provenance, **not execution pins**. Do not checkout/reset to them. Inspect actual HEAD, working tree, intervening changes and sibling revisions; adapt paths and tests to legitimate drift. Do not execute historical archived bundles again. [EV01, EV03, EV04]

Use CodeAnalytics MCP for current consumers and affected tests when available. Inspect actual Components contracts/examples through the Components tooling when available. Otherwise use local source, project evaluation, build-backed discovery and CLI/browser evidence, and record the fallback honestly. Do not infer a dependency from a namespace string alone. Do not treat default-branch search as proof of the current branch.

## Objective and scope

First close **SCAT-R1**, a bounded Storage administration draft-lifetime defect. Then extract the complete **Storage catalog selection field and dialog family** into a light feature boundary with an independent development sandbox, and integrate it into its actual production consumers.

The picker stages an explicit list of catalog GUIDs. It does **not** grant authority, persist an Agent, administer a Storage catalog, test a driver, open file content or run Recovery. Preserve that distinction. Do not copy the administrative mutation ledger/receipts pattern into this read-and-selection feature: it has no new durable write of its own.

Deliver the real selected-reference table, choose/remove behavior, real nested picker dialog, search, staged selection, Apply/Cancel, missing/disabled/read-only states, source failure/retry and production Agent integration. Preserve the parent’s explicit Save and existing access normalization. UI text, code, comments, committed documentation and test descriptions remain English. Your final report may be Czech.

Out of scope: Data Sources/profile activation or transfer extraction; placement Recovery/owner continuation extraction; generic Settings renderer extraction; new HTTP APIs; a privilege or credential redesign; storage-driver implementation; database schema/migrations; distributed transactions; entire Agent/Processes/Workbench refactoring; and moving all shared component families just to make a graph count smaller.

## S0 — repair Storage catalog same-target reselection without weakening exact retry

The current `CatalogSession.SelectAsync(id)` discards `Draft` and starts `ReadEditorAsync` even when the same current, acquired catalog is selected. `CatalogSurface` attaches it to the selected row. The real BaseLib `SelectionListItem` invokes OnSelect unless Disabled; IsSelected only affects presentation. Thus another activation of the highlighted row can silently discard unsaved fields, incomplete numeric input, validation, wizard step and EditContext. This is a source-derived finding, not a runtime reproduction supplied by the reviewer. [ST01–ST04, ST09, SC01]

Reproduce through the real CatalogSurface and actual row button before fixing. Select an existing editable catalog, move to another wizard step, type a changed name and an incomplete port/order, trigger validation where relevant, and activate the same selected row again. Demonstrate current draft/context/raw-state replacement and the unnecessary owner read. Also exercise reselection while an accepted Save/Test or automatic read-back is held.

A same-target no-op must require a genuinely acquired, current, non-deleted draft matching the requested identity. Do **not** add only `if (SelectedId == id) return;`: a failed/missing/unacquired same-ID selection must still retry. Keep initial exact loads, deliberate different-target/New transitions and A→B→A protections. Do not resurrect a deleted draft or bypass unresolved mutation admission. Preserve any explicit retry/reload path as a separate intent rather than overloading ordinary selection.

Retain field values, validation, EditContext, step and receipts on the no-op. No extra database/driver/routing/Activity operation is allowed. Fix at the owning feature boundary; do not globally change SelectionListItem behavior or add a new persistence protocol. Run focused S0 state/renderer tests and the relevant production interaction. Preserve prior API denial/disposal and Files destination-identity regressions. Then proceed to the picker slice in this run. If current checkout already repairs this behavior, prove that fact and proceed rather than force a redundant edit.

## P1 — inventory the actual picker and its consumers

Start from the currently owned family:

- `StorageCatalogSelectionField.razor` and `.razor.cs`;
- `StorageCatalogSelectionDialog.razor` and `.razor.cs`;
- `StorageCatalogSelectionModels.cs`;
- `WorkspaceStorageCatalogSelectionSource.cs` and its registration;
- actual `AgentDetailsDialog` field usage, rendered-session callback, access normalization and parent persistence;
- current `StorageCatalogSelectionComponentsTests` and the generic picker/table/dialog tests. [WS01–WS08, ST15]

Run a current-branch reference scan for these types, namespaces, test selectors and the source interface. Distinguish direct rendered consumers from backend callers and documentation mentions. The review confirmed the Agent details consumer; it did not establish an exhaustive consumer list. Inventory any additional consumer before changing it.

Capture the existing production nested-dialog journey, current read/write counts and dependency/watch baseline before moving code. Determine who owns the parent editor lifetime, profile/caller changes, dialog closure and result callback. Preserve existing owner checks; the present Agent callback already verifies its rendered session. An extraction must not replace that guard with a GUID-only comparison. [WS07, WS08]

## P2 — cut a narrow selection boundary, not another Workspace dependency hub

Recommended project locations are:

```text
src/Modules/CanDoItAll.Modules.Workspace.StorageSelection.Contracts
src/UI/CanDoItAll.Workspace.StorageSelection.UI
src/Sandboxes/CanDoItAll.Workspace.StorageSelection.UiSandbox
```

Use equivalent existing projects if legitimate drift already supplies the boundary. A fourth Presentation project is justified only by a real separation, not uniform naming. Keep presentation orchestration local to its feature; no universal chooser framework, IServiceProvider, object service bag or new partial-service cluster.

The light contract needs only stable catalog identities, display-safe selection metadata, source availability/context and the read/selection seam. The current `StorageCatalogSummary` carries Infrastructure enums and is compiled into Workspace; do not reference its implementation assembly merely to name those values. Project the minimal data in a production adapter instead of moving the entire Storage API or duplicating full runtime records. A source context stamp may identify the original profile/generation for stale-read rejection; it is not an authorization token. Do not put connection clients, raw JSON/configuration, secrets, EF, canonical factories or provider implementations in this contract. [WS05, ST06, ST07]

Protected directions:

- Workspace/Agent **hosts** may compose the selection UI and narrow read adapter.
- The selection UI/sandbox must not reference Workspace, AgentFramework module implementations, Security implementation, Infrastructure, Web/Composition, API administration, Storage catalog administration UI/controllers, or Recovery implementations, directly or transitively.
- Core and API contracts/UI/sandbox graphs must remain unchanged by the picker extraction. Storage administration must not acquire a reverse selection dependency just because both features display catalog names.
- Foundation, MAF runtime and feature-neutral AppComponents must not acquire a product StorageSelection dependency. Do not introduce a new Agent-runtime→Workspace contract edge to render the chooser.
- An existing broad production-module reference may have unrelated consumers. Remove it only if the complete reference inventory proves it is no longer needed; never replace it with another heavier facade.

Reuse the **real** `ResourceCardPicker`, `SelectedReferenceTable` and BaseLib dialog controls. ResourceCardPicker is presently under AppComponents, not under the isolated RecordBrowsing project. AppComponents has a broader but intended neutral graph. Reuse it and evaluate that graph; do not promise a five-project picker sandbox, copy the component, stub it, or transplant all generic components as an incidental cleanup. If a genuinely forbidden implementation edge prevents reuse, isolate the smallest proven generic dependency with explicit consumer/asset tests and document the necessity before widening. Do not add feature semantics to the neutral parent. [WS09–WS11]

Preserve current production catalog owner behavior, including any existing bootstrap work on its read path. Do not change StorageCatalogService, routing, host binding, driver tests, secret resolution or migrations to make this read projection convenient. No new mutating method belongs on the selection port. Keep compatibility adapters only where a real remaining caller needs them; no parallel mutable state machines.

## P3 — correct the existing read/dialog lifetime weaknesses during extraction

### Ownership and current intent

The current field awaits a dialog without a cancellation token or post-await origin check and then consults the latest Value/ValueChanged. Detail and dialog catalog loads can publish after retirement. Its once-loaded cache is not keyed by a source lifetime. These are inherited picker issues, not regressions attributed to the latest Storage administration commit. [WS01–WS05]

Give every open dialog a captured origin comprising the actual parent-editor lifetime, the effective selection/policy revision, the source context, and the dialog request. The exact representation is your design. A stable normalized list echoed by a parent render is not a new intent. A different parent lifetime or A→B→A transition is a new intent even when GUIDs happen to match.

When the parent changes its selection, AllowAll/Disabled policy, source/profile or owner lifetime while a dialog is open, retire that dialog or explicitly mark it obsolete and refuse Apply. Do not apply its result into the successor, silently merge old rights or overwrite newer IDs. Closing the parent closes only its owned child dialogs. Pass the existing supported DialogService cancellation/lifetime handle and guard after each incomplete await; merely canceling a token is insufficient for an owner that ignores cancellation. [WS08, EV09]

A late success, failure, CatalogsLoaded callback, cancel result or finally block must not change another owner’s cache, busy flag, staged selection, notification or callback. Cleanup must detach/dispose the exact owned source even when the view may no longer publish; do not clear a successor source. Disposal is idempotent and leaves no orphan dialog/subscription. Reuse repository lifecycle helpers in tests; do not mask errors with sleeps or catch-all UI suppression.

### Selection policy

Preserve GUID-based, duplicate-free selection. Guid.Empty is never emitted. Deterministic ordering is appropriate for set equality and existing wire behavior; labels, indices and endpoints are never selection identity.

Preserve already-selected missing and disabled catalog IDs visibly until explicitly removed. A disabled catalog cannot be newly selected; a read-only catalog can be selected because the picker does not grant write capability. Removing a disabled selected row must not enable re-adding it as a new selection. Newly fabricated or unavailable IDs must not enter through an internal toggle/confirm path. An authoritative empty result and a failed/unrequested source are different states. [WS03, WS04, WS06]

AllowAll is parent-owned. While enabled, preserve the explicit list but prevent picker edits; toggling it off restores the retained list. An empty explicit list with AllowAll=false must stay empty and limited. Never turn on AllowAll, CanReadStorage or CanWriteStorage to fix a test or compensate for empty/missing data. Preserve the parent’s existing normalization and neighboring fields. [WS07, WS08, WS12]

Dialog Apply is admissible only for its live owner/context and a successfully acquired catalog state; enforce this in the handler as well as the UI. Apply emits staged IDs once and does not save the Agent. Cancel, Escape, backdrop/parent closure and disposal emit no selection. Loading failures preserve the staged selection, expose a safe retry, and cannot masquerade as successful empty data. No raw backend exception text is shown.

### Reads and performance

Retain current behavior: no catalog read for an empty field merely to render it; selected saved IDs may resolve readable metadata without opening the chooser; each new chooser open requests the current catalog. Avoid repeating the same list read on every parameter echo, search keystroke, toggle or render. Local filtering of an acquired catalog remains local. Do not invent server paging, background polling or an administrator session cache.

Keep metadata availability separate from the parent’s persisted ID list. A failed refresh never drops IDs, promotes them to AllowAll, or reassigns them to the first current row. A successful snapshot from a retired profile must not relabel current IDs with old metadata. Source/context changes invalidate cached names; a genuine parent-owned explicit new session may acquire fresh data.

Display only approved catalog metadata. Diagnostics must not expose credentials, vault material, raw configuration or sensitive exception details. An endpoint/root string already intentionally displayed is not permission to echo embedded credentials. Apply an appropriate safe projection without fetching secret payloads. [SENSITIVE_STATE.md](SENSITIVE_STATE.md)

## P4 — production integration and faithful sandbox

Use the extracted components in the **actual Agent details Storage access surface**, not only a demo. Preserve rendered-session checks and add the explicit lifetime/revision seam needed for the generic child. Keep Agent draft mutation, parent EditContext, save admission, normalized settings and runtime policies with their existing owners. The current callback updates only AllowedStorageCatalogIds and normalizes access; retain its meaning. Do not let the read adapter persist an Agent or read private runtime state just to render labels. [WS07, WS08]

Keep existing Settings URLs, Core/API/Storage catalog administration, Data Sources, Recovery launch and dynamic settings renderer registrations working. No new application route or HTTP API is required for a chooser. Update actual DI and compiled consumers coherently; obsolete razor imports or duplicate old/new components must not silently select the wrong assembly.

The independent sandbox must mount the same field/dialog/state code with actual generic child components. Include empty/representative/large catalog, selected/missing/disabled/read-only IDs, AllowAll, externally disabled, initial/load/partial failure and retry, two fields/parent owners, held/cancellation-ignoring reads, profile replacement, late dialog result, parent close/reopen and owner A→B→A scenarios. Keep fixture state and pending operations bounded. Its source changes must actually affect subsequent reads; do not merely switch success labels.

The sandbox does not run EF, Storage drivers, Agent runtime, API accounts, vault or production DI. Its parent draft is a labelled fixture, not persistence evidence. Source and independently published Production hosts must render real styles, fonts, dialogs, bounded card results and overlay stacking. Search/caret, keyboard use, Apply/Cancel and closing only the child must work inside a realistic parent dialog. Do not log test credential material or capture unrelated real application state.

## P5 — prove the application boundary, not just a green toy demo

Execute [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md) and [APPLICATION_REGRESSION_MATRIX.md](APPLICATION_REGRESSION_MATRIX.md). They define observable obligations, not a quota of methods or assemblies. Derive current test names and data rows; do not inherit a prior reported count as this run’s result.

Build every affected production root. Select tests from current references, then perform build-backed discovery with expected/actual counts and the same execution filter. A zero/mismatched filter is not success. Honor HostPlatform and other actual classifications; do not remove or relabel tests just to close a run. Use short isolated configuration/output names where needed and serialize builds sharing output. No broad solution-configuration or fixture rewrite merely to run this small slice.

Required proof includes real field/dialog tests, generic picker/table/dialog regressions, actual Agent host/persistence and current runtime catalog restriction checks. Demonstrate that Apply only changes the staged list, parent Save stores the exact IDs with AllowAll=false, another catalog remains denied/filtered, read-only behavior is still enforced at the runtime owner, and neighboring permissions do not change. Use existing safe owned fixtures, not a live LLM or a fake owner registered in production. The parent cancel path must leave persisted settings unchanged.

Use real Web Playwright for the nested Agent dialog, selection, parent Save, reopening/read-back and cancellation. Also test the small S0 catalog interaction in production. Separately run source/published sandbox journeys. Wait on exact acquired editor/operation completion, not button existence, disabled state or an arbitrary delay. Inspect large-desktop geometry, scroll ownership, focus, overlay order and browser/circuit errors.

Re-run appropriate smoke/regression paths for Settings Core/API/catalog and passive deferred hosts. Preserve ordinary project attachment/export, Resources reopen and Storage routing behavior through affected consumer selections. Do not operate the user’s running application on port 5032. Use private PostgreSQL 18, control-plane/vault/file roots and ephemeral loopback hosts; prove exact identity/labels before cleaning only resources you created.

Assess a named broad-Stable trigger after actual changes are known. A leaf extraction or a phase completion alone does not require an unfiltered suite. A real public-owner protocol, canonical factory, shared persistence/fixture, build-policy or composition change does require reassessment under current repository rules. Keep the decision and evidence explicit. Do not count the earlier mixed Stable run plus targeted repairs as a clean new broad run. [EV03, EV05–EV08]

Run the mandatory complete proposed-tree portability-static procedure, review new/stale findings, fix genuine defects, record intentional baseline changes and enforce without rewriting the baseline. Run applicable documentation/evidence and secret checks, including new files. Update maintained architecture/README/testing documents and actual test-solution/CI membership. Historical bundles and shared v3 are sealed input, not execution logs.

Measure the development loop as described in DEV_LOOP.md. Preserve Core/API/Storage admin graph and watch isolation; evaluate the new selection graph honestly with its actual neutral components. No promised fixed count or full-Web speedup. Record source/sibling revisions, native/package edges, watch inputs, startup readiness and visible Razor/C# edit behavior; move/prove any owned CSS/JS assets. No fabricated N/A or performance claim.

## Completion and delivery

Finish S0 then the complete picker family in this run. Do not start Recovery, Data Sources or another module. If an environmental gate is unavailable, preserve progress and provide its exact unresolved requirement; never substitute a sandbox claim for production persistence/authority proof.

Keep local commits signed under the existing Git/GPG configuration. Reuse the task’s unlocked signing process/session where supported; do not disable signing, store a passphrase or alter machine-wide Git configuration. No push, merge, reset, deployment or operation of unrelated processes is authorized by this assignment.

Final report: actual start/end SHA and changes, S0 reproduction/fix, chosen dependency cut and consumer migration, all test discovery/execution and remaining failures, production and independent browser/publish evidence, graph/watch results, mandatory gate results, safe cleanup, and the updated partial Workspace checklist. Explicitly state that **Workspace is not fully decoupled** while Recovery, Data Sources and residual configuration hosts remain. The small chooser is complete only when the actual production user can select/cancel/save without changed permissions or broken unrelated application behavior.
