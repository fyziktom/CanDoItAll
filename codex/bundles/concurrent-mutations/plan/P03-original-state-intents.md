# P03: original state for project deletion and hierarchy

Status: source-reviewed design refinement; no P03 production changes or test executions.
The ordered implementation and durable recovery decisions are in
[P03 durable lifecycle checkpoints](P03-durable-lifecycle-checkpoints.md). That card
supersedes the former suggestion to add recovery after exposing these new intents:
each externally callable intent must ship with its exact durable acknowledgement and
recovery adapter in the same cutover.
The implementation scope stays with existing operations. There is no demonstrated need
to add a generic field patch language, phase-by-phase mutation API, or whole-hierarchy
replacement command to finish this slice.

## Current owner behavior and gaps

| Existing path | Current protection | Remaining original-state gap |
| --- | --- | --- |
| `ProjectsService.DeleteAsync` / `DeleteCoreAsync`, `ProjectModels.cs:764` | Serializable owner scope, project key, hierarchy key, participant preparation keys, optional lifetime admission, retirement record and durable participant cleanup | Root is loaded afresh; optional admission says which lifetime can be written, not which definition the user reviewed. A stale editor can delete newer content. EF's root concurrency token only fences changes after this new read. |
| `AddSubprojectAsync`, `ProjectModels.cs:273` | Serializable, sorted endpoint keys, global hierarchy key, source authority, optional endpoint admissions, cycle check; existing edge is a successful no-op | Callers can omit endpoint lifetimes. An explicitly idempotent ensure-link intent needs original namespace/lifetimes, not a root scalar revision or an artificial edge-absence revision. |
| `RemoveSubprojectAsync`, `ProjectModels.cs:347` | Serializable, endpoint/hierarchy keys and source authority | Endpoint pair alone can remove a different edge recreated after the original observation. Public callers do not supply an original edge identity. |
| `ReconnectSubprojectAsync`, `ProjectModels.cs:398` | Serializable, all endpoint/hierarchy keys, optional admissions, cycle check; creates target edge if needed then removes original | Same edge ABA gap; current root lifetime captures do not identify the originally observed relationship. |

`ProjectHierarchyLink` already has a generated Guid `Id`, endpoint uniqueness and
`CreatedAtUtc`. Existing hierarchy read DTOs discard `Id`: `ProjectHierarchyLinkSummary`
contains endpoints/time, and `ProjectHierarchySnapshot` contains parent/child summaries.
Use the existing identity as the relationship witness. Do not add an unrelated global
hierarchy revision unless a future operation actually replaces the entire graph.

## Minimum command contracts

**Delete:** require the original dataset namespace, project lifetime and root definition
revision from the editor's coherent opening. Validate these before participant
preparation, reservation cancellation, search/routing deletion or any effect. Preserve
existing participant cleanup preparation/commit/recovery. A currentness rejection has
no deletion side effects; a committed deletion with pending cleanup keeps the existing
distinct cleanup result. Missing expected state cannot mean force-delete or cause a
dispatch-time read to manufacture an expectation.

This expectation protects the Projects definition that was reviewed. It does not
claim that every file, workflow run, agent record or Workbench object is unchanged.
Deletion remains a lifecycle termination that intentionally removes currently owned
data under the existing participant contract. If the product requires a reviewed list
of exact cross-module contents, each affected owner must supply its own witness; a
Projects root revision cannot invent that guarantee. An explicitly authorized force
deletion would be a separately named administrative operation, not an optional token.

**Add link:** preserve its existing ensure-present semantics. Capture namespace and
both endpoint lifetimes. Under the current hierarchy scope, validate authority and
lifetimes, check cycles, then create the edge or return the existing edge identity.
An accepted result identifies the exact resulting edge. Returning already-present
states current postcondition; it is not proof that a lost previous request committed.

**Remove link:** carry the originally observed edge `Id`, endpoint pair and both
lifetimes in the original namespace. An endpoint-matching replacement edge with a
different `Id` is a conflict and survives. Preserve the existing missing-edge refusal
for a new remove command; do not introduce a new already-absent success. An exact
accepted replay succeeds from its receipt even when the edge is now absent. Current
absence alone is not an operation receipt.

**Reconnect:** carry the original source edge identity and original lifetimes for
child, old parent and new parent. Validate them atomically under all current sorted
keys and the hierarchy key, preserve an existing target edge when appropriate, and
remove only the original source edge. A different source edge must not be silently
adopted. Preserve the current no-cycle and target identity rules. A scalar project name
change alone must not reject this relationship intent.

Source/current authority remains mandatory at execution. A snapshot is data, not
permission; neither a retained lifetime nor an edge identity replaces current source
lease, endpoint admission or disclosure checks. Expectations come from the original
read and are never refreshed by an execution adapter.

## Caller cutover inventory

