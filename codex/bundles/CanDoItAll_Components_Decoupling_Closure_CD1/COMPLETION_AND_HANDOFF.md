# Completion decision and next-refactoring handoff

The end of this task is a verified architectural state, not a required number of moved files,
new abstractions, tests or commits. Finish at that state rather than suggesting another generic
"check and next module" wave.

## End conditions

1. The complete current product census has no active `REMAINING` or `UNCLASSIFIED` rendering
   edges. Every retained native/framework/compatibility entry has a concrete responsibility
   and current reachability rationale, not a blanket directory exemption.
2. Dashboard, Runtime Capabilities, database dialog/flyout/utilities and status presentation
   are usable through their intended independent light seams and actual child/asset closure.
   Native owners still enforce and perform all original effects.
3. Existing features, routes, drafts, identities, permissions, outcome/recovery semantics,
   startup gating, native process/workflow/chat/files and renderer compatibility are preserved.
4. Current evaluated boundaries, native/browser/published-asset and relevant development-loop
   proof are complete. Required test/static/portability gates are met; reuse is explicitly
   source/dependency-addressed and not represented as new execution.
5. Current docs/indexes and a concise machine-readable closure/evidence register exist outside
   shared. Verified signed commits and resource disposition are recorded. External limitations
   have exact owners/impact and do not conceal untested in-scope product work.

Record dimensions separately: `structure`, `functional_parity`, `assets_and_dev_loop`,
`required_validation`, `signatures`, `resource_disposition`. The first four plus verified source
commits must pass to state `COMPONENTS_DECOUPLING_COMPLETE`. Resource disposition may explicitly
be `COMPLETE_WITH_REPORTED_EXTERNAL_BLOCKER` for an already denied, noninterfering cleanup, not
for an unowned running database or leaked credential. Do not claim release/merge/live-model/
multi-platform certification unless those exact independent gates are met.

If an actual blocker remains, use `COMPONENTS_DECOUPLING_BLOCKED`, give its exact path/owner,
impact, reproduction/evidence and completed independent work. Do not move an active renderer
or a regression introduced here into an unrelated future backlog to obtain completion.

## Authoritative current records

Prefer one maintained closure document and a compact structured companion, plus the existing
family records linked by source. Update `src/UI/README.md` and the architecture index to point
to it. Keep historical evidence immutable; add current dispositions without relabeling failed
or incomplete historical campaigns. Shared's historical map can remain historical. A current
status document is not a reason to ship or version another shared bundle.

The final report names actual new/moved rendering paths and retained native owners, graph and
activation closure, unresolved items (explicitly none if true), exact executed versus reused
proof, prior raw-artifact availability, tested source and final report commit relationship,
current sibling/policy tuple, resource cleanup and any external qualifications.

## Handoff to the next architecture refactoring

Deliver a short owner/dependency map useful to the next agent: the now-stable presentation
contracts, their native ports/adapters, state/effect lifetimes and tests protecting them.
State that the following remain deliberate native responsibilities: data/persistence authority,
project/profile admissions, writes and operation receipts, orchestration, runtime/provider
execution, route ownership and application composition. Their presence is not a failed UI seam.

Record separately the future backend/module/concurrency work, authoring-aware portable history
transfer/export, large catalog paging and any independently confirmed product limitation.
For each, include the current owner, preserved safety behavior and acceptance needed before
changing it. Do not predesign a speculative replacement for all those systems in CD1.

The next refactor must use the completed seams and existing regression contracts. It must not
recouple a renderer to native services, lose accepted outcomes, narrow functionality, remove
transfer refusals without a replacement, or repeat the entire component extraction.
