# Workspace completion and application regression

## Current closure — 2026-09-30

The completed Workspace renderers, production hosts and 22-entry census remain intact.
The [critical-fixes closure](workspace-critical-fixes-closure.md) records final production
application `3d7c88f384b7920744464a7c7570530ca825325a` and Components
`22d5b21afdf80c2bca74c1c598f0b1bb72c86f9e`, followed by separately identified private
test-observation/catalog corrections. WCL-R1, WC-C1 and WC-C2 are closed locally; WC-C3's
controlled correction passes but composed navigation remains open under WCL-NAV1.
WCL-DEL1, required live/environment proof and remote dependency delivery also remain open.
Application readiness is false. WC-H1's completed test-support ownership is recorded below.

## Completed extraction record

This record follows the sealed `CanDoItAll_Workspace_Completion_and_Regression` package on
`components-decoupling`. Entry HEAD is `1080c24163afd3cf65fcc68756913c5dd3262a62` (bundle only);
the reviewed product checkpoint is `fbfba65de9d3118729b73ce9dcf97f28a219c84c`.
The checkout was clean. Source-mode Components/FileTools references remain enabled.
No ordinary application, retained database, external FTP/IPFS service or port 5032 is a fixture.

## Stages

| Stage | Current disposition |
| --- | --- |
| S0 existing catalog/selection | Fresh six catalog reselection, 30 selection, one actual Agent Apply/Cancel/Save and 38 runtime-policy cases passed. Two CRM live harness cases completed no-send rehearsal only. |
| W1 Recovery | Renderer, session, owner adapter and independent sandbox implemented and separately validated; see below. |
| W2 Data Sources | Renderer, transfer dialog, owner adapter and independent sandbox implemented and separately validated; see below. |
| W3 Configuration and census | Neutral renderer, independent sandbox and complete file census implemented and separately validated; see below. |
| Application campaign | Browser and live lanes have completed with mapped blockers; the frozen full Stable checkpoint is tracked separately. Application readiness is not declared. |

Raw attempts are retained under the ignored `artifacts/workspace-completion/20260930-1080c2416`.
Its append-only campaign result copy records every required group. Package/tool validators are
bookkeeping proof, never application test results. The early no-send rehearsal used no model
requests; the later live campaign consumed its authorised forty requests, including failures.

The remaining Workspace UI boundaries are complete: S0 and WS-01 through WS-06 have
passing production/owner proof, independent sandboxes and a complete census. This decision
does not certify application readiness. The [findings index](workspace-regression-findings.md)
retains the shared lifetime failures and incomplete live journeys. The
[campaign report](workspace-regression-campaign.md) records the separate group decisions.

## W1 Recovery boundary

`StoragePlacementRecoveryDialog` remains a production composition host. It owns a new
session when its captured StorageId or caller changes and retires the old one on disposal.
`WorkspaceStorageRecoveryOwner` maps neutral requests to the existing
`IStoragePlacementRecovery` and `IStoragePlacementOwnerContinuation` owners. The original
owners, public wire contracts, grants, database fence and receipt protocols are unchanged.

The real two-feed grid, independent offsets, exact inspection and all five explicit actions
live in `CanDoItAll.Workspace.StorageRecovery.UI`. Its contracts project has no backend
reference. The standalone sandbox references only this leaf and real BaseLib components:
five evaluated projects including the sandbox, one package, no native assets, unresolved
edges or cycles. Its data source is explicitly simulated; it registers no database,
storage driver, model or persistent recovery owner.

Reads have a generation and an exact cancellation source that is disposed when that read
returns. Superseded success, denied reads and finally blocks cannot replace current detail.
Same acquired intent inspection preserves the detail. A current denial retires actionable
data. Empty scanned continuation pages retain the owner's NextOffset.

Each command captures profile/generation, intent, action and, for Workflow, the original
run/occurrence/slot/fingerprint. The handler admits one operation per session. An accepted
command is allowed to settle after view disposal and cannot publish into a successor.
The persistent recovery protocol remains the source of truth when an intent is reopened.

