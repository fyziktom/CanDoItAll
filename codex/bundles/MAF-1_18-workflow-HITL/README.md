# CanDoItAll MAF 1.18 Upgrade and Workflow HITL Bundle

**Bundle state:** Implemented — Wave A/Wave B historical proof is preserved; Wave C/SB07 is
technically validated but `GOVERNED_PROOF_INCOMPLETE`

**Target repository:** `fyziktom/CanDoItAll`  
**Target branch:** `development`  
**Preparation baseline:** `5cdf1666dbafdcea975909101c1854773f5f3556`  
**Prepared for:** Codex GPT-5.6, reasoning effort `xhigh`  
**Prepared on:** 2026-08-20

**Historical Wave A/Wave B execution:** `maf-update-and-hil` at
`af425ac371b251447f9858b15476092531c686da`; frozen FG-01 window
`2026-08-21T12:52:49.8229732Z`–`2026-08-21T13:59:43.2785414Z`

## Outcome

Upgrade the Microsoft Agent Framework packages used by CanDoItAll from .NET release 1.17 to 1.18 without silently changing tool-execution semantics, then complete the existing workflow human-in-the-loop boundary so a workflow can:

1. stop at a native MAF external request;
2. persist a real MAF checkpoint;
3. survive disposal or host restart;
4. accept an authorized, validated, idempotent API response;
5. rehydrate the exact workflow definition and continue from the checkpoint;
6. complete, fail, cancel, or wait for the next request without restarting from the beginning.

The original initiative is intentionally split into two independently closable waves:

- **Wave A — SB00–SB02:** MAF 1.18 package update and regression hardening.
- **Wave B — SB03–SB06:** native checkpointed workflow HITL and API completion.

Wave A must remain reviewable and revertible even if Wave B uncovers larger runtime work.

The 2026-08-21 follow-on adds an independently closable **Wave C — SB07**. It consumes the
proven HITL API from a standalone Blazor SSR application, repairs only defects demonstrated
by live API use, and supplies real browser proof. It does not rewrite or rerun the historical
FG-01 result.

SB07 implementation and behavior are complete. The governed evidence under
`proof/SB07` records the standalone Release build, 61/61 sample tests, four affected product
Release builds, 71/71 focused Unit tests, 64/64 focused Integration tests, three terminal
API/SSE browser journeys against the frozen sample source set, inspected screenshots, and
safety/anti-stub scans. `BG-SB07-01`
is preserved as invalidated because later PostgreSQL concurrency review required a replay
row lock and a native checkpoint-link uniqueness migration. Replacement gate `BG-SB07-02`
passed its once-only full Integration-project run: 982/983 passed, zero failed, and the sole
skip was the explicitly opt-in live Ollama catalog test that requires additional installed
model families. Final ledgers and independent closeout verification pass.

## Non-negotiable decisions

- Do **not** globally enable concurrent tool invocation.
- Keep application-owned tool invocation serial by default, including an explicit `AllowConcurrentInvocation = false` where CanDoItAll constructs MAF agent options.
- Do **not** enable `StoreInvocableFunctionCallsForFutureTurns` in this initiative.
- Do not confuse provider support for multiple tool calls with permission to execute calls concurrently.
- Do not implement workflow pause by throwing an exception and later restarting the workflow.
- Do not claim exactly-once execution for arbitrary external side effects. The precise guarantee is exactly-once response acceptance and deduplicated participating governed effects.
- Do not mark the in-process backend as durable merely because its checkpoints are persisted.
- Do not expose an API that lets a model or workflow approve its own governed operation.
- Do not accept a response against a different workflow version, topology, request, tenant, project, or actor boundary.
- Do not fall back to rerunning from workflow input when a checkpoint is missing, corrupt, or incompatible.

## Execution order

Read in this order:

1. `AGENTS.md`
2. `CODEX-EXECUTION-PROMPT.md`
3. `inputs/USER-REQUEST.md`
4. `inputs/HITL-API-SAMPLE-REQUEST.md`
5. `inputs/REQUIREMENTS.md`
6. `evidence/CURRENT-STATE.md`
7. `evidence/HITL-API-SAMPLE-CURRENT-STATE.md`
8. `evidence/MAF-1.18-DELTA.md`
9. `architecture/ARCHITECTURE-REVIEW.md`
10. `architecture/HITL-API-SAMPLE-DESIGN.md`
11. `architecture/TOOL-CONCURRENCY-POLICY.md`
12. `architecture/HITL-STATE-MACHINE.md`
13. `plan/EXECUTION-PLAN.md`
14. `traceability/TRACEABILITY.md`
15. the next dependency-ready subbundle under `subbundles/`

Before and after every subbundle, apply the repository-owned bundle workflow and subbundle validator from `CanDoItAll.SharedInfo/codex/skills/bundles`.

## Bundle compatibility map

| Semantic role | Location |
|---|---|
| Original inputs | `inputs/USER-REQUEST.md` and `inputs/HITL-API-SAMPLE-REQUEST.md` |
| Normalized requirements | `inputs/REQUIREMENTS.md` |
| Current evidence | `evidence/` |
| Architecture decisions | `architecture/` |
| Impact and test inventories | `inventories/` |
| Dependencies and invalidation | `plan/` |
| Work units | `subbundles/SB00` through `SB07` |
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

## Historical Wave A/Wave B validation summary

All Wave A/Wave B completion-bar items are satisfied. The final valid frozen FG-01 checkpoint used the
five repository-authoritative commands from `docs/testing.md`: both restores passed, the
product and Stable Release builds passed with zero warnings and zero errors, and the exact
filtered Stable test gate passed **8,471/8,471** with zero failed or skipped tests. The
assembly totals were Components 1,078, Integration 923, AgentFramework.Memory 22, Memory
196, and Unit 6,252.

The final dependency roots remained pinned throughout the gate:

- `CanDoItAll.Components`: `8372c1d55f21b349f8e859470b02eeb4421e96ca`;
- `CanDoItAll.FileTools`: `c95dd07208a6d48724443317cdc6cfe67a13020a`.

Pre-freeze broad diagnostics are retained honestly in the execution report. They exposed
test-composition readiness, implicit plugin-export, safe API projection, in-memory runtime
redaction/atomicity, and process-host readiness defects. Those findings reopened the
affected ownership claims, received focused red/green proof, and were revalidated before
the single accepted FG-01 checkpoint. Append-only supplements preserve the SB03, SB04,
and SB05 reproof without rewriting their frozen historical ledgers or TRXs.

## Wave C validation state

Current-source focused validation passes: the standalone sample is 61/61, the Unit selector is
71/71, the combined Web-boundary and PostgreSQL recovery Integration selector is 64/64,
and three Playwright journeys cover direct hit, second-attempt hit, and exactly-three miss.
The sample plus four affected product projects build in Release with zero warnings and zero
errors. The final persistence changes serialize request-operation replay against lease/state
mutation with a request-scoped PostgreSQL `FOR UPDATE` lock and add migration
`20260822013043_AddWorkflowNativeCheckpointRequestUniqueness` for the filtered native
`(SessionId, BackendRequestId, BackendRequestPortId)` tuple.

The full Integration project passed 982/983 with zero failures and the single declared opt-in
live Ollama catalog skip. The implementation is technically validated, but no authentic
failing-first test artifact exists for the behavior-changing recovery work. The SHA-256 ledgers
are frozen and both validators pass, but SB07 and the current parent therefore remain
`GOVERNED_PROOF_INCOMPLETE`, not Proven. Historical FG-01 stays Pass with its original HEAD,
counts, timestamps, and sibling pins.
