# C2 independent persistence review

Status: source/design review only, 2026-10-16, in the managed runtime checkout. No production edit, build, test, scan, database operation or commit. Parent accepted both corrections below and they are incorporated into the [C2 ownership decision](P04-C2-finite-operation-ownership.md). The concrete source card must still define their exact installation SQL/typed protocol and proof inventory before release.

## Accepted ownership and preserved invariants

One Infrastructure implementation of finite metadata is justified by the concrete Workbench second owner. Projects retains its receipt codec, exact acceptance, authority, one-statement lookup and semantic result types. An explicit maintenance participant is a real owner boundary; it must not grow into a mutation callback or common receipt serializer. Neutral records may have composite namespace/pool keys without making pool identity a new caller-controlled wire field.

The proposed lock ordering preserves the current critical fixes: expiry precheck before lifecycle SHARE; no state-row lock in admission; state before epoch for rotation; standalone retirement without lifecycle SHARE/epoch UPDATE; exact binding predicates on bounded cleanup; no nested connections while owner/source guards remain held. `ProjectDefinitionOperationStore.ObserveAsync` and `ProjectDefinitionReceiptReader.ObserveAsync` demonstrate why lookup must remain one coherent owner statement and must reject missing/incompatible metadata before interpreting absence. Past minimum retention alone is not Expired; the committed watermark is.

P03 already defines family as part of the address, implicit in its typed endpoint/table, with a distinct digest and operation-lock domain (`P03-durable-lifecycle-checkpoints.md`, checkpoints 1–3). Preserve that distinction when definition and lifecycle share the Projects epoch pool. Equal operation UUIDs in different families are different addresses; equal UUIDs in different pools must also avoid an accidental shared operation mutex. A shared real target still uses P. None of this makes a definition acknowledgement certify deletion or participant cleanup.

## Required correction: explicit first installation versus ordinary readiness

Current `ProjectOperationEpochService.InitializeForActivationAsync` creates epoch 1 whenever the state row is absent. `DatasetActivationCoordinator.EnsureReadyAsync` calls it even for a matching Active file/SQL binding; Attach/Resume can reach it through `ApplyAsync`. Copying that behavior into the neutral service would make missing previously issued pool metadata indistinguishable from a newly installed owner.

Separate these contracts in the source card:

- New namespace Bootstrap/Reactivate may explicitly create every installed pool in the existing activation transaction.
- Matching Active readiness and Attach validate every installed pool's exact state/current epoch/protocol and fail on absence or corruption. They do not manufacture epoch 1.
- Adding Workbench/Workspace requires an explicit stopped-host installation/migration step that initializes the new pool exactly once. Specify its issued initial timings and how those are persisted; do not infer installation permission from an absent row on any later startup.
- Preserve all existing Projects state, issued epochs and promises during migration. A retention-protocol change must account for retained historical namespace rows as well as the current namespace; it cannot strand cleanup or silently reinterpret unsupported history.

The smallest implementation may seed the first new pool during the reviewed offline migration/activation procedure. The exact SQL/configuration choice belongs in that card. No extra general registry is recommended merely to hide this distinction.

Decisive proofs: matching Active state with a deleted required pool row fails without repair; orphan receipt/state mismatch refuses migration/readiness; fresh namespace activation initializes atomically; explicit new-pool install persists its original timings across restart/stricter current options; repeated install preserves epoch/watermark/receipt bytes.

## Required correction: cross-family cleanup and exclusive import

The proposed all-family emptiness check must not execute while a purge transaction still holds another family's receipt-table write lock. A new cycle is possible:

1. Family B purge holds B's ROW EXCLUSIVE table lock.
2. Existing exclusive import obtains A's ACCESS EXCLUSIVE table lock, then waits for B in its complete sorted table plan.
3. Purge's metadata cleanup asks family A whether it is empty, requiring ACCESS SHARE on A, and waits for import.

Lifecycle-first exclusion does not prevent this: purge deliberately holds no lifecycle SHARE. The current single-family Projects SQL does not introduce this second receipt-table dependency. Neither SKIP LOCKED receipt rows nor a row lock timeout proves safe table-lock ordering.

Recommended minimum: separate monotonic metadata cleanup from single-family purge transactions.

1. Each family purge commits its bounded DELETE and separate post-delete observation before metadata cleanup begins. Publish receipt/byte totals only after its acknowledged commit, and preserve them if later phases fail.
2. Capture at most 128 eligible Closed/retired epoch addresses. Inspect each registered family in its own short coordinated read transaction, releasing it before inspecting the next family. No transaction holds two families' receipt-table locks. Validate that returned addresses are a subset of the requested pool/namespace/epoch set; failed/unknown evidence retains metadata.
3. Delete only the intersection proved empty by every family, in a separate neutral-metadata transaction. Repeat exact active binding, protocol and eligible-state/horizon predicates in the DELETE. Namespace-state deletion follows only after its epochs and all admitted evidence have been accounted for.

Cross-statement emptiness is safe here because the selected epoch is already permanently closed/retired and supported writers cannot insert a new receipt into it. Closure waits for held epoch SHARE guards; each effect and receipt commit atomically before that guard is released. Business transfer never inserts receipts, and managed restore requires stopped hosts plus new namespace activation. Require this insertion perimeter for every family before relying on monotonicity. This reasoning does **not** authorize splitting user receipt lookup into multiple reads.

An alternative is a complete, canonical all-family ACCESS SHARE table-lock plan before purge effects. That can also avoid the inversion, but introduces extra table-plan metadata and holds every family's read lock through each purge. Prefer the separate bounded monotonic inspection with existing coordinated enlistment unless measurement establishes a concrete need for the alternative.

Decisive proofs: actual exclusive-import A-then-B race against family B purge; first family empty while second is nonempty/locked/fails; no metadata delete on Unknown; concurrent workers inspecting the same retired keys; binding/protocol change between inspection and final deletion; attempted post-close owner admission cannot insert evidence after an empty inspection. Verify counts remain confirmed if metadata cleanup later fails.

## Fairness, bounded costs and compatibility

Round-robin pool/family cursors advanced before awaits are sufficient to prevent a slow first family from taking every later family turn, provided cursor state survives cycle cancellation and per-pool timing is reset appropriately on runtime binding change. They do not prove fairness within a family's chosen oldest cleanup epoch. Current Projects SQL selects one eligible epoch ordered by namespace/epoch; a fully locked first epoch can leave later eligible epochs untouched for that cycle. Keep this limit visible in mixed-pressure capacity proof and health. No throughput guarantee or extra per-write lock follows from the scheduler design.

Metadata inspection adds a bounded transaction/query per family and candidate set; include it in the existing two-second aggregate pool budget, not an unbounded post-budget loop. Separate receipt purge count/bytes from metadata cleanup count and phase failure. No expired promise may be shortened because the additional work exhausts a budget.

The version predicate must be present in every metadata write, participant eligibility query and owner lookup before a negative result is interpreted. Exact stopped-host migration is necessary; version numbers alone cannot fence already-running old transactions. Preserve old payload/digest/caller/version bytes verbatim. Rollback after a new family has accepted operations cannot discard its metadata or reinterpret it through an older family set.

No new source-derived counts exist for C2. The ownership decision's migration, registration, fairness and mixed-load cases plus the focused cases above are prospective proof requirements. B1/C1 remain separate prerequisites; this review is not implementation or activation permission.