An acknowledged result is stored before optional list/detail observation. A refresh failure
keeps that result and original identity, labels stale observations and disables their
actions. An unacknowledged command remains explicitly uncertain. Refresh never repeats a
command. This repairs a reproduced pre-existing UI defect: the old catch cleared confirmed
file progress when its subsequent list read failed. `w1-progress-before` failed on the old
component; the production component suite passes after extraction.

### Actual owner/browser evidence

The private PostgreSQL browser fixture prepares two genuine Workflow outputs through the
existing owners. A controlled native transaction interceptor interrupts one before native
commit and one after commit; stored bytes are already acknowledged. The production UI
then completes the prepared asset or records the existing receipt respectively. Independent
read-back checks one native node, exact original output/receipt identity, unchanged prepared
command and run, equal content hashes, zero driver resolutions during continuation, zero
agent executions and zero provider-history requests. This is deterministic owner/UI proof,
not a live model test.

The default local principal has no Recovery grants and remains denied. The positive fixture
issues an actual short-lived managed credential limited to API, Recovery read/reconcile and
Project Structure write. It does not replace authorization services. Browser HTTP headers
do not authenticate the WebSocket circuit, as confirmed by a test-only circuit observation.
The positive journey selects the server-advertised LongPolling transport during negotiation;
the real JWT validation, managed registry and circuit principal then enforce the exact grants.
This transport condition is explicit and is not evidence for a separate login/SSO journey.

Source and published Production sandbox browser cases exercise both Workflow actions,
independent feeds, empty scanned pages, read-only and denied states, acknowledged progress
with failed refresh, keyboard close, hydration, footer access, real CSS/fonts and absence
of browser errors. Synthetic external-termination scenarios do not attest anything about
an external server. The existing HTTP fixture provides the controlled stopped transport
proof and checks explicit true attestation, original path, read-back count and no reupload.

### Validation ledger

| Selection | Fresh result |
| --- | --- |
| Production `StoragePlacementRecoveryDialogTests` | 7 passed; includes failing-first result preservation |
| Leaf `WorkspaceStorageRecoveryUi` | 20 passed: session, renderer, public API and evaluated dependency checks |
| Source/published Recovery sandbox browser | 2 passed in the complete three-case `w1-browser-final` run |
| Production exact Workflow browser | 1 passed, including real read-only grants, in `w1-browser-final` |
| Workflow continuation owner/HTTP | 17 passed in `w1-workflow-owner` |
| Process recovery/continuation owner/HTTP | 68 passed in `w1-process-owner` |

### Development-loop limitations

The first original Web probe reached Recovery but lost an edit/reload click before hydration;
that failed attempt is retained. The corrected measurement synchronizes on actual startup
prompt dismissal and on the sandbox's interactive marker, then explicitly reloads and reopens
Recovery for each edit. Each row below used an owned process, a warm build/cache, three Razor
and three C# samples; the final source bytes were restored exactly. The original renderer was
restored temporarily from entry HEAD on the W1 composition, so these are local observations,
not an isolated statistically robust Web speedup claim.

| Scenario | Startup to hydrated Refresh | Razor edit samples (seconds) | Executed C# edit samples (seconds) |
| --- | --- | --- | --- |
| Original renderer / Web | 55.20 s | 4.65 / 4.30 / 3.73 | 3.26 / 2.77 / 2.30 |
| Extracted renderer / Web | 51.82 s | 4.71 / 4.06 / 3.97 | 2.88 / 2.65 / 2.18 |
| Independent Recovery sandbox | 7.79 s | 2.65 / 1.03 / 0.80 | 0.44 / 0.33 / 0.35 |

The feature owns no scoped CSS or JavaScript; those edit probes do not apply. Source and
published browser tests exercise the real shared styles, fonts and modal implementation.

### Protected graphs and follow-on work

Entry Core/API/Catalog/Selection sandbox closures were 8/5/5/17 projects; Web was 159.
All had zero cycles/unresolved references. Their before watch inputs and effective assets
are recorded separately from timings. W1 adds only Recovery contracts/UI to the Workspace
production host; no protected leaf or Foundation gains a Recovery reference.
The Workbench test friend is solely for the interrupted-state browser fixture to use the
same internal admission/fingerprint helpers as existing persistence tests.

