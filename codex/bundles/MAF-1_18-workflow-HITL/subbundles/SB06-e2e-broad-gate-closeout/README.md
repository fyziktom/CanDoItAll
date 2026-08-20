# SB06 — End-to-End Proof, Frozen Broad Gate, and Closeout

## Status

Prepared

## Outcome

Prove the complete upgrade and workflow HITL flow under realistic restart, race, denial, authorization, corruption, and side-effect replay scenarios; run the named broad gate once; update documentation and close every requirement.

## Owned requirements

RQ-031, RQ-041 through RQ-045, and final proof for all prior requirements.

## Non-goals

- new product behavior;
- opportunistic refactoring after freeze;
- enabling parallel tools;
- broad UI redesign;
- hiding failed checks as residual risk;
- repeatedly rerunning the broad suite.

## Prerequisites

SB00–SB05 passed. SB05 declared frozen source/schema/API state.

## Reopen triggers

- any source/schema/package/API semantic change after FG-01;
- any focused or E2E result contradicts a prerequisite;
- any traceability row lacks implementation/proof;
- any secret or raw checkpoint appears in evidence;
- broad gate reveals an affected-scope defect.

## Exact sources and discovery

Review all changed files and bundle closure records.

Run source assertions from `proof/VALIDATION-PLAN.md`.

Inspect:

- central package props and resolved graph;
- agent options/client composition;
- workflow compiler/backend/checkpoint adapter;
- response manager/service/state store;
- EF migration/schema;
- API/auth/status mapper;
- documentation;
- all affected tests.

## Implementation boundary

SB06 should contain only:

- E2E fixtures/proof fixes required to expose real behavior;
- documentation/API metadata corrections;
- small defect fixes found by proof;
- bundle evidence and closure.

Any material architecture or schema change reopens the owning subbundle and invalidates FG-01.

## Acceptance criteria

### Upgrade

- target stable/preview package graph proven;
- no active old symbols/versions;
- affected solution builds;
- tool execution serial and non-overlapping;
- approval/session regressions pass.

### HITL E2E

At minimum prove:

1. HumanInput wait → host/service reconstruction → response → completion.
2. Approval wait → approve → governed side effect once → completion.
3. Approval wait → deny → no side effect → governed result.
4. Two consecutive external requests with two checkpoints.
5. Duplicate same-key response returns same operation.
6. Conflicting replay is rejected.
7. Two concurrent callers create one active resume.
8. Cancellation while waiting rejects response.
9. Crash after claim recovers.
10. Crash after response delivery does not intentionally duplicate governed side effect.
11. Missing/corrupt checkpoint fails closed.
12. Topology/version mismatch fails closed.
13. Legacy waiting run remains inspectable/non-resumable.
14. Wrong actor/scope/self-approval rejected.
15. Raw checkpoint/secrets absent from API/log proof.
16. pre-HITL nodes are not rerun after rehydration.
17. tool multi-call order remains serial.

### Documentation/closure

- API control-plane documentation matches;
- MAF version documentation matches;
- migration/legacy behavior documented;
- operational guarantees avoid false exactly-once claim;
- every traceability row is Proven or honestly Blocked;
- original request is closed note by note.

## Proof tier

Governed

## Focused validation

Rerun the focused suites only when E2E evidence invalidates them.

Create a realistic E2E fixture using deterministic fake providers/executors plus real MAF workflow/checkpoint protocol and the real persistence/API service boundary. Use a real test database where repository conventions support it.

## Invalidation keys

IK-01 through IK-18 as applicable, especially IK-05, IK-12, IK-13, IK-16, IK-17.

## Broad-gate decision

Run `FG-01` once after all focused/E2E checks pass:

```bash
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --no-restore
```

Run solution-wide tests once when feasible and repository-conventional:

```bash
dotnet test CanDoItAll.slnx --no-build
```

When full solution tests are not a valid gate, run every affected test project once and record the exact exclusion/baseline reason. Do not repeatedly run expensive UI tests.

## Closure record

Not executed.

Record:

- frozen HEAD/diff:
- E2E scenario matrix:
- source assertions:
- FG-01 commands/results:
- discovered counts:
- documentation:
- traceability audit:
- original input closure:
- remaining blockers:
- final state:
