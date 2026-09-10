# 12. Governance of subsequent changes

## This foundation does not authorize blanket implementation

It defines target constraints and regression conditions, not implementation subbundles, patches, migrations, or commands to run every test. Each implementation gets a small separately approved scope. Forecast features are not automatically part of it. All subsequent engineering artifacts are written in English.

## Stable Agents checkpoint

Record exact SHA, delivered scope, exclusions, public/schema changes, test discovery, and affected runtime evidence. Stabilize current work without expanding to the entire roadmap. Rebaseline against that checkpoint; historical foundation observations and the targeted 504c47d audit are not a substitute for current code.

## Gates for one boundary

**G0 — Current truth:** inspect actual owners, consumers, writers, endpoints/tools/jobs, serialized payloads, supported purposes, and tests. Resolve discovery anchors; record documentation/code conflicts and unknowns.

**G1 — Ownership:** assign each changed field/relation as master, projection, historical snapshot, or local enrichment. Establish lifecycle, identity, pricing/scope, and semantic migration of mixed records.

**G2 — Contract:** define query/command/receipt, exact caller and owner, authority/egress, revision, ordering, idempotency, failure/cancellation, compatibility, and actual adapter registration. A tool is not proven by its API's existence.

**G3 — Data safety:** preserve constraints, concurrency, audit/filter behavior, atomicity, upgrade/rollback, single-writer routing, and durable retry history. Plan storage/remote compensation explicitly.

**G4 — Production routing:** real callers use the new owner adapter, not only a sandbox. Legacy paths delegate, block, or disappear. No shadow mutation, dual master writes, or silent fake capabilities.

**G5 — Proof:** run the scoped tests and relevant browser/live paths with explicit NOT_RUN/BLOCKED gaps. A green build is not agent-to-file/task-to-workflow/process-to-writeback proof. Test each supported interactive/workflow/process transport separately.

**G6 — Closure:** update durable docs, evidence, and ownership; remove duplicated implementation or name a compatibility shim's purpose/end. Resume UI extraction over the now-stable interface.

These are acceptance gates for small changes, **not seven execution subbundles**.

## Resolving conflicts

Prioritize safety and truthful outcomes, product/data preservation, clear/testable ownership, then physical layout/build performance. An existing security defect need not be retained as parity, but its correction requires explicit hardening scope and evidence, not an unrelated silent regression.

If every operation carries a huge foreign aggregate or needs many global transactions, revisit the boundary rather than use a repository/service locator. Targeted owner reimplementation behind a stable port is legitimate; rebuilding the entire product under “refactoring” is not.

## Changing this foundation

Changes to authority, identity, destructive defaults, permissions, cost basis, atomicity, or schema compatibility require a reasoned decision and impact record. Evidence/symbol corrections retain provenance. Do not weaken rules merely to satisfy one implementation/test.

Move accepted long-lived principles into ordinary repository/SharedInfo documentation over time. Production code/tests must not treat the ZIP/bundle filename as a runtime concept. This is a development guide, not an application dependency.
