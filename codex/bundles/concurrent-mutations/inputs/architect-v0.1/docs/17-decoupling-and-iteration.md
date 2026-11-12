# 17 · Coexistence with component decoupling and iterative review

## Two independent change axes

Component extraction moves rendering/assets/contracts and clarifies host state. Concurrency work changes owner mutation semantics, read/write contracts, outcomes and lifecycle rules. A moved renderer does not solve backend stale writes; a new token must not recouple the renderer to persistence. Current Collaboration guidance explicitly leaves the owner protocol unchanged (SRC-13).

Keep both axes visible in the impact inventory. A phase tracks logical facts and operations, with file paths as changeable evidence anchors. Preserve the current canonical UI seam contract, including submitted/live reconciliation, while adding conflict-specific presentation (SRC-02).

## Integration protocol

At phase entry resolve the actual working branch and sibling revisions. Compare the source ledger to that tree. Remap types that moved into feature contracts. Determine whether the proposed owner signature is already consumed by an in-progress extraction. Agree the smallest data-only seam, then let rendering and owner implementations evolve separately against it.

Avoid editing the same large routed page for both extraction and backend semantics at once when a small contract agreement can separate the work. Keep each logical fix reviewable. At integration, rerun serialization/public-signature and evaluated dependency guards, actual production host wiring and sandbox independence. Retaining the CLR namespace does not alone prove binary or generated-client compatibility.

Do not prescribe a rebase, branch reset, merge, commit or push from this package. Those are separate user-controlled actions. Evidence SHAs serve reproducibility only. A new branch based on later development is expected and supported.

## Drift classification

| Refresh result | Meaning | Required action |
|---|---|---|
| Same path, same blob | Local source observation still matches | Recheck changed callers/composition/test assumptions |
| Same path, different blob | Observation may be obsolete | Read semantic diff, update findings and invalidated tests |
| Missing path, identical blob elsewhere | Candidate relocation | Confirm ownership/public API/asset and registration effects |
| Missing path, no identical blob | Unknown moved/rewritten/deleted implementation | Symbol/owner discovery before using old recommendation |
| Guidance/CI changed | Validation and boundary assumptions may have changed | Read current full guidance and reconcile test lanes |
| Migration head changed | Canonical schema plan may conflict | Re-plan migration from actual head and retain-data proof |
| Sibling/MAF/provider version changed | Adapter/runtime behavior may differ | Revalidate impacted boundary; do not infer compatibility |

The included refresh utility is read-only and deliberately conservative. It cannot certify callers, all mutations or compiled dependencies. Its output is an evidence to-do list, not an automated approval.

## Stable review artifacts

Maintain the mutation inventory, sources, decisions, risks, proposed/actual tests and phase gates together. A changed aggregate boundary invalidates every writer/reader/merge/basis/projection/transfer assumption that mentions it. A changed operation hash invalidates receipt compatibility. A change to shared lock ordering invalidates cross-owner and two-process proofs. Moving a renderer invalidates graph/assets/host wiring evidence, but does not automatically invalidate an unchanged owner SQL predicate.

Use the traceability register to drive these invalidations instead of rerunning either nothing or the entire test suite by habit. Test discovery still resolves actual current assemblies and cases.

## Versioning this design

Bump the package minor revision when decisions, phase scope, coverage or source snapshots change. Record what was accepted, rejected, superseded and left open in CHANGELOG. Retain stable IDs where the same logical issue persists; create a new decision ID for a materially different protocol, with an explicit supersedes link rather than rewriting history invisibly.

An accepted decision is not the same as an implemented phase. An implemented phase is not validated until its gate evidence exists. Keep these states separate in registers and review notes.

## Next review agenda

Close the selected Projects writer perimeter and exact API/tool/UI paths; inspect existing durable journal reuse; ratify source/lock order, stamp and projection choices; validate restore epoch semantics; establish baseline costs; then update P01/P02 scope. Process-wide generic abstractions are not a prerequisite for that next review.
