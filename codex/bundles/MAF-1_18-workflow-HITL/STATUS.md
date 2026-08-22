# Bundle Status

**Overall:** Implemented — Wave C is technically validated but Governed proof is incomplete;
Wave A/Wave B remain historically Proven

**Current wave:** Wave C — implementation complete; Governed closure incomplete

**Current subbundle:** SB07 — Implemented (`GOVERNED_PROOF_INCOMPLETE`)

**Preparation baseline:** `5cdf1666dbafdcea975909101c1854773f5f3556`  
**Historical Wave B execution HEAD:** `af425ac371b251447f9858b15476092531c686da`
**Wave C working HEAD observed during scaffolding:** `c19cb94bfac710f567db63a93f1e4af69f046cc5`
**Last update:** 2026-08-22

| Subbundle | Title | Proof tier | Status | Dependency state |
|---|---|---:|---|---|
| SB00 | Re-anchor and baseline | Standard | Proven | Complete; baseline trusted |
| SB01 | MAF 1.18 package and compile migration | Standard | Proven | Complete; 1.18 graph trusted |
| SB02 | Agent/tool safety regressions | Behavioral | Proven | Complete; Wave A trusted |
| SB03 | Native MAF workflow request/checkpoint foundation | Governed | Proven | Complete; governed proof passed and CP-WB1 approved |
| SB04 | Persistent checkpoint and response recovery state machine | Governed | Proven | Complete; governed proof passed and CP-WB2 approved |
| SB05 | Authorized and idempotent workflow HITL API | Governed | Proven | Complete; governed proof passed and CP-WB3 approved |
| SB06 | End-to-end proof, documentation, and frozen broad gate | Governed | Proven | Complete; 17-row E2E matrix, final documentation/input audit, and FG-01 passed |
| SB07 | Standalone HITL API sample and browser E2E | Governed | Implemented | Technical validation passes; authentic failing-first test evidence and final SHA-256 freeze are absent |

## Wave C re-anchor

Wave C was prepared from branch `maf-update-and-hil`. The working HEAD recorded for this
uncommitted Wave C worktree is `c19cb94bfac710f567db63a93f1e4af69f046cc5`. The standalone
sample targets .NET 10, has no project reference into this checkout, contains exactly twenty
SimWiki JSON articles, and consumes stable identity, idempotent start, run-specific SSE,
canonical readback, governed response, operation status, and artifact APIs.

The standalone Release build and 61/61 tests pass; `node --check` passes. Four affected product
Release builds pass at 0W/0E, as do 71/71 focused Unit and 64/64 focused Integration tests.
Three terminal Playwright conversations prove first-attempt hit, second-attempt hit, and
exactly-three miss with one EventSource and no polling fallback. Canonical API readbacks,
inspected 1280x900 screenshots from the frozen source set, and safety/anti-stub
scans are recorded under `proof/SB07`. The once-only full Integration project passed 982/983
with zero failures; the sole skip is the explicitly opt-in live Ollama catalog test requiring
additional installed model families.

The final repair serializes response-operation replay against lease/state mutation with a
request-scoped PostgreSQL `FOR UPDATE` lock. Migration
`20260822013043_AddWorkflowNativeCheckpointRequestUniqueness` fail-closes on pre-existing
duplicates before adding the filtered unique native-link tuple over session, request, and port.
Known unique-constraint violations map to a typed link conflict; callers roll back the aborted
transaction. The independent race review found no blocker and retained explicit follow-ups.

The historical FG-01 checkpoint remains accepted evidence for SB00–SB06 only. Wave C source
changes do not retroactively alter that recorded result. SB07 has its own focused and live
E2E closure gate.

## Closure notes

Technical validation has no active code blocker. `BG-SB07-02`, final ownership/evidence hash
verification, and both validators pass, but no authentic failing-first test transcript exists for
the behavior-changing recovery work. `BG-SB07-01` is retained as invalidated evidence;
it cannot prove source or schema added after that gate. The pre-fix replay
`DbUpdateConcurrencyException` was reproduced in console diagnostics only and is not relabeled
as governed RED evidence. Runtime workspace, credentials, connection material, raw request
context, and checkpoint payloads remain outside proof.

The historical Wave B source/test freeze remains
`af425ac371b251447f9858b15476092531c686da` with sibling pins Components
`8372c1d55f21b349f8e859470b02eeb4421e96ca` and FileTools
`c95dd07208a6d48724443317cdc6cfe67a13020a`.

FG-01 ran against the final valid frozen checkpoint from
`2026-08-21T12:52:49.8229732Z` through `2026-08-21T13:59:43.2785414Z`. Product restore
was current; the product Release build passed 0W/0E in 51.15s; Stable restore was current;
the Stable Release build passed 0W/0E in 70.13s; and the exact filtered Stable test command
passed 8,471/8,471 with zero failed or skipped tests. Per-assembly results were Components
1,078/1,078, Integration 923/923, AgentFramework.Memory 22/22, Memory 196/196, and Unit
6,252/6,252. A sandbox-only restore denial was retried with the identical command under the
required permission and passed; it was environmental, not a product failure.

