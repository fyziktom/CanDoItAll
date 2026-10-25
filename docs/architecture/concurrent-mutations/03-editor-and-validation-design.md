# Concurrent mutations: editor and validation design

Status: architecture review, not implementation or passing runtime evidence. Reviewed
2026-10-16 against `ead988abeb4cfb3e12341e618362bfa3dde5df29`. The architect package is
design input; its proposed phases, tests and performance numbers are not evidence that
the current application implements them. This review covers its documents 08, 11, 16,
21 and 22 and the current Projects editor, API, owner and validation seams.

## Decision

Extend the existing Projects editor lifetime and acknowledgement contracts. Do not add
a shared mutable editor service, import CRM implementation helpers into Projects, or
make the renderer reference persistence. A content revision protects the definition
scalars, phases and options together initially. Project lifetime/admission remains a
separate prerequisite. Search, activity, hierarchy and runtime progress must not silently
become additional members of that edit revision.

The first safe UI supports preserved dirty text, a separate current-state view, explicit
discard/reload and deliberate manual reconciliation against the state actually reviewed.
It has no force action, automatic merge or automatic retry of a rejected write. A later scalar merge
policy can propose changes, but the owner must validate the resulting conditional command.

## Current evidence and evolution since the package

All paths in this table are repository relative.

