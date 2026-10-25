# Native Workbench and SPA cutover: next ordered slice

Readiness review, 2026-10-16. Sections 1–3 now have a source-only Workbench
implementation in the managed continuation checkout; see the exact scope and
unmet proof arrangements in [Workbench create validation](../validation/P02cd-workbench-create.md).
The instructions below describe the intended cutover, not completed runtime proof.
SPA generator/asset preparation is separately committed and validated; its writer
cutover remains a plan. Repair and validate HTTP schemas before generating its client.

## 1. Reuse the protected native root-create contract

`src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ProjectCreation.cs`
previously reserved an independent **root** (no parent argument), called legacy
`CreateEditorAsync`, then offers the created project for later hierarchy selection.
There is no atomic create-and-attach guarantee to preserve. Use the existing
`ProjectNativeDefinitionOperations` LocalWorkspace create path and take the
project ID from its exact acceptance. Remove this caller's preselected project ID
and reservation; retain the authoring opening/submission IDs only as presentation
correlation. Do not change governed agent/reserved creation or node-conversion
callers. No new owner DTO, receipt kind, schema or owner dependency is needed.

Acquire through `ProjectNativeDefinitionOperations.AcquireEditorAsync(null)` when
opening, and construct the draft with that original scope. Keep its before/after
dataset checks. Initialize/read the shared pending store before enabling dispatch;
an unresolved root create blocks another root create consistently with Projects
page. Keep raw bounds validation before `ProjectEditorDraft.Capture` via existing
`ProjectModalHost.ValidateCapture`, and preserve the original authoring opening
as the navigation/selection fence.

## 2. Add the narrow Workbench session orchestration

Reuse `ProjectEditorOperationSession`, `ProjectPendingOperationSession`,
`IProjectPendingSubmissionStore`, limits and native codec/adapter registrations.
Prepare durable frozen input, require an applied dispatch claim, then dispatch.
Remember exact acceptance before acknowledgement, outcome rendering or refresh.
Wire the existing modal's `PendingRecovery`/`PendingAction` parameters for explicit
check, same-operation retry, reconciliation, safe prepared cancellation and finish.
Retain live typing and raw starter plans; restored frozen records do not contain
later typing. Reuse the Projects page as the full recovery inventory after close
or restart, with a visible navigation action; do not duplicate its administration
surface or introduce a generic Workbench transaction manager.

Prior defects this removes: `Acknowledge` preceded `outcome.Editor`, so a client
reconciliation exception loses known acceptance; seeds may begin after the modal
closes because the current opening is checked only at publication. Use the shared
session's known-acceptance and `CanSeed`/Begin/Complete/Refuse transitions. Seed only
on the live original, first accepted dispatch; never on replay, restored recovery
or after retirement. An unknown seed result keeps definition acceptance and its
hold. Hierarchy refresh/selection remains a separate read; attaching stays an
explicit existing hierarchy command.

Retire sessions in all four places: close handler; `ActionContexts.cs`
`RetireInactiveAuthoring`; route/project-change reset in `ProjectStructurePage.razor`;
and `DisposeSynchronousState` before awaiting `authoringOperations`. Retirement
does not clear evidence or infer rollback. Late completion may retain historical
acceptance but cannot seed, navigate, refresh a successor or replace its draft.
Keep the existing bounded authoring outcome queue informational; it is not the
durable recovery store. Change only these host seams and relevant rendering
register entries; shared renderer owns no authority/storage.

## 3. Workbench proof before caller closure

Extend `ProjectStructureProjectCreateLifetimeTests` (currently **two** theory
rows). Its old expectation that a closed draft is automatically acknowledged must
become retained exact acceptance without successor mutation; unresolved evidence
must prevent a fresh create even if a replacement editable draft is displayed.
Add focused arrangements in that file or a dedicated adjacent pending test file:

| Arrangement | Required assertion |
| --- | --- |
| Storage prepare failure / lost claim acknowledgement | Zero owner calls; original operation retained; no fallback |
| Delayed owner acceptance, later typing | Accepted IDs/token reconciled once; later fields/rows/raw input survive |
| Close, route change, selection change, dispose while pending | No late seed/navigation/successor refresh; retained reference recoverable from Projects |
| Actual commit then response/persistence failure | One root/receipt; lookup restores historical ack; no second root/reservation |
| Known acceptance then malformed ack | Terminal reconciliation failure retaining exact result, never SaveOutcomeUnknown |
| Seed refusal/unknown/replay | Definition remains accepted; unknown held; recovery/replay never seeds |
| Opening namespace changes / expired or closed epoch | No rebinding; explicit lookup distinctions and no fresh operation |

Reuse `ComponentTestHarness`'s explicit pending-store registration and existing
hierarchy/modal event seams. Add one real browser journey from Structure create
through page reload to Projects recovery, with exact database effects and no
automatic hierarchy link/seed. The implementation now contains 17 component cases
(the existing two amended and 15 added) plus one browser case. These are
source-derived expectations; the linked validation card identifies the
arrangements still missing at the host boundary.

## 4. Finish actual HTTP schemas before SPA generation

