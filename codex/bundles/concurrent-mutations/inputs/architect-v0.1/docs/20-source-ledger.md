# 20 · Source ledger

Evidence date: **28 September 2026**. Links are immutable repository snapshots where possible. `current` external documentation URLs can evolve; pin the relevant documentation/version when implementing. This ledger records observations, not executed application proof. A null blob hash means it was not recovered from the inspected tool response, not that the file was absent.

## SRC-01 · `AGENTS.md`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/AGENTS.md)

- Evidence type: repository-guidance.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `4d7ab165a035e032dcf327cf3e972316fb5b3d64`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Repository instructions require canonical UI seam guidance, focused validation and portability closure.

## SRC-02 · `docs/architecture/ui-component-seams.md`
[Source](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/docs/architecture/ui-component-seams.md)

- Evidence type: repository-guidance.
- Commit: `e55a780b36e75db39a05b7400408d8267d0f32d5`.
- Git blob: `da998e19c2206ef4396e04b20aeed6ac4634bbaa`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Host-owned drafts, submission reconciliation, outcome distinctions, independent read lanes and backend-free renderer boundaries already exist.

## SRC-03 · `src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs)

- Evidence type: selected-code.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `b911f122385a9f824af0a80d963b11440d9591a9`.
- Extent: Lines 1–240, 350–1145; not every partial implementation or caller.
- Observation: Selected project model/read/save/hierarchy/projection paths; nullable lifetime precondition, no editor content token, complete collection replacement.

## SRC-04 · `src/Foundation/CanDoItAll.Infrastructure/Persistence/SerializableMutationScope.cs`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Foundation/CanDoItAll.Infrastructure/Persistence/SerializableMutationScope.cs)

- Evidence type: selected-code.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `919ed8452d28e0836166bdc5084d21f2817bed37`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: 64 process semaphore stripes also used on its relational branch; Serializable transaction, sorted per-key advisory lock SQL; local locks released on disposal.

## SRC-05 · `src/Foundation/CanDoItAll.Infrastructure/Persistence/CoordinatedDatabaseTransaction.cs`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Foundation/CanDoItAll.Infrastructure/Persistence/CoordinatedDatabaseTransaction.cs)

- Evidence type: selected-code.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `14fb3f1e689e3b8271df47be6369f5505aeb4c6f`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Explicit shared connection/transaction, immutable profile binding and participation lifetime checks; InMemory not atomically transactional.

## SRC-06 · `src/Modules/CanDoItAll.Modules.Projects/ProjectMutationAuthority.cs`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Projects/ProjectMutationAuthority.cs)

- Evidence type: selected-code.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `9155dae3351b1fafca5061ddf614fc5081b9b8d0`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Source authority is acquired before transaction; authorized PostgreSQL path differs from striped scope; required authority and admissions rechecked before commit.

## SRC-07 · `src/Foundation/CanDoItAll.Infrastructure/Persistence/ApplicationManagedConcurrencyTokens.cs`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Foundation/CanDoItAll.Infrastructure/Persistence/ApplicationManagedConcurrencyTokens.cs)

- Evidence type: selected-code.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `deddf7ec06bbb9eb9f60f14f2efa46af380f0600`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: GUID stamping is available for tracked IHasConcurrencyToken entities; it does not discover aggregate child-only changes.

## SRC-08 · `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs)

- Evidence type: selected-code.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `e29798ec4909ae483434ef0ca9347c1dbc1d7ed7`.
- Extent: Lines 1–270; caller graph and BuildProjectEditor implementation still require refresh.
- Observation: Project mutation tools delegate to ProjectsService with lease/source authority; structure read filters after loading a full surface.

## SRC-09 · `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskDetailsService.cs`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskDetailsService.cs)

- Evidence type: selected-code.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `3cc40356c9d1f292ee91f2aaee224546d5868e65`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Task details preserve expected/proposed domain state, assignment revision and captured project admission; do not flatten into generic save.

## SRC-10 · `src/Modules/CanDoItAll.Modules.Projects/ProjectLifetime.CaptureMany.cs`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Projects/ProjectLifetime.CaptureMany.cs)

- Evidence type: selected-code.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `67d470a934c3562f395f85918dd4ff7251f188c4`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Existing bounded bulk lifetime reads and enlisted validation can be reused; validation must never silently truncate requested dependencies.

## SRC-11 · `docs/architecture/modules.md`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/docs/architecture/modules.md)

- Evidence type: repository-guidance.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `584045b2c2a0fd534f5edd4ac233096cd0699d2d`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Documented authoritative owners, typed integration ports, contracts/UI split, canonical schema composition.

