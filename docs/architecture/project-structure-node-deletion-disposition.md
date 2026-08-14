# Project Structure node deletion disposition

## Decision

Deleting a Project Structure branch has two distinct, explicit outcomes:

- `RetainManagedFiles` removes editable nodes, bindings, links, assignments, and UI state while leaving every backing storage object untouched.
- `DeleteOwnedManagedFiles` performs the same graph deletion and also asks the existing managed-storage pipeline to delete unreferenced objects whose current ownership can be proved.

The choice is represented by the closed `ProjectStructureManagedStorageDisposition` enum. `Unspecified` is invalid at the HTTP and agent-tool boundaries, so external callers cannot receive an implicit destructive default. Existing in-process compatibility overloads on the Workbench services and batch coordinator preserve their historical behavior by delegating explicitly to `DeleteOwnedManagedFiles`; new user- or agent-originated paths must call the disposition-bearing overload.

Confirmation authorizes the selected outcome, but it does not weaken storage provenance, current-namespace, shared-reference, provider, or filesystem-confinement checks. A stale or malformed binding can therefore be removed with its node in `RetainManagedFiles` mode, while `DeleteOwnedManagedFiles` still fails before the graph commit when physical ownership cannot be established.

## Responsibility inventory

| Owner | Responsibility after this change |
|---|---|
| Blazor page and support dialog | Explain affected nodes/files and issue one explicit retain-files or delete-files command after confirmation. |
| HTTP and agent runtime adapters | Require the typed disposition, retain existing mutation approval/write-access policy, and map typed application failures. |
| `ProjectStructureBatchDeletionCoordinator` | Normalize a selection once, reduce selected descendants, propagate one disposition to every independent root, and retain truthful completed/failure evidence when roots finish independently. |
| `ProjectWorkbenchCrossModuleMutationService` | Own the branch deletion transaction, choose whether storage planning is in scope, and persist the disposition in the durable mutation payload. |
| Managed-storage planner and deletion service | For `DeleteOwnedManagedFiles` only, prove current ownership/liveness and perform conservative physical cleanup. |
| Durable mutation processor | Replay the persisted plan; a retry cannot replace the original disposition. |

The current Workbench module remains the correct project boundary. Projects does not gain a Workbench reference, and no new single-implementation interface or provider abstraction is introduced.

## Flow and dependency direction

`Blazor / HTTP / agent tool -> Project Structure application service or batch coordinator -> Workbench mutation service -> managed-storage infrastructure`

The enum is an application contract. UI code does not inspect storage provenance, and storage code does not decide user intent. Existing project references remain unchanged.

## Durable and compatibility rules

The initial `DeleteSubtree` payload records the disposition beside the exact deleted node keys and storage plan. Old payloads deserialize as `DeleteOwnedManagedFiles`, matching the only behavior that existed when they were written. Exact cleanup retries replay the payload. A public retry request that supplies a different disposition is rejected rather than silently changing already-committed cleanup semantics.

Projected read-only branches continue to use their existing hide semantics and do not acquire ownership over process history or projected files.

## UI contract

When confirmation is required, the existing shared overlay presents explicit actions:

- **Delete node(s) only** — preserves backing files.
- **Delete node(s) and files** — requests conservative cleanup and uses danger emphasis.
- **Cancel** — makes no change.

The same choice applies to every root in a multi-selection. The UI invokes the batch coordinator once instead of implementing a separate per-root deletion policy.

Independent roots do not share a database transaction. If one root commits and a later root fails storage validation, the batch returns a typed partial result with the completed node count and a safe per-root failure. Known storage-validation failures do not prevent later independent roots from being attempted. Unexpected failures stop further processing, but the same partial contract preserves evidence for work already completed. UI, HTTP, and agent callers therefore never receive a false all-or-nothing result.

## Rejected alternatives

- A `force` or `unsafe` boolean is rejected because confirmation cannot authorize deletion outside proven storage ownership.
- Catching provenance failures and silently retaining files is rejected because it would make a delete-files request appear successful with a different outcome.
- Putting the choice only in the component is rejected because agents and HTTP clients need the same semantics and durable audit.
- A strategy interface per disposition is rejected because two closed modes contain no provider-varying algorithm; an enum and explicit branch are smaller and clearer.
- A new project or partial class is rejected because the existing application and storage boundaries already point in the required direction.

## Testability contract

Focused tests must prove:

- `RetainManagedFiles` deletes graph rows without invoking storage planning, including when a binding points at a retargeted workspace root;
- `DeleteOwnedManagedFiles` retains the current provenance failure-before-commit guarantee;
- shared content is deleted only after its final binding when owned-file deletion is selected;
- one batch disposition reaches every independent root;
- mixed-root failures report completed nodes and typed failed roots without losing durable recovery evidence;
- single and multi-select dialogs expose both explicit outcomes and preserve/delete bytes accordingly;
- HTTP and runtime-tool contracts reject `Unspecified` and accept both defined outcomes under existing approval policy;
- durable retry replays, and cannot change, the persisted disposition.

Integration tests remain responsible for EF cascade behavior, durable cleanup, storage drivers, and HTTP/runtime serialization. Unit tests isolate disposition validation and batch propagation without a database, filesystem, page, or full host.

## Architecture evidence

The pre-change scoped CodeAnalytics snapshot is `snap-20260813201527-82ce8900`. It has no blocking diagnostics. The relevant dependency query found the existing UI/application/infrastructure path and no new project boundary is required. It also identified existing large-file hotspots, so this change must not add a partial file, nested architecture boundary, or broad helper type.

The final forced post-change snapshot is `snap-20260813220459-03508714`. It has no blocking diagnostics. Its remaining diagnostics are the existing analyzer limitations around factory-based dependency-injection registrations; the scoped complexity and dependency-cycle warnings are existing module-wide findings, not a new project reference or service boundary introduced by this change.
