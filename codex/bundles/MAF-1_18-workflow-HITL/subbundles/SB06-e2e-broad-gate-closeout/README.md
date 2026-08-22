# SB06 — End-to-End Proof, Frozen Broad Gate, and Closeout

## Status

Proven

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

### E2E evidence map

The final SB06 source/test state is frozen. The rows below distinguish facts proven by the
reconstructed real-host fixture from retained focused prerequisites. FG-01 subsequently
passed against that valid frozen state; no row treats the product solution as a test
solution.

| # | Required scenario | Current proof anchor | Scope note | State |
|---:|---|---|---|---|
| 1 | HumanInput wait, host/service reconstruction, response, completion | `WorkflowHitlEndToEndIntegrationTests.HumanInput_AfterHostReconstruction_ResumesConsecutiveWaitsWithoutRerunningPrefix` in the governed 12/12 E2E TRX | Three separately constructed real Web hosts share PostgreSQL and protected checkpoint state. | Proven |
| 2 | Approval wait, approve, governed effect once, completion | Approve row of `Approval_AfterHostReconstruction_EnforcesDecision` | Real Web/JWT/service/PostgreSQL/MAF path; deterministic participating executor is the probe. | Proven |
| 3 | Approval wait, deny, zero side effect, governed result | Deny row of `Approval_AfterHostReconstruction_EnforcesDecision` | Denial is a typed governed outcome and the probe remains zero. | Proven |
| 4 | Two consecutive external requests and checkpoints | HumanInput reconstruction case | Two distinct public request IDs and two distinct native-boundary checkpoint IDs are asserted. | Proven |
| 5 | Duplicate same-key response returns the same operation | `AuthenticatedResponse_TraversesWebServicePostgreSqlAndMaf` in the frozen 14/14 Integration selector | Same operation identity, no duplicate event/effect. | Proven |
| 6 | Changed-payload replay conflicts | Same real API/PostgreSQL/MAF test in the 14/14 selector | HTTP conflict, one durable operation, no new response-side event. | Proven |
| 7 | Two concurrent callers create one active resume | `ConcurrentApprovalResponses_CreateOneActiveContinuationAndOneEffect` | Blocking claim hook proves one active operation and one governed effect. | Proven |
| 8 | Cancellation while waiting rejects response | `AuthenticatedResponse_AfterRealCancellation_ReturnsGoneWithoutCreatingOperation` in the 14/14 selector | Late response is rejected and no response operation is created. | Proven |
| 9 | Crash after claim recovers | Claimed-before-delivery row of `ApprovalCrashWindow_AfterHostReconstruction_RecoversWithoutDuplicateEffect` | A reconstructed host invokes the real recovery coordinator after lease expiry. | Proven |
| 10 | Crash after delivery avoids intentional duplicate governed effect | Delivered-before-commit row of the same crash theory | Persistent participating-executor replay leaves the governed effect count at one. | Proven |
| 11 | Missing/corrupt checkpoint fails closed | Missing and corrupt rows of `CorruptRecoveryState_AfterHostReconstruction_FailsClosed` | Typed outcome, terminal operation, no prefix restart or approval effect. | Proven |
| 12 | Topology/version mismatch fails closed | Topology and workflow-version rows of the same corruption theory | Exact typed mismatch outcomes and zero restart/effect. | Proven |
| 13 | Legacy waiting run remains inspectable/non-resumable | `LegacyWaitingRun_AfterHostReconstruction_RemainsInspectableAndRejectsResponse` | Detail remains readable, request is explicitly `LegacyNonResumable`, and no operation is created. | Proven |
| 14 | Wrong actor/scope/self-approval is rejected | Real broad-scope HTTP denial in the 14/14 selector plus authorizer cases in the 7/7 Unit selector | The real API proves zero mutation; focused authorizer cases cover autonomous actor, exact scope, capability, and intended approver. | Proven |
| 15 | Raw checkpoint/secrets are absent from API/log proof | `RestartableResponsesAndLogs_DoNotExposeCheckpointOrSecrets` plus `MafWorkflowEventNormalizerTests` 4/4 | Sentinels are proven present only after production-store decryption; logger output and HTTP are scanned without rendering secret values. | Proven |
| 16 | Pre-HITL nodes are not rerun after rehydration | HumanInput and corruption/crash probes in the governed E2E class | Prefix count remains one across reconstructed hosts. | Proven |
| 17 | Tool multi-call order remains serial | `MafToolInvocationConcurrencyPolicyTests` in the frozen 7/7 Unit selector | Both streaming modes are serial; the explicit opt-in overlap probe remains isolated test evidence. | Proven |

### Frozen focused evidence

- governed restart E2E: 12/12 passed;
- final retained Unit selector: 7/7 passed;
- final retained Integration selector: 14/14 passed;
- normalizer regression: 4/4 passed after an honest internal-port-ID red diagnostic;
- PostgreSQL completion precision: 0/1 red, then 1/1 green;
- PostgreSQL lease boundary: 4/6 red, then 6/6 green;
- affected Integration builds: zero warnings and zero errors;
- architecture snapshot: `snap-20260821092959-44e660f5`, 9 projects, 499 documents, no blocking errors, and only the two unchanged baseline non-project cycles;
- package scanner: stable `1.18.0` and preview `1.18.0-preview.260818.1`, with no findings;
- EF model gate: no pending model changes (the EF tools/runtime version advisory is non-blocking).

Crash-window E2E evidence uses the real recovery coordinator on a reconstructed host. It
does not independently claim automatic hosted-worker startup, which remains covered by
the focused recovery-worker and production-composition tests from SB05.

