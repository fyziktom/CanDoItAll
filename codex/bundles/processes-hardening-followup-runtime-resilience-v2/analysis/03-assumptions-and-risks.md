# Assumptions And Risks

## Assumptions

- PostgreSQL is the only supported runtime database for this branch.
- The existing `processes-hardening` branch is the intended target despite the user's singular branch spelling.
- Some tool enforcement may exist outside reviewed files, but the next bundle must still add tests that prove end-to-end behavior, not only metadata emission.
- The runtime must support both process-owned direct agents and process roles backed by AgentFramework workflows.
- Runtime heuristics are acceptable as migration aids but not as the only source of truth.

## Critical Path Risks

- If explicit operation contracts are not added, the boundary classifier will remain brittle.
- If tool policy only protects external-target aliases, managed output product mutation may still bypass non-mutating boundaries.
- If manager recovery lineage is not fixed, artifact recovery can paradoxically produce artifacts that finalizer rejects.
- If upstream unblock is missing, processes can stay blocked after the cause is fixed.
- If linter remains passive, bad process definitions will keep creating runtime surprises.

## Validation Risks

- Source-assertion tests can pass while real process runtime behavior remains wrong.
- Metadata tests can pass while tool policy is not enforced in production tool calls.
- Happy-path artifact validation can hide stale/wrong-run artifacts.
- Red-team tests focused only on Blazor may regress generic non-software processes.

## Reopen Triggers

Reopen this bundle if:

- any architecture/scope/review step can mutate product files without an explicit mutation contract
- any implementation/product mutation step is denied needed product mutation solely because it writes a report/plan first
- workflow-backed roles complete without process-owned artifact projection and validation
- manager recovery artifacts are rejected due to original execution-run id mismatch
- a downstream step remains blocked after upstream artifacts are produced
- a review step with modeled repair/no-go branches hard-blocks when it can make a valid disposition
- an artifact-production step completes on a repair/no-go branch while its own required artifact is missing
- malformed relative managed JSON satisfies a JSON contract