Extend only `ProjectDefinitionOperationOpenApiSchemas.cs` and its actual OpenAPI
integration proof. Existing epoch transport is a canonical decimal string and
must remain BigInt-free. Required schema audit:

| Type/shape | Actual required wire contract |
| --- | --- |
| `ProjectDefinitionExpectation` | Object with required `project` admission and nonempty `revision`; nested required `databaseProfileId`, `projectId`, `lifetimeId`. Never a scalar string. |
| Standalone `DatasetNamespaceId`, `ProjectCallerBinding` | UUID string / exact `pc1:` lower-hex string, including write-context members, not only the operation envelope |
| Request/result versions | Request `{schema, normalization}` object wherever used; result currently `{value}` object. Verify arrays in write context and accepted receipt. |
| Three commands | Create scalars + explicit full phases/options; Replace original `expected`, scalars and explicit full arrays; scalar update original `expected` + all desired scalars only. Preserve actual external-identity property names and optionality. |
| Scalars/children | Nullable dates, nullable new-row IDs, nonnullable stored/accepted IDs; numeric enum wire values; required fields/arrays versus explicit null/empty |
| Write/read/lookup responses | Literal status values and closed unions; nullable rejected operation; 201/200, 400/403/404/409/410/413/415/503 bodies; lookup callerBinding query; expectation + bound context on read |

Do not infer custom-converter shapes from CLR reflection. Existing generator
`fillEmptySchemas` converts unknown empty schemas to string; reject any protected
schema that would take that fallback. Compare actual emitted JSON and generated
serializer round trips, including scalar child preservation and full child order.
Owner canonical codecs and default numeric epoch serialization remain unchanged.

## 5. Generate and package one maintained browser store

In sibling `CanDoItAll.SPA`, use an explicit reviewed candidate OpenAPI input.
The former default generator fetched ordinary5032 and deleted its generated
source tree. SPA commit `2fe9e8ee1b6b7ce9e8777320b7daa19f895cfebb` replaces that
entry with explicit local reviewed input/hash and protected-schema preflight.
Use its documented process-scoped dependency-check guard, then the existing
prepare/TypeSpec/fix pipeline. Review generated models, serializers,
lookup query and response unions before changing Vue callers.

Use the existing ES module `Web/wwwroot/js/projectPendingSubmissions.js` unchanged
as the transactional engine. Preferred distribution is an explicit SPA build
input that copies this exact engine-owned module into the SPA artifact, with
source hash/version recorded and verification failure if absent/mismatched.
Do not fork its CAS logic or assume Vite's current `/api` proxy serves `/js`.
Choose the deployment asset route before coding this adapter. The native and HTTP
body formats differ: native frozen input/receipt uses owner serialization, while
HTTP epoch is a string. SPA must use a versioned, bounded HTTP-envelope adapter;
never feed HTTP strings to the native codec or reproduce the owner digest.
LocalWorkspace and HTTP caller bindings are distinct; same-origin global
metadata inventory must remain body-free across these scopes. Reusing the store
does not imply cross-client body interchangeability. Exact asset bytes, the
actual app build and a real Vite non-root URL check passed in the separate
[SPA preparation proof](../validation/P02d-spa-preparation.md); no generated
client or protected Vue writer has been enabled by that checkpoint.

## 6. Cut over the two SPA callers and prove transport behavior

Add a Projects-specific typed client/draft/session module beneath
`packages/app/src/pages/projects/api.ts`; Vue pages render intents. Capture bound
context on New opening and coherent definition/context on Overview opening.
Separate live draft, frozen command and historical acceptance. Use original
expectation for replacement and freeze the exact typed values; the owner remains
responsible for normalization. Never update tokens from a background read.
Hierarchy attach/detach refreshes
hierarchy only. Explicit current-based reconcile retains the old draft for copy.

Persist before dispatch; applied CAS only; quota/abort fails closed; no automatic
resume or fresh ID after uncertainty. Keep response timestamp strings exactly
(generated Date conversion loses precision), validate complete accepted mapping,
and exclude per-attempt `replayed` from stable retained acceptance. A restored
stored result requires fresh authorized lookup. The pinned
`@typespec/ts-http-runtime` 0.2.1 review found default SDK retries and a separate
browser redirect risk; see [transport cutover review](P02d-spa-client-cutover-review.md).
Use a dedicated protected client with zero SDK retries and its supported HTTP
client seam to reject redirects. Verify actual generated-client sends and the
original owner identity/receipt behavior before enabling protected writes.

Proof: generated shape/enum/null/epoch/timestamp round trips; all closed outcomes;
transport 503/disconnect sends once; delayed typing/row edits and route fencing;
two-tab same-operation CAS; quota/abort zero dispatch; auth/restore scope change;
actual two-browser conflict; lost acceptance then restart lookup; historical ack
after a later edit; explicit ClosedAbsent versus expired/unavailable. Existing
`--passWithNoTests` scripts are not acceptance. Remove the legacy `saveProject`
usage in New and Overview only after these pass, then rescan all owner callers
before deciding which legacy facade/HTTP entry points can be disabled. Other
hierarchy/node/deletion/maintenance paths remain separately owned.