## Invalidation keys

IK-01 through IK-18 as applicable, especially IK-05, IK-12, IK-13, IK-16, IK-17.

## Broad-gate decision

FG-01 ran once against the valid final frozen state after all focused/E2E and pre-freeze
diagnostic repairs passed:

```powershell
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet restore ./tests/Solutions/CanDoItAll.Tests.Stable.slnx
dotnet build ./tests/Solutions/CanDoItAll.Tests.Stable.slnx --configuration Release --no-restore /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Stable.slnx --configuration Release --no-build --no-restore --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true" /m:1
```

`CanDoItAll.slnx` is only the product build graph and deliberately has no test projects.
Never use `dotnet test ./CanDoItAll.slnx` as FG-01 or as any test gate.

For this execution, keep the sibling source graph pinned throughout FG-01:

- `CanDoItAll.Components`: `8372c1d55f21b349f8e859470b02eeb4421e96ca`
- `CanDoItAll.FileTools`: `c95dd07208a6d48724443317cdc6cfe67a13020a`

The five commands above are the repository-authoritative broad gate from
`docs/testing.md`. Do not substitute affected-project runs, the product solution, or an
unfiltered UI lane for this gate. The accepted frozen run was not repeated.

### FG-01 result

- source/test HEAD: `af425ac371b251447f9858b15476092531c686da`;
- valid freeze window: `2026-08-21T12:52:49.8229732Z` through
  `2026-08-21T13:59:43.2785414Z`;
- product restore: pass; projects current;
- product Release build: pass, 0 warnings/errors, 51.15s;
- Stable restore: pass; projects current;
- Stable Release build: pass, 0 warnings/errors, 70.13s;
- exact filtered Stable test: exit 0, 8,471/8,471 passed, zero failed/skipped;
- assembly totals: Components 1,078/1,078 in 15m59s; Integration 923/923 in 43m08s;
  AgentFramework.Memory 22/22 in 297ms; Memory 196/196 in 11s; Unit 6,252/6,252
  in 2m10s;
- dependency pins stayed exact: Components
  `8372c1d55f21b349f8e859470b02eeb4421e96ca` and FileTools
  `c95dd07208a6d48724443317cdc6cfe67a13020a`.

A sandbox-only restore denial was retried with the identical permission-enabled command and
passed. It did not change source or count as a product failure.

### Honest pre-freeze diagnostic and reopen record

An earlier exact Stable attempt was interrupted after it exposed a Components UI harness
composition gap. Adding the production AgentFramework UI registrations and authorization
context to the shared bUnit harness repaired the fixture; the affected selector passed
40/40 and the complete Components assembly passed 1,078/1,078.

The next complete exact diagnostic returned 8,463/8,470 and deliberately did not become
the accepted FG-01 checkpoint because its findings required source/test changes. The seven
failures drove these narrow repairs:

- implicit plugin assembly discovery now admits only visible, concrete, closed executor
  types; explicit type registration is unchanged (`1/1` focused green);
- the API contract asserts that `WorkflowRunStartApiResponse` does not expose the protected
  idempotency-key hash (`1/1` focused green);
- in-memory native resume persists a blank source response while the protected operation
  retains the payload, uses a typed internal acceptance seam, and fails during construction
  when native compatibility lacks that capability; lifecycle reproof passed `14/14`;
- the in-memory cancellation transition was extracted into a cohesive top-level helper,
  restoring the architecture line budget without weakening it; the combined runtime repair
  selector passed `9/9`;
- Windows process-host tests now serialize their process-tree fixture and require the child
  readiness signal before asserting tree termination (`2/2` focused green).

Earlier SB06 red/green findings also reopened narrow historical claims: PostgreSQL
sub-microsecond completion precision (`0/1` to `1/1`), exact-expiry lease fencing (`4/6`
to `6/6`), and unresolved native backend-port identifiers in public event text (`0/1` to
the normalizer `4/4` plus restart E2E `12/12`). Append-only supplements under
`proof/SB03/reopen-20260821`, `proof/SB04/reopen-20260821`, and
`proof/SB05/reopen-20260821` preserve those reproofs without rewriting frozen parent
ledgers or TRXs.

## Closure record

Proven. The 17-row E2E matrix, retained focused selectors, source/security review,
architecture gate, package graph, documentation/input audit, and the single accepted FG-01
checkpoint all pass on the frozen source state.

- frozen HEAD/diff: `af425ac371b251447f9858b15476092531c686da`; final
  `git diff --check` passes;
- E2E scenario matrix: all 17 rows Proven, including reconstructed-host HumanInput,
  approval/denial, crash windows, corruption/version/topology negatives, legacy handling,
  concurrency, cancellation, redaction, no-prefix-replay, and serial tool invocation;
- source assertions: package, MAF isolation, common response facade, public projection,
  no-restart fallback, and deduplicated participating-effect boundaries pass;
- FG-01: all five commands pass; exact filtered Stable test is 8,471/8,471;
- discovered counts: Components 1,078; Integration 923; AgentFramework.Memory 22; Memory
  196; Unit 6,252; zero failed or skipped;
- documentation: API/runtime/package documentation and the maintained-Markdown validator
  pass;
- traceability audit: RQ-001 through RQ-045 are Proven;
- original input closure: every note is Solved with implementation and proof evidence in
  `closeout/EXECUTION-REPORT.md`;
- remaining blockers: none;
- final state: SB06 Proven, CP-WB4 Pass, parent bundle Proven.
