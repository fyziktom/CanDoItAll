# CanDoItAll MAF 1.18 Upgrade and Workflow HITL Bundle

**Bundle state:** Prepared for execution  
**Target repository:** `fyziktom/CanDoItAll`  
**Target branch:** `development`  
**Preparation baseline:** `5cdf1666dbafdcea975909101c1854773f5f3556`  
**Prepared for:** Codex GPT-5.6, reasoning effort `xhigh`  
**Prepared on:** 2026-08-20

## Outcome

Upgrade the Microsoft Agent Framework packages used by CanDoItAll from .NET release 1.17 to 1.18 without silently changing tool-execution semantics, then complete the existing workflow human-in-the-loop boundary so a workflow can:

1. stop at a native MAF external request;
2. persist a real MAF checkpoint;
3. survive disposal or host restart;
4. accept an authorized, validated, idempotent API response;
5. rehydrate the exact workflow definition and continue from the checkpoint;
6. complete, fail, cancel, or wait for the next request without restarting from the beginning.

The initiative is intentionally split into two independently closable waves:

- **Wave A — SB00–SB02:** MAF 1.18 package update and regression hardening.
- **Wave B — SB03–SB06:** native checkpointed workflow HITL and API completion.

Wave A must remain reviewable and revertible even if Wave B uncovers larger runtime work.

## Non-negotiable decisions

- Do **not** globally enable concurrent tool invocation.
- Keep application-owned tool invocation serial by default, including an explicit `AllowConcurrentInvocation = false` where CanDoItAll constructs MAF agent options.
- Do **not** enable `StoreInvocableFunctionCallsForFutureTurns` in this initiative.
- Do not confuse provider support for multiple tool calls with permission to execute calls concurrently.
- Do not implement workflow pause by throwing an exception and later restarting the workflow.
- Do not claim exactly-once execution for arbitrary external side effects. Provide exactly-once response acceptance plus a stable deduplication boundary for replayable side-effecting workflow executors.
- Do not mark the in-process backend as durable merely because its checkpoints are persisted.
- Do not expose an API that lets a model or workflow approve its own governed operation.
- Do not accept a response against a different workflow version, topology, request, tenant, project, or actor boundary.
- Do not fall back to rerunning from workflow input when a checkpoint is missing, corrupt, or incompatible.

## Execution order

Read in this order:

1. `AGENTS.md`
2. `CODEX-EXECUTION-PROMPT.md`
3. `inputs/USER-REQUEST.md`
4. `inputs/REQUIREMENTS.md`
5. `evidence/CURRENT-STATE.md`
6. `evidence/MAF-1.18-DELTA.md`
7. `architecture/ARCHITECTURE-REVIEW.md`
8. `architecture/TOOL-CONCURRENCY-POLICY.md`
9. `architecture/HITL-STATE-MACHINE.md`
10. `plan/EXECUTION-PLAN.md`
11. `traceability/TRACEABILITY.md`
12. the next dependency-ready subbundle under `subbundles/`

Before and after every subbundle, apply the repository-owned bundle workflow and subbundle validator from `CanDoItAll.SharedInfo/codex/skills/bundles`.

## Bundle compatibility map

| Semantic role | Location |
|---|---|
| Original input | `inputs/USER-REQUEST.md` |
| Normalized requirements | `inputs/REQUIREMENTS.md` |
| Current evidence | `evidence/` |
| Architecture decisions | `architecture/` |
| Impact and test inventories | `inventories/` |
| Dependencies and invalidation | `plan/` |
| Work units | `subbundles/SB00` through `SB06` |
| Traceability | `traceability/TRACEABILITY.md` |
| Validation strategy | `proof/VALIDATION-PLAN.md` |
| Execution state | `STATUS.md` and subbundle closure records |
| Final closure | `closeout/` |

## Stop rules

Stop the current subbundle and repair or re-anchor the bundle when:

- the checked-out branch or package graph no longer matches the baseline assumptions;
- target MAF versions are no longer `1.18.0` and `1.18.0-preview.260818.1`;
- MAF 1.18 API reality contradicts a planned call;
- the implementation would require enabling concurrent tool invocation globally;
- checkpoint rehydration cannot preserve exact executor and port identities;
- persistence cannot atomically claim a response operation;
- API authorization cannot be established at the service boundary;
- a failed checkpoint load would otherwise trigger a full workflow restart;
- focused tests discover zero tests;
- a downstream finding invalidates a completed prerequisite.

Record the blocker in `STATUS.md`, update the owning subbundle, and identify every downstream subbundle that must be revalidated.

## Completion bar

The bundle is complete only when:

- package versions and lock/restore evidence prove the intended MAF release;
- all relevant compile-time breaks are resolved without compatibility shims that hide stale APIs;
- serial tool execution remains the default and is protected by regression tests;
- existing agent approval/session behavior still passes in streaming and non-streaming paths;
- a real MAF workflow pauses through `RequestPort`/`RequestInfoEvent`;
- checkpoint JSON is persisted through an application-owned storage abstraction;
- a new run instance can rehydrate and resume the exact workflow version;
- API authorization, validation, idempotency, conflict handling, and audit data are proven;
- duplicate and crash-recovery scenarios do not intentionally execute a governed side effect twice;
- focused proof passes for every subbundle;
- the named broad gate runs once at the frozen checkpoint or is honestly blocked with evidence;
- all requirement rows in `traceability/TRACEABILITY.md` are closed.