The pre-freeze diagnostic history remains explicit. An interrupted Stable run exposed the
Components UI harness missing authorization and AgentFramework UI composition. After the
fixture repair, Components passed 1,078/1,078. The next complete exact diagnostic was red at
8,463/8,470 and exposed seven retained failures: implicit plugin discovery admitted a
non-public nested fixture executor; a stale API contract expected the deliberately removed
idempotency-key hash; in-memory runtime composition/persistence did not preserve the blank
source-response contract and needed a typed construction-time capability fence plus a
cohesive cancellation extraction; and two process-host tests raced child readiness. Focused
repairs passed before the final frozen gate. The earlier SB06 PostgreSQL precision and lease
fence findings, plus the native backend-port message leak, are preserved by append-only
SB03/SB04/SB05 reproof supplements; no frozen parent ledger or TRX was rewritten.

## Re-anchor record

Executed on 2026-08-20. The checkout is `C:\repositories\CanDoItAll`, branch
`maf-update-and-hil`, HEAD `af425ac371b251447f9858b15476092531c686da`. The only commit
after preparation baseline `5cdf1666dbafdcea975909101c1854773f5f3556` adds this bundle;
the product source and tests are unchanged. The worktree was clean at entry.

- Instructions: user `AGENTS.md` contract, bundle `AGENTS.md`, root `README.md`, and
  `CONTRIBUTING.md`; no additional repository `AGENTS.md` or `CLAUDE.md` exists.
- SDK: .NET SDK `10.0.303`, satisfying `global.json` `10.0.302` with `latestPatch`.
- Sibling source anchors: Components `8372c1d55f21b349f8e859470b02eeb4421e96ca`;
  FileTools `c95dd07208a6d48724443317cdc6cfe67a13020a`.
- Restore: `dotnet restore CanDoItAll.slnx` passed; 119 of 120 projects were current and
  the scenario seeder restored.
- Package graph: stable MAF libraries resolve to `1.17.0`; A2A/Hosting preview libraries
  resolve to `1.17.0-preview.260804.1`; no mixed 1.18 graph exists before SB01.
- Source scan: one production `new ChatClientAgentOptions` site, the central
  `MafChatClientAgentOptionsFactory`; no production `FunctionInvokingChatClient`, direct
  `ToolApprovalAgent`, old session-isolation symbols, enabled concurrency, or enabled
  declaration-only storage option.
- Workflow/API/persistence: the prepared exception-as-pause, metadata checkpoint,
  unsupported-resume backend, existing API routes, and persistent store surfaces still
  match current source.
- CodeAnalytics: scoped snapshot `snap-20260820203442-90bdd166` loaded 6 projects and
  364 documents with no blocking diagnostics or project-reference cycle. Existing
  module/type cycles predate this initiative and are outside the planned boundary.
- Discovery and baseline: `MafApprovalSessionRoundTripTests` 12,
  `MafWorkflowAdapterIsolationTests` 4, `WorkflowRuntimeLifecycleRedGateTests` 13,
  `WorkflowApiIntegrationTests` 16. The combined unit selection passed 29/29; the
  authorized integration run passed 16/16.
- Build environment: a focused Release test-project build was not used as closure proof
  because process 42680 holds the web host's Release binaries. It also initially required
  permission to write sibling Components generated assets. Restore plus executed focused
  tests establishes the SB00 baseline without stopping the existing host.

## Decision log

| Decision | State | Rationale |
|---|---|---|
| Keep tool invocation serial by default | Accepted | Ordering and side effects are not generally commutative. |
| Upgrade and HITL in one bundle, separate waves | Accepted | 1.18 is small enough to share discovery, but HITL requires an independent review boundary. |
| Do not enable declaration-only tool storage experiment | Accepted | It is opt-in/experimental and unrelated to required behavior. |
| Use native MAF request ports and JSON checkpoints | Accepted | Exception-as-pause cannot rehydrate a disposed run. |
| Preserve `IsDurable = false` for in-process backend | Accepted | Persisted checkpoints do not create a durable orchestration host. |
| Exactly-once response acceptance and deduplicated participating governed effects | Accepted | Arbitrary external side effects cannot be made exactly once by checkpointing alone. |
| Reuse existing project boundaries; extract top-level collaborators | Accepted | The scoped graph already has the necessary neutral/runtime/adapter/persistence layers; a new project or partial split would add ceremony without fixing a dependency defect. |
| Keep execution/resume on the same backend instance | Accepted | `WorkflowRuntimeManager` selects an execution backend and casts that instance for response resume; the backend delegates resume to a focused concrete driver. |
| Serialize response-operation replay with request mutation | Accepted | PostgreSQL replay now locks the request operation row with `FOR UPDATE`, preserving the request-before-operation lock order used by claim and commit. |
| Enforce one native request tuple per session | Accepted | A filtered unique index over session/request/port is the authoritative cross-context boundary; migration preflight fails visibly on legacy duplicates instead of deleting data. |
| Replace invalidated BG-SB07-01 with BG-SB07-02 | Accepted | The earlier gate remains invalidated; BG-SB07-02 passed 982/983 with zero failures and one declared opt-in live Ollama skip. |
