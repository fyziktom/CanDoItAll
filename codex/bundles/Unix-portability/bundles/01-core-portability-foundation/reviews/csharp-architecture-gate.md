# C# architecture gate

## A00 review

- Status: `GO for A01`.
- Snapshot: `snap-20260808192349-53bec4ab`.
- Project cycles: `0`.
- Architecture owner: primary execution agent.
- Proof tier: `Standard`.

## Required findings

- [x] Current responsibilities and high-risk types inventoried.
- [x] Target ownership and dependency direction recorded.
- [x] Pattern selections recorded with rejected alternatives.
- [x] Testability and partial-class policy recorded.
- [x] Architecture checkpoints added to the execution graph.
- [x] Windows/Linux baseline evidence complete and classified.
- [x] Path and persistence inventories contain no unknown high-priority record.
- [x] Gate C0 issued in the core gate log.

## Guard decision

A01 is the only permissible next implementation phase after C0. The runtime/tools bundle remains blocked until exact core Gate C4 evidence.

## A01 C1a candidate

- Status: `GO — independently reviewed`.
- Snapshot: `snap-20260809031028-a2e9718e`.
- Full project graph: `104` projects, `619` direct references, `0` project cycles.
- Boundary result: `Infrastructure.Abstractions` is dependency-free; Core/Models use
  the abstraction and do not reference the Infrastructure implementation.
- A01 SharedKernel additions: pure logical values/codecs and syntax classification only;
  no filesystem I/O, host probe, data protection, or mutable binding registry.
- Composition result: the scoped registry implementation is selected by hosting and
  rebuilt from trusted protected bindings per execution scope. Strict DI validation
  passes, workspace consumers and workflow executors are scoped, and two-scope tests
  prove binding isolation.

The independent C1a decision is recorded in `reviews/08-a01-independent-review.md` and
the canonical gate log. A02 is the only eligible next subbundle.

## A02 C1 GO

- Status: `GO — independently reviewed after FS-008 remediation`.
- Scoped snapshot: `snap-20260809105134-2719bac5`; no blocking diagnostics.
- Full project graph: `105` projects, `631` direct references, `0` project cycles.
- Boundary result: Infrastructure owns filesystem probing and durable operations;
  Infrastructure.Abstractions is dependency-free; SharedKernel owns only the pure
  portable filename codec.
- Identity result: logical paths are ordinal; physical equality and containment use a
  root-scoped detected case model and fail conservatively when unknown.
- Authority result: all composition roots register the mutable external-target registry
  as scoped. The stateless factory remains singleton; Windows/Linux two-scope tests prove
  binding isolation.
- Safety result: link/reparse traversal fails closed, mutation parents are revalidated,
  durable commits are same-directory and cross-process coordinated, Unix private modes
  are verified, and watcher events converge through deterministic rescan/polling.
- FS-008 remediation: stable filename identity is separate from occupied-name allocation;
  every allocated candidate is rechecked, and auto-allocated storage uses a typed atomic
  create-new/no-replace commit so post-guard occupancy is never overwritten.

The complete independent review history and final GO are recorded in
`reviews/10-a02-independent-review.md`. Remediated evidence is frozen in
`reviews/09-a02-evidence-report.md`. A03 is the only eligible next subbundle after bundle
integrity closure.