At the W1 checkpoint, Workspace completion and application readiness were not declared.
The later stages and application campaign below carry their own independent evidence.

## W2 Data Sources boundary and owner effects

`DatabaseSourcesSettingsPanel` composes the backend-free `DataSourcesPanel` and
`DataSourceTransferDialog` with `WorkspaceDataSourcesOwner`. The module retains profile,
credential, driver, bootstrap and transfer-owner references. The new UI references only
its neutral contracts and BaseLib; it does not gain Core, API, Catalog or Selection edges.
The host maps completed safe receipts to the existing notification service.

The acquired editor owns a stable `EditContext` and non-sensitive draft. Password input is
separate, consumed exactly once and cleared before dispatch. A save captures all fields
before awaiting and adopts the acknowledged catalog ID before optional reads. Later edits,
including later password input, survive that completion. Independent read lanes prevent
old success, failure and finally blocks from replacing a newer acquisition. A missing exact
ID cannot become a new profile. Selecting the acquired row preserves incomplete raw input;
a failed acquisition remains retryable. Two failing-first production-component tests
reproduced the original mutable-save and same-row replacement behavior.

The circuit-scoped operation ledger admits one command across panel and transfer-dialog
lifetimes. Closing a view does not release an accepted command. Receipts contain only
original context/profile IDs, action, safe outcome and per-group counts. Unknown and partial
outcomes hold further writes in that circuit, while explicit observation remains available.
A new dialog does not clear uncertainty. This ledger is not a durable recovery protocol:
after process/circuit loss, the existing persisted owners remain the source of truth.
No automatic replay or rollback is introduced.

### Reviewed commit boundaries

| Owner path | Real effect and retained policy |
| --- | --- |
| Profile Save | Saves encrypted catalog metadata; first-profile active selection is a later control-plane write. Blank password retains an existing protected value. Saving does not create the physical database. |
| Profile Delete | Removes saved configuration only. The UI adapter additionally refuses current, pending and startup-locked profiles; physical databases/files are retained. |
| PostgreSQL Test connection | Calls the actual driver's EnsureDatabase, which opens/retries a connection and does not issue CREATE DATABASE. |
| Create empty / Apply schema | Driver creation and bootstrap/migrations are separate stages. Failure can retain a created database or applied stages; the UI reports uncertainty and allows observation. |
| Activate for restart | Existing coordinator bootstraps the exact target and persists selection. Current canonical factories/context do not switch. A new owned process is required to use the pending profile. |
| Transfer coordinator | Executes selected groups in order and retains each group's result. It does not provide a global transaction or undo an earlier success when a later group fails. |

The adapter validates exact source/target identities and current schema evidence before
transfer, then rechecks the captured runtime context before dispatch. Selected keys and
ReplaceExisting are copied at admission. Original group results are retained before any
preview refresh. Exceptions are projected to fixed safe text; the modified facade logs
profile/action and failure type without raw connection exceptions.

### Transfer-owner contracts retained

Workspace default-provider transfer copies only its opaque preference in its target
transaction. Provider transfer preserves referenced protected secret payloads and existing
sharing/target-lock guards. Project transfer requires an inactive, empty target and rejects
retained storage bindings/cross-module state. History transfer retains its empty-partition,
lineage and protected-reference checks. Agent transfer updates its target catalog atomically
without changing target provider/memory authority. Chat transfer preserves versioned graph,
receipt and history-owner restrictions. API accounts/tokens remain instance control-plane
records and are not transferable business-profile groups.

The standalone scenario host exercises the actual raw fields, explicit group selection,
schema controls, partial results, held writes and close/reopen behavior without registering
a driver or backend. Source and published browser cases passed in `w2-browsers-first`;
the production case in that attempt failed during resource-marker setup and is not a pass.
Subsequent attempts retain their setup, synchronization and selector failures separately.
The final restored-source browser run, `w2-browsers-restored-final`, passed all seven
discovered cases: actual production profile/schema/transfer/restart, source and published
sandbox, existing locked-mode geometry, two snapshot refusals and the existing cross-tab
Workbench switch. Production read-back proves exact A/B canonical IDs and workspace roots,
A isolation before restart, B after restart, Secrets/Resources/preferences isolation and
retained instance accounts/tokens. A later Project transfer refusal retains the earlier
confirmed preference copy. No provider requests or external effects are needed for this proof.