| Caller | Required minimal adaptation |
| --- | --- |
| Native `ProjectsPage.razor:1078` | It currently captures `ExpectedProjectAdmission` only. Use the coherent original definition token and namespace for the confirmation/dispatch; preserve any later editor typing and distinguish deletion accepted/cleanup pending from rejection or uncertainty. |
| HTTP `ProjectsApi.cs:254` | Delete currently accepts route ID only. Require an explicit original expectation; old request shape refuses. Existing cleanup-pending response remains distinct. |
| HTTP hierarchy endpoints `ProjectsApi.cs:431`, `:456`, `:479` | Require typed original endpoint/edge expectations with presence validation. Expose edge identity from a coherent owner read before clients can use remove/reconnect. |
| Native `ProjectStructurePage.ProjectHierarchy.cs:69` | Dialog currently captures endpoint admissions but no edge ID. Preserve the edge observed by the surface/dialog and its endpoint lifetimes; do not replace it from a new dispatch read. Avoid claiming separate project-list and edge-list queries form one coherent opening. |
| `ProjectStructureAgentService.ChangeSubprojectAsync:224` and `ProjectStructureSubprojectChangeRequest:317` | Current request has child/current-parent IDs plus lease token. Add original relationship witness; keep lease/source authorization separate. |
| `ProjectStructureAgentRuntimeToolProvider.ProjectStructureHierarchyGetAsync:991`, link helper `:1017` | Return edge witness in governed hierarchy data and preserve it in the proposal. Existing just-in-time endpoint admission capture is authority validation, not original relationship observation. |

Ordinary deletion has the native and HTTP callers above; no additional agent ordinary
delete entrypoint was found in this scoped search. Creation compensation is separate:
`ProjectCreationReceipt.TryCompensateCreationAsync` checks original reservation,
lifetime, creation fingerprint and cross-module compensation guards. CRM and Workbench
compensation callers retain that narrower witness and must not be routed through an
ordinary delete with manufactured current state.

Other hierarchy writers are child creation, incident-edge deletion and profile import.
They retain their own creation/deletion/import semantics. They must continue to obey
the same hierarchy keys and preserve/allocate identities intentionally; edge IDs must
not be regenerated by ordinary reads, editor mapping or replay.

## Execution and performance boundary

Keep the current source-authorized Serializable protocol and existing hierarchy key
for this first cut. `ValidateHierarchyConnectionAsync` currently reads the complete
edge set and performs graph reachability in memory while holding that key. This is a
real scalability limit, but replacing it needs measured graph size/query plans and a
proof against cycles created jointly by disjoint endpoint commands. A root revision
or endpoint-only lock cannot supply that proof. Do not downgrade this graph transaction
because source-null definition replacement uses ReadCommitted.

New protected deletion/hierarchy entrypoints take the selected dataset admission
before owner/table effects and respect source-authority lock order. Freeze bounded
typed payloads and reject excessive capture before copies/serialization. The commands
above are small; no unbounded graph payload is accepted. Reuse existing owner helpers
and extract only the responsibility that actually outgrows `ProjectsService`.

Operation recovery is part of each caller cutover within this owner wave. The current
`ProjectDefinitionOperation` kind/schema/digest/receipt cannot certify hierarchy or
external deletion cleanup. Add only the necessary owner-local command codecs and
exact acknowledgements with each intent; do not introduce a generic
mutation coordinator or broaden the definition codec by silently accepting other
effects. Until durable recovery is supplied, an uncertain hierarchy/deletion dispatch
cannot be auto-retried with a fresh ID, and current state is not receipt evidence.

## Decisive tests before closure

- A stale editor's delete after another accepted definition save rejects before any
  deletion participant prepares; newer root/children/search/routing survive.
- Current delete succeeds; a participant cleanup failure remains committed cleanup
  pending with the original recovery IDs. Creation compensation still refuses after
  changes and is not weakened by the new ordinary-delete contract.
- Remove/reconnect after edge remove-and-recreate rejects the replacement edge even
  with unchanged endpoint IDs and lifetimes; unchanged original edge succeeds.
- A root scalar change does not reject an otherwise current relationship command.
- Endpoint delete/recreate, namespace reactivation and current source revocation
  reject old expectations; no dispatch-time read refreshes them.
- Concurrent disjoint endpoint additions that jointly form a cycle cannot both win;
  retain the current barrier-driven hierarchy test and exact existing edge identity.
- Native opening/selection changes preserve the original submission and later edits;
  HTTP omissions fail before effect; governed proposals carry the observed edge ID.
- Recovery tests separately distinguish exact accepted operation, authoritative
  retained absence, current postcondition and cleanup state before claiming retry safety.

These are behavior tests, not a reason to add a method or interface for every field.
The implementation wave must publish exact discovery, scoped execution, portability
enforcement, boundary review and a commit before the next owner is declared protected.
