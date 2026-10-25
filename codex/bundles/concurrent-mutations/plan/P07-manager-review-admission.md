# Manager continuation admission

Entry: accepted after the qualified 183-case capacity/authority correction. Proof tier:
Governed. Snapshot `snap-20261021071835-1de77694` records the affected Core, Workbench
and Process owners, five project edges and nine pre-existing cycles. Direct project
inspection confirms Workbench already references Core and Process Application.

## Problem and owner boundaries

An inherited Agent saved-effect continuation currently always requires current Process
dispatch authority. The first NeedsManager result completes the Process dispatch claim.
Reusing that claim would reject valid approval or incorrectly reopen expired authority.
The existing Process publication fence already has a ManagerReview purpose that requires
an exact current child, a waiting step, no active claim, no root cancellation and the
same original source/material/contract in the effect owner's PostgreSQL transaction.

Core owns the live saved-effect lease, exact displayed Agent expectation, immutable
proposal binding, approval, receipt check, work accounting and invocation claim.
Workbench owns Project task publication and selects the enlisted Process fence using
authority actually admitted on that lease. Process Application will later own current
manager authorization and waiting result settlement. No new project reference, store,
journal schema, serialized authority flag or runtime partial is needed for this seam.

## Minimal change

Extend AgentInheritedSavedEffectContinuation with an explicit ForManagerReview factory.
It captures the original inherited origin, exact displayed planning expectation, selected
immutable proposal binding and trusted current-parent-review callback. Existing constructor
semantics remain dispatch-only. Validate the review selection against the actual requested
expectation/binding before admission. Every later read still verifies the same original
run/decision/generation and parent origin; journal revisions may advance through this
operation's own acknowledged writes.

RequireSavedReadAsync uses disclosure plus the trusted review callback only for that
explicit review capability. Ordinary inherited continuation keeps the dispatch check.
Retain the existing current caller/source checks before and after waiting for the run
lease, exact revision comparison inside the write, original approval/digest, mandatory
receipt absence, active work, claim and publication snapshot. Review does not authorize
a provider call, a substituted proposal, rejection-through-dispatch, or a replacement child.

Expose the admitted inherited continuation purpose only through AgentPlanningLiveWork
after RequirePublication validates the actual held snapshot, invocation claim, lease,
selected binding and work. Workbench resolves Dispatch versus ManagerReview from that
validated result inside RequireEnlistedAsync. It must not accept a DTO boolean or infer
manager authority from stored JSON.

## Testability and proof

Use the existing real file-store/journal fixture to prove dispatch-denied/read-allowed
review; unchanged ordinary rejection; exact displayed expectation and proposal mismatch;
revocation before admission, while waiting for the lease and before the claim; competing
reviewers; lost admission acknowledgement; current caller/source identity; disposed or
unbound live work and changed publication binding. No provider is invoked to approve.
Use the actual Project publication owner and Process PostgreSQL fence in the next
adapter/manager integration checkpoint before activation.

Review purpose is not a public endpoint. A current trusted parent-review callback remains
mandatory; the future Process manager entry must bind caller, current review permission,
waiting phase and original displayed Process expectation to it. This seam alone does not
close manager result transitions, rejection, cancellation, refresh, recovery, or activation.

## Performance and failure semantics

No additional background query for ordinary dispatch. Review substitutes current disclosure
for the impossible expired-claim check and retains the necessary parent-review check.
The added exact selection comparison is in memory. No lock spans provider or human wait.
Authority is rechecked on the existing short lease/effect transactions. Unknown writes
retain open work for reconciliation; they never become a zero-work retry or automatic
reapproval. Preserve exact old receipts across retries and host restart.

## Architecture and validation gate

This is a capability-contract extension at existing boundaries, not a new orchestrator.
The simpler option of changing every inherited read to Read was rejected because it
would weaken ordinary dispatch. Reusing the expired Process claim was rejected because
it cannot authorize a waiting manager action. No new interface, service location, project
boundary, runtime partial or persistent flag is introduced.

Pure capability-selection tests must run without the journal/runtime/filesystem. Existing
real saved-effect integration tests gain review variants for exact admission, current
authority, lease waits, work limits and acknowledgement loss. Real Project publication
tests must transition the Process to waiting using its result owner, then prove manager
publication without a live claim and rejection when its phase or root authority changes.
The existing ordinary publication/expiry cases remain dependent regression coverage.
Record exact discovery after test construction and before execution; never accept zero.

Build Core and Workbench directly in Release, then refresh Unit/Integration using those
outputs. Run full portability and before/after dependency/findings comparison, freeze
source/runtime, observe the selected fixture families, retain proof and reconcile.
No unfiltered gate is justified by this bounded seam; final runtime-composition activation
remains a named later broad checkpoint. The journal partial gains only selection delegation
and check-mode selection; pure capability logic stays in the existing independent type.

The frozen candidate selects 74 cases: 11 pure continuation-capability cases, all 47
inherited saved-effect cases, four real manager-publication cases, nine selected ordinary
publication/expiry/work/snapshot cases, and three registered MAF publication cases.
The latter selections use exact method names rather than unrelated class cases.
The four manager cases intentionally stop with the Process waiting receipt preserved;
they prove publication admission/fencing, not the still-required manager-result owner.

Qualified result: [74 distinct cases](../validation/2026-10-21-manager-review-admission.md).
The profile-revocation case now expects denied disclosure and proves actual SQL rollback
plus retained unsettled work. Production is unchanged across that test-only correction.
The failed initial case, observer-file-sharing incident and deterministic sharing probe
are retained. Process waiting-result completion and actual manager authorization remain open.