The 53-case owner attempt passed all 47 existing cases and five new cases; its sixth new
case failed because the assertion ignored the target's seeded default preference. After
comparing the exact target value before and after the refused operation, all six new owner
cases passed in `w2-owner-closure`. These are separate attempts, not one clean 53-case run.
The final leaf, production component and canonicality runs passed 20, 14 and 26 cases.
Maintained-document validation passed 311 cases. Earlier setup and synchronization failures
remain recorded. The final browser cases also check the relocated create control and wrapped
footer for clipping, keyboard/focus behavior, raw invalid numbers, Unicode and asset errors.

| Scenario | Startup to hydrated controls | Razor edit samples (seconds) | Executed C# edit samples (seconds) |
| --- | --- | --- | --- |
| Original renderer / Web | 55.03 s | 6.66 / 6.31 / 5.27 | 3.33 / 3.27 / 2.31 |
| Extracted renderer / Web | 52.46 s | 5.56 / 5.06 / 4.66 | 3.27 / 2.74 / 2.77 |
| Independent Data Sources sandbox | 8.32 s | 3.14 / 1.26 / 1.49 | 0.28 / 0.19 / 0.19 |

These warm local observations use explicit reload and exact source restoration, with the
same limitations as W1. There is no feature-owned scoped CSS/JS to probe. The evaluated new
sandbox closure is five projects, one package and no native assets. Protected Core/API/
Catalog/Selection closures remain 8/5/5/17; Web grows to 163. No unresolved edges or cycles
were found. Portability review accepted 13 new and seven stale fingerprints: opaque root
values forwarded/displayed/captured and display-search comparisons, with no new filesystem
identity policy. The reviewed baseline contains 15,214 fingerprints.

## W3 Configuration and Workspace census

The real schema loop now lives in the existing Configuration.UI library. It accepts only
SharedKernel schema/state, safe secret-reference options, optional caller-owned input draft,
validation and explicit callback. It preserves field identity, raw invalid input, encoded
error text and the caller's state identity while a callback is pending. It performs no
backend reads and carries no trusted registry or arbitrary component types.

Workspace's compatibility component remains because both the trusted Image Generation
renderer and generic host consume its Security metadata contract. It now only projects
ID/name options once per parameter update and delegates rendering. The projection is
retained when metadata is unchanged, including a caller mutating the supplied list in place.
`SettingsRendererHost` retains its exact key/owner/trust/schema checks and parameter names
and types. Missing or mismatched claims cannot select the generic fallback. No request at
all remains the only generic branch.

Source-wide inspection found one registered renderer source: Workflow's application-owned
Image Generation component. Plugins contribute executor/schema metadata, not a registered
arbitrary component type. Their generic Workflow configuration follows the same no-request
branch. No plugin registration or authority was added.

The final six-case `w3-shell-ready-browsers` run passes the actual production Workflow
edit/save/reopen, source/published independent Configuration sandbox, all eight production
Settings entries and the live request-budget guards. The production case edits both
Project Structure generic fields and the real registered Image Generation renderer, then
compares exact persisted Workflow/node/executor/version/settings through a fresh owner read.
It never executes that definition. The earlier attempt saved and read back correctly but
failed its post-reload synchronization; all attempts remain in evidence. The browser now
waits for the existing shell listener before operating tabs after navigation/reload. Sandbox cases
cover number/JSON errors, booleans/selects, missing secret references, Unicode, caret/focus,
state acquisition and actual static assets. No private secret value is exposed.