## SRC-12 · `docs/testing.md`
[Source](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/docs/testing.md)

- Evidence type: repository-guidance.
- Commit: `e55a780b36e75db39a05b7400408d8267d0f32d5`.
- Git blob: `96912b58d97dd13a68f37012cadc123b3701e961`.
- Extent: Lines 1–230; reread full current document before implementation.
- Observation: Focused discovery counts and test lanes; isolated PostgreSQL 18; UI test lifetime guidance.

## SRC-13 · `docs/architecture/collaboration-ui-boundary.md`
[Source](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/docs/architecture/collaboration-ui-boundary.md)

- Evidence type: repository-guidance.
- Commit: `e55a780b36e75db39a05b7400408d8267d0f32d5`.
- Git blob: `not recorded`.
- Extent: Decision/lifetime/validation and most completion record read; no independent rerun of its reported tests.
- Observation: Collaboration extraction explicitly changes rendering/lifetime behavior, not database concurrency or durable idempotency.

## SRC-14 · `docs/architecture/modules-decoupling/COVERAGE.md`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/docs/architecture/modules-decoupling/COVERAGE.md)

- Evidence type: historical-document.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `not recorded`.
- Extent: Opening owner audit and earlier coverage sections; document explicitly historical.
- Observation: Historical owner coverage provides discovery seeds, not present-tense proof of passing tests.

## SRC-15 · `src/Modules/CanDoItAll.Modules.Projects/README.md`
[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Projects/README.md)

- Evidence type: repository-guidance.
- Commit: `7db3543ab437376baeca55089cb331fbe1b30483`.
- Git blob: `362adc5f9d065dd8c8c2625ba395f75badd95742`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Owner schema, lifetime, deletion, source authority, reservation/compensation and transfer-history guarantees to preserve.

## SRC-16 · `.github/copilot-instructions.md`
[Source](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/.github/copilot-instructions.md)

- Evidence type: repository-guidance.
- Commit: `e55a780b36e75db39a05b7400408d8267d0f32d5`.
- Git blob: `dde5c2fdbcb56b7b5f48dfa17ed74fda238c2e1a`.
- Extent: Whole named file, unless explicitly qualified.
- Observation: Pragmatic boundaries, owner tools, no silent fallbacks, preserve governance, focused tests and explicit stable-gate triggers.

## SRC-17 · `.github/workflows/ci.yml`
[Source](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/.github/workflows/ci.yml)

- Evidence type: configuration-excerpt.
- Commit: `e55a780b36e75db39a05b7400408d8267d0f32d5`.
- Git blob: `3db271f26908b570c3a3837da76eb7cfe6142262`.
- Extent: Lines 1–160; complete current workflow must be reviewed at phase entry.
- Observation: Inspected CI source dependency resolution and platform split, not whole workflow or executed checks.

## EXT-01 · EF Core concurrency
[Official/primary source](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)

Expected original concurrency values participate in conditional writes; application-managed tokens require explicit lifecycle choices.

## EXT-02 · EF Core transactions
[Official/primary source](https://learn.microsoft.com/en-us/ef/core/saving/transactions)

Sharing transactions between contexts requires a shared database connection and transaction.

## EXT-03 · PostgreSQL explicit locking
[Official/primary source](https://www.postgresql.org/docs/current/explicit-locking.html)

Advisory/row lock modes, transaction lifetime and deadlock considerations.

## EXT-04 · PostgreSQL transaction isolation
[Official/primary source](https://www.postgresql.org/docs/current/transaction-iso.html)

Statement versus transaction snapshots and serializable execution semantics.

## EXT-05 · EF bulk updates and deletes
[Official/primary source](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)

ExecuteUpdate/Delete do not inherit automatic tracked concurrency handling.

## EXT-06 · EF connection resiliency
[Official/primary source](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)

Commit connection failures can leave the outcome unknown and require verification.

## EXT-07 · Blazor with EF Core
[Official/primary source](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0)

DbContext lifecycle/concurrency must not be confused with a long-lived server circuit.

## EXT-08 · HTTP Semantics RFC 9110
[Official/primary source](https://www.rfc-editor.org/rfc/rfc9110.html)

Strong If-Match semantics and 412 precondition failures.

## EXT-09 · Additional HTTP Status Codes RFC 6585
[Official/primary source](https://www.rfc-editor.org/rfc/rfc6585.html)

428 Precondition Required.

## EXT-10 · Making retries safe with idempotent APIs
[Official/primary source](https://aws.amazon.com/builders-library/making-retries-safe-with-idempotent-APIs/)

Caller request identity and intent matching support safe retries; retention must be designed.