| Source | Current behavior | Design implication |
|---|---|---|
| `src/UI/CanDoItAll.Projects.UI/ProjectEditorDraft.cs` | Owns the mutable model, stable `EditContext`, field revision counters, frozen submission values and original phase/option row references. `Acknowledge` adopts owner-generated IDs and accepted values without replacing fields edited after dispatch. | Preserve this implementation. Add the accepted base/content revision and typed conflict state; do not copy the CRM reconciler into this feature. |
| `src/UI/CanDoItAll.Projects.UI/ProjectModalHost.razor` | Captures the submission before asynchronous browser validation; uses the same form for both save actions; fences retired forms and retains raw invalid dates across wizard steps. | Conflict work must preserve those guarantees and allow existing typing during save. Freezing the whole form would change a proven interaction contract. |
| `src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor` | `AcquireEditorAsync` uses generation/cancellation fences; same-target acquisition is inert. `SaveCoreAsync` separates committed project save from starter-object seeding, holds pending/unconfirmed targets and prevents late navigation into a successor editor. | Add recovery through this owner. A blanket reload after save or agent completion would regress editor ownership. |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectEditorAcknowledgement.cs` | Acknowledgement contains exact normalized scalars and accepted child IDs paired by submitted index. | Return the accepted content revision in this acknowledgement. Submitted indices identify frozen submission slots, not the current display position. |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectPortfolioModels.cs` | `ProjectEditorModel.ExpectedProjectAdmission` is server-only `JsonIgnore`; `ExpectedLifetimeId` is public but optional. | A mandatory public content precondition is needed. A server-only admission cannot protect API clients. |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs` | `GetAsync` reads root, phases and options in three queries without a read transaction; `SaveWithReceiptCoreAsync` loads the current row and replaces lists without a content precondition. Unknown child IDs currently become new rows. | Make the editable read coherent and reject stale replacement atomically. Validate duplicate/foreign/unknown child IDs explicitly. |
| `src/App/CanDoItAll.Web/Api/ProjectsApi.cs` | `SaveProjectAsync` uses `SaveAsync`; current published remarks explicitly describe last-save-wins and return only a GUID. | Change the wire contract deliberately, including missing precondition and conflict outcomes. The old GUID alone cannot carry accepted revision or durable recovery identity. |
| `tests/Components/CanDoItAll.Projects.UI.Tests/ProjectsBoundaryTests.cs` | Traverses compiled references, checks public contract types and forbids injected owner services. | Extend the existing guard if a lightweight protocol assembly is introduced; never allow EF/Infrastructure/MAF/Web through an indirect reference. |
| `src/Sandboxes/CanDoItAll.Projects.UiSandbox/ProjectsScenarioStore.cs` | Backend-free scenarios already exercise save refusal, held reads/saves, unknown seed and partial deletion. | Add typed content conflict, unavailable conflict read, third-writer and recovery states here using the real renderer. |

The package predates some of these Projects UI improvements. Its conceptual B/L/S/C
distinction is still correct, but implementation should extend the current feature's
field revisions and native acknowledgement rather than prescribe a fresh UI framework.

## State, snapshots and transitions

An editor lifetime owns four different values:

1. Base: immutable accepted content and revision this editor continues.
2. Live draft: current mutable values and the existing `EditContext`, including raw
   validation state that a typed DTO alone cannot represent.
3. Submission: frozen normalized command, original precondition, operation identity,
   child correlation and field revisions captured before dispatch.
4. Current observation or accepted result: explicitly distinguish a current conflict
   read from the exact result of this operation. A newer read-back is not its receipt.

Conflict comparison uses base/live/current. Postcommit reconciliation uses
submission/live/accepted. These are separate algorithms and cannot be replaced by
"set the new token and reload".

| Transition | Required state change |
|---|---|
| Clean acquisition | Adopt coherent base and content revision; create the draft once. |
| Wizard/overview switch | Preserve the draft, `EditContext`, raw inputs, base and revision. |
| Save dispatch | Freeze command and operation identity; admit one write for the editor lifetime. Keep later typing separate. |
| Validation refusal | Preserve draft and base; release the write gate for correction. |
| Content conflict | Preserve draft, validation and original submitted precondition. Expose a typed conflict state and a separate current-state read action. |
| Conflict read fails | Retain the prior draft and any previously accepted current observation with explicit stale/failure state. Retry reads only. |
| Explicit discard/reload | Clearly identify that local changes are discarded; adopt the displayed latest state as a new base. Fence a late result if the target or lifetime changes. |
| Deliberate resolution | Form a new operation with a new identity and the revision of the state actually reviewed. Never substitute a fresh revision underneath the original command. |
| Another writer during resolution | Reject again; preserve both the deliberate proposal and the updated conflict state. No force flag. |
| Known commit | Adopt exact accepted identity/revision and reconcile untouched values, preserving post-dispatch edits and original child row identity. |
| Known commit plus refresh/projection failure | Keep success identity and a visible warning; retry reconciliation reads or separately identified secondary work only. |
| Unknown outcome | Keep the original operation identity and draft. Disable blind repeat. Resolve that operation through an authorized lookup; absence must have a defined meaning before replay is allowed. |
| A to B to A or dispose | Retire the original generation. Its completion may resolve its operation but cannot replace the new draft, clear its busy flag, reopen a dialog or navigate. |

Dirty is not equivalent to `EditRevision > 0`: revisions are also useful for detecting
editing back to the submitted value, while visible unsaved state must compare against
the accepted base. Keep these meanings separate. A conflict read is not authorization to
expose all stored fields; retrieve only values this caller is currently permitted to read.

## Collection rules

The initial replace command protects scalars/phases/options with one revision. Retain
submission-slot-to-live-row pairing for acknowledgements. Require unique persisted child
IDs owned by the same project; a nonempty unknown ID is an error, not a request to create.
New rows have local submission identity and adopt the returned persisted ID. A row removed
while saving must not be resurrected; a later local row must not receive an earlier row's ID.

Do not use array indices for conflict matching between independently read versions.
Persisted ID is the match key. New local rows need an explicit local identity or the
existing captured row reference within one editor lifetime. Reorder, add, edit and delete
are distinct intentions when a future narrow command family is adopted. Automatic
whole-list union would silently undo deletion and is excluded.

Blank option categories are presentation defaults. Their materialization, normalization
and no-op rules must be stated at the owner. Invalid raw dates never become successful
"no change" saves merely because the typed model retained its earlier value.

## Caller perimeter and cutover

This is the inspected Projects definition perimeter, not a claim that all engine writers
are already protected. Every entry must be converted, proven create-only/maintenance, or
refused before advertising Projects definition protection.

| Caller or write surface | Required participation |
|---|---|
| Projects page `SaveEditorAsync` | Original public content revision plus existing captured project admission; typed acknowledgement/conflict. |
| Workbench project creation host `Pages/ProjectStructurePage.ProjectCreation.cs` and `ProjectStructureAuthoringOutcome.cs` | Preserve native creation acknowledgement, reservation identity and postcommit starter effects. Creates do not invent an update precondition. |
| HTTP `ProjectsApi.SaveProjectAsync` | Same owner command/precondition/outcomes; no adapter fetch of a current revision. Raw JSON missing/empty/malformed preconditions tested. |
| `ProjectStructureAgentService.SaveProjectAsync`, `BuildProjectEditor` and `ProjectStructureAgentContracts.ProjectStructureProjectSaveRequest` | Original decision/read revision must survive agent request-to-editor mapping. Existing project mutation lease/authority is separate and does not establish content freshness. |
| Agent native/tool/HTTP/process/workflow adapters reaching that service | Trace actual request serialization and admission through the real registered tool. A DTO unit test alone does not prove the perimeter. |
| `ProjectCreationReceipt.cs`, reserved/root/subproject creates and CRM opportunity conversion in `CrmHrServices.cs` | Preserve create-only semantics, reservation and compensation fingerprints. Durable retry identity must not be confused with an in-memory creation acknowledgement. |
| `ProjectStructureSubprojectTransferCoordinator.cs` | Preserve exact source/target admissions and declared transaction; never refresh a proposal's source revision while moving. |
| `DevelopmentDiagnosticsEndpoints.cs` | Verify current calls remain create-only setup. Do not leave an enabled development update route bypassing mandatory preconditions. |
| `DatabaseTransfer/ProjectsProfileTransferStore.cs` | Explicit serializable maintenance session already required. Imported roots/children need new local live revisions; source approvals and operation receipts remain historical. |
| Delete/compensate and hierarchy attach/remove/reconnect in `ProjectModels.cs` | Retain lifetime and graph protections. Define separately whether deletion requires the observed content revision; do not imply an edit token protects graph predicates. |
| Search projection and optional activity callbacks | Do not bump the definition revision. Prevent delayed projection of old content from overwriting newer content; report known commit independently from callback errors. |

Searches of active source found phase/option writes in the owner replacement path,
deletion and profile transfer. This is source evidence for the current pilot, not a
permanent guarantee against future raw SQL or bulk writers. Maintain the inventory and
atomic child/root regression whenever new writers are added.

## HTTP and separate SPA compatibility

Read-only inspection of sibling `CanDoItAll.SPA` found a real incompatible caller:

- `packages/api-client/src/models/models.ts` defines `ProjectEditorModel` without even
  the existing `expectedLifetimeId`.
- `packages/api-client/src/models/internal/serializers.ts` explicitly enumerates fields
  in both transport directions, so unknown revision properties are dropped.
- `packages/app/src/pages/projects/ProjectOverviewPage.vue` copies editable values into
  a new payload and performs a silent full reload after save and hierarchy changes.
- `packages/app/src/pages/projects/api.ts` flattens error codes to `Error(message)`;
  `saveProject` expects a GUID. `ProjectNewPage.vue` also assumes that create response.

Therefore regeneration alone is insufficient. The SPA editor must retain the original
revision, freeze submissions, preserve dirty state, understand typed conflict/unknown
outcomes and adopt the accepted acknowledgement. The server must refuse tokenless
updates during the transition. Do not add a compatibility branch that fetches a token
and writes on an old client's behalf. Until separately coordinated and verified, report
SPA mutation compatibility as outstanding; keep this sibling read-only in this change.

Choose the public version/route migration explicitly in the overall ADR. Whatever its
shape, return structured conflict codes, expected/current safe revision metadata,
operation identity and exact accepted result. A content edit revision is not a strong
ETag for a dashboard containing independently changing tasks and related modules.

## Realtime and runtime policy presentation

Correctness does not depend on notifications. An optional same-target invalidation may
refresh a clean editor and mark a dirty one stale. Coalesce bursts, bound subscriptions
and release them on retirement. Do not broadcast confidential record contents or reload
every portfolio card for one update. Existing agent completion refreshes the portfolio;
retain its separation from the acquired draft.

Runtime settings should separately describe input observation and publication behavior:
keep the starting snapshot, refresh at a safe boundary, or replan after relevant changes
is a planning choice. All modes still conditionally publish into current owner state and
recheck current authority/active attempt. The UI must show a bounded replan limit and
deadline and distinguish stale input, stale target, revoked authority, approval mismatch
and genuinely unknown effect. "Keep snapshot" never means overwrite current data.

## Validation and performance acceptance

The executable case mapping is in the bundle's
`architecture/04-csharp-testability-plan.md`. No new cases, builds or benchmarks were
executed for this review. Existing test names are discovery seeds only.

Hard structural requirements are: one bounded coherent aggregate read; no portfolio or
whole-workspace reads for a single edit; no external HTTP/LLM/human waits under database
locks; same-target interference cannot serialize all projects; rejected writes leave no
domain/child/projection effect or success receipt; missing notifications cannot admit stale writes; and
field/row work is linear after validated identity maps are built.
An owner may retain a minimal rejected-operation result for deterministic recovery,
provided it cannot be mistaken for a committed domain effect.

The architect's +15% uncontended p95 latency and -10% disjoint-project throughput figures
remain investigation thresholds pending matched baseline measurements. Record absolute
latency and noise when the baseline is small. Sweep 5/7 normal phases/options and
10/100/1000 collection sizes only within declared request limits, with concurrency
1/8/32/64 and one/two host processes. Report SQL command/row counts, bytes, p50/p95/p99,
lock and pool waits, transaction duration, allocations and conflict rate. Match source,
SDK, siblings, database topology, data and warmup between comparisons.

UI capacity proof measures repeated open/close, held saves with continued typing,
large rows and conflict refresh bursts. Check retained drafts/subscriptions, allocation,
rerender counts and interaction latency at 1920 by 1080. A backend-free sandbox proves
rendering; real two-editor browser proof must also inspect the persisted owner result.

## Decisions still required before implementing dependent behavior

1. The overall design selects typed body edit stamps and explicit rejection of missing
   update preconditions. Finalize the rollout/cutoff and exact supported client matrix.
2. Durable operation result/retention contract, including how an unknown submission is
   resolved after restart and how an expired ID is refused.
3. Delete content-precondition policy and explicit division between edit revision and
   graph/set witnesses.
4. Request/collection/basis limits based on measured costs, rather than unbounded lists.
5. The first slice offers inspect/discard/manual reconciliation with no force or automatic
   merge. A future scalar merge proposal requires a separate owner-validated policy.

These are design choices, not requests to weaken existing checks. Resolve them in the
overall architecture record before their implementation slice; preserve unresolved
coverage explicitly rather than labelling the whole engine concurrency-safe.