The [machine-readable closure map](workspace-closure-map.json) accounts for every recursive
Workspace Razor/code-behind file, its callers, route/slot, effect/lifetime owner, render target
and proof group. Workspace owns no scoped CSS or feature JavaScript. The single remaining
DynamicComponent is the trusted host. Route discovery still includes the Workspace module
assembly. The obsolete Files wrapper had no source or test caller and was removed; the live
Files surface remains inside WorkspaceSettingsSurface. App-wide MainLayout database dialog,
topbar and profile coordinator are explicitly retained shell composition, covered by W2
and the application campaign. Their retention does not conceal a Workspace feature renderer.

The restored Configuration component run passes 22 cases. The final static enforcement
passes with 15,214 reviewed fingerprints: W3 only moves the case-insensitive validation-key
lookup from the old wrapper to the neutral renderer. Protected project closures remain
unchanged; Configuration's independent sandbox has five projects, one package and no native
assets. Web has 163 projects and 30 native assets; no unresolved edges or cycles were found.

| Scenario | Startup to hydrated controls | Razor edit samples (seconds) | Executed C# edit samples (seconds) |
| --- | --- | --- | --- |
| Original renderer / Web | 56.17 s | 5.09 / 3.92 / 3.27 | 4.04 / 2.85 / 2.69 |
| Extracted renderer / Web | 56.27 s | 5.18 / 3.93 / 3.67 | 3.40 / 2.71 / 2.82 |
| Independent Configuration sandbox | 7.61 s | 2.49 / 0.28 / 0.28 | 0.18 / 0.17 / 0.19 |

These are warm local observations with explicit reload, not a claimed Web speedup. All
probe bytes were restored and owned watch processes stopped. The initial Web probe fixtures
were rejected for a missing terminal node before any timing samples; their failures remain
recorded. Configuration owns no scoped CSS/JavaScript to probe.

## Application campaign repairs and scope

The campaign adds actual Quartz delivery with exact fire/version/input/file read-back and
replay through a fresh owner; real Workflow execution with only its external Responses
endpoint scripted; a TestLab record of that observed run and asset hash; and an Agent
conversation through the real MAF tool registration, approval, persistence and activity
stream. These deterministic cases make no external model requests. The separate live lane
uses an outbound admission proxy capped at ten requests per execution and forty across
all attempts, retaining its campaign journal across retries.

Two bounded defects were reproduced before repair:

- Scheduler treated the `route` navigation URL in a Project Structure asset result as its
  business outcome. The URL exceeded the existing 80-character outcome column; the
  completion transaction failed after the Workflow had created the file. Rooted navigation
  URLs are now ignored during outcome projection. Explicit business outcomes and summaries
  keep their existing semantics. The failing unit attempt had two failures and seven passes;
  the repaired nine cases pass, as does the actual Quartz/file/replay browser journey.
  The route reader predates this extraction series (`b3b08b71e3`, May 2026); the isolated
  before/after evidence proves this repair, not a claimed historical runtime regression.
- Agent pending-approval cancellation created a new activity operation but left the UI
  subscribed to the previous approval operation. After an exact cancellation receipt and
  the existing view/profile checks, the host now selects that cancellation operation in
  the original stream scope. The failing component regression and its repaired 12-case
  cancellation suite are retained. The affected method originates in `e8ac2aaf9b`; this
  finding was reproduced on the campaign entry implementation.

Visual inspection also found the floating conversation header squeezing its title beside
the actions. Its shared CSS now wraps the action group; production browser assertions
check title width and horizontal overflow. This presentation-only change does not alter
conversation identity or effects. See the [findings index](workspace-regression-findings.md)
for original attempts, attribution and remaining infrastructure prerequisites.

The Agent transcript currently presents the final persisted message after a turn;
intermediate phases use the activity stream. The initial deterministic browser assertion
incorrectly required token text before completion. The corrected journey observes the
actual responding phase while the provider response is held, then requires the final text,
approval receipt, file bytes and reopened history. No new token-streaming feature is added.

The inert runtime package fixture proves registration and a harmless real invocation with
its owner log. Its minimal executor does not implement a grant evaluator, so it is not an
authority-denial oracle. The denial assertion instead invokes the shipped Office365
executor with its Workflow grant explicitly denied, before any OAuth/network work. The
original invalid fixture assertion remains in the attempt history. No plugin capability
or production grant behavior was changed.

The live Workflow investigation also reproduced a missing handoff of the existing
`WorkflowModelSettings.MaxOutputTokens` value. The bounded repair sends it through the
existing lightweight model-parameter envelope. The failing-first limit case received
null instead of 150; the repaired limit and unset-limit cases pass. The actual Workflow
browser journey checks `max_output_tokens=150` at its scripted external HTTP boundary,
then verifies the managed asset and records that run in TestLab. Both the neutral port
and provider driver keep their existing contracts. The final full Stable checkpoint includes
this runtime edit; the preceding successful Unit/leaf runs are preserved as partial proof.

Full Stable, the non-quarantined browser inventory and live results are separate gates;
focused passes above do not declare the application ready. Raw attempts and source hashes
are retained under `artifacts/workspace-completion/20260930-1080c2416`.

## C# architecture gate result

Status: **Pass with follow-up** for the Workspace boundaries. The separate application
gate retains the failed lifetime and live-validation groups.

| Check | Evidence and decision |
| --- | --- |
| Responsibility | Recovery rendering/session and Data Sources rendering/editor/transfer state are separate from their real owners. The original 322-line Recovery and 1,320-line Data Sources components are composition hosts. No business service was moved into a UI assembly. |
| Dependency direction | Neutral contracts have no implementation references. Each new leaf uses its contracts and real BaseLib; the existing Configuration.UI family owns the generic field loop. Evaluated graphs have no cycles/unresolved references. Protected Core/API/Catalog/Selection project, package, edge and native-asset sets match entry. |
| Construction | Module DI composes owner adapters; sandboxes supply explicit scenario owners. No new domain service locator, provider SDK or registration-time service-provider build was introduced. |
| Independent testability | Recovery passes 20 leaf cases and Data Sources passes 21 without production services. Real wrapper/owner/browser cases separately prove composition, refusal, late completion, exact origin and partial progress. |
| Extension seam | New renderer behavior belongs in the leaf/session. A new backend action requires an explicit neutral command and original owner policy. The trusted configuration host still rejects invalid renderer claims. |
| Partial-class policy | The Workspace renderer boundaries are unchanged. The existing MainLayout host has a bounded lifetime companion; no new feature layer or runtime project is introduced. Top-level test-support owners and separate acceptance classes replace the temporary live fixture partial scaffold; exact moved identities are reconciled. |

CodeAnalytics, Components MCP and dotnetwatch MCP were unavailable in this session.
The recorded fallback uses source inspection, evaluated MSBuild/NuGet graphs, assembly
and public-contract tests, actual source/published browser composition and owned CLI watch
probes. No tool snapshot or dashboard-health result is invented.

The UI uses the existing component library and desktop composition. Recovery has a single
scrolling modal body with reachable actions; Data Sources keeps the acquired editor and
explicit transfer overlay, with group progress visible after failure; Configuration retains
the caller's fields, focus and raw validation state. Normal and open-overlay screenshots
were inspected separately from assertion results. The live capture review also records
blank/loading frames and icons as inspected media, never as proof of a hydrated feature.

### WC-H1: acceptance-fixture ownership closed

The closure removes the temporary `CrmHrLiveAgentToolUiSmokeTests` partial scaffold.
`LiveUiHost` owns the private runtime and two independent stop controls; `UiEvidence`
collects retained owner/HTTP/provider evidence; `AgentUiJourneySupport` and
`ProjectFilesUiJourney` own their respective orchestration; `ScriptedAgentUiFixture`
owns the external scripted control. The support partial declaration needed by
`GeneratedRegex` is compiler plumbing, not a growing fixture split.

Project Files, Workflow, Agent runtime, file-harness and shared-host journeys now have
separate test classes. The two original CRM/HR identities remain; ten moved identities
are mapped in `test-identity-moves.json` under the ignored closure evidence root.
The complete final browser inventory executes the deterministic budget, approval,
timeout, retained-evidence and actual MAF/Workflow controls. No production project,
renderer ownership or provider abstraction was added. Live rows stay authorization-blocked.

See the [current closure report](workspace-critical-fixes-closure.md) for exact source,
proof, dependency delivery and remaining application readiness limits.
