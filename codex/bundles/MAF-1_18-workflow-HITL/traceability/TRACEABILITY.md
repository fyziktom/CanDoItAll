# Traceability Matrix

**Status vocabulary:** Prepared, In progress, Implemented, Proven, Blocked, Deferred.

| Requirement | Owner | Implementation evidence | Proof | Status |
|---|---|---|---|---|
| RQ-001 | SB01 | Central props + resolved package assets | Package scan; affected builds | Proven |
| RQ-002 | SB01 | Central preview property + resolved package assets | Package scan; hosting/A2A build | Proven |
| RQ-003 | SB01 | Compile adaptations | Affected project builds | Proven — no adaptation required |
| RQ-004 | SB01 | Resolved graph evidence | No 1.17 scan; assets inspection | Proven |
| RQ-005 | SB01/SB02 | Migration adaptations and regression suite | Focused MAF/workflow tests | Proven |
| RQ-006 | SB02 | Central serial policy | Concurrency policy test | Proven |
| RQ-007 | SB02 | Options factory setting | Architecture/static test | Proven |
| RQ-008 | SB02 | Custom invocation composition audit | Static scan + behavior probe | Proven |
| RQ-009 | SB02 | Order/overlap regression fixture | Behavioral test | Proven |
| RQ-010 | SB02 | No public toggle | Static/API scan | Proven |
| RQ-011 | SB02 | Experiment remains disabled | Static scan | Proven |
| RQ-012 | SB02 | Approval/session behavior | MafApprovalSessionRoundTripTests | Proven |
| RQ-013 | SB03 | Native HumanInput RequestPort binding | `proof/SB03`: real request/checkpoint and disposed-run resume in exact 203/203 selector | Proven |
| RQ-014 | SB03/SB04 | Native approval gate/token foundation plus durable response-operation and participating governed-effect invocation identity | `proof/SB03`: approve/deny/tamper; `proof/SB04`: persistent operation/dedup positive and adversarial proof | Proven |
| RQ-015 | SB03 | Streaming native start/resume drivers and request/checkpoint correlator | `proof/SB03`: real MAF event/checkpoint tests in exact 203/203 selector | Proven |
| RQ-016 | SB03/SB04 | Framework-neutral native checkpoint capture with authoritative EF/PostgreSQL payload/session persistence | `proof/SB03`: adapter/order/hash; `proof/SB04`: real PostgreSQL create/index/read/hash and migration proof | Proven |
| RQ-017 | SB03/SB04 | Exact catalog-version recompile and fresh-instance resume from persisted continuation | `proof/SB03`: disposed-run rehydrate; `proof/SB04`: process/service reconstruction and exact-version recovery | Proven |
| RQ-018 | SB03/SB04 | Deterministic topology fingerprint persisted and enforced during recovery | `proof/SB03`: deterministic/mismatch tests; `proof/SB04`: persisted topology and fail-closed reconstruction proof | Proven |
| RQ-019 | SB03/SB04 | Consecutive native waits plus recoverable persistent operation/boundary sequencing | `proof/SB03`: consecutive request; `proof/SB04`: atomic boundary and crash-window proof | Proven |
| RQ-020 | SB03/SB05 | Typed native approval/denial foundation plus governed public decision contract | `proof/SB03`: approve/deny/tamper; `proof/SB05`: typed Web/service outcomes, derived action, denial, self-approval, and executor-authorization proof in the 297/297 Unit plus 137/137 Integration selectors | Proven |
| RQ-021 | SB04 | Explicit cancellation, retryable failure, and terminal failure transitions with terminal-state immutability | `proof/SB04`: direct transition/continuation tests and PostgreSQL race proof in 419/419 + 16/16 selectors | Proven |
| RQ-022 | SB03 | Resume descriptor gated by real store/catalog; in-process backend remains non-durable | `proof/SB03`: composition and descriptor architecture tests | Proven |
| RQ-023 | SB03/SB04 | Exact persisted identity verification and typed fail-closed missing/corrupt/legacy recovery | `proof/SB03`: native verifier negatives; `proof/SB04`: PostgreSQL reconstruction, corruption, and legacy non-resumability | Proven |
| RQ-024 | SB03/SB04 | Framework-neutral payload port, MAF JSON adapter, and persistent PostgreSQL implementation | `proof/SB03`: neutral boundary/direct adapter; `proof/SB04`: architecture assertions and real store proof | Proven |
| RQ-025 | SB03/SB04 | Atomic ordinal allocation and explicit oldest-to-newest ordering in both proof and PostgreSQL implementations | `proof/SB03`: adapter/store ordering; `proof/SB04`: real PostgreSQL ordinal/index proof | Proven |
| RQ-026 | SB04 | Focused persistent checkpoint, request-boundary, response-operation, and invocation schema via migration `20260821021747_AddWorkflowHitlRecovery` | `proof/SB04`: migration build/application, real persistence tests, and no-pending-model-changes result | Proven |
| RQ-027 | SB04 | PostgreSQL response-operation CAS, lease/heartbeat, takeover, and stale-owner rejection | `proof/SB04`: direct lease tests and real PostgreSQL concurrency tests | Proven |
| RQ-028 | SB04/SB05 | Durable operation idempotency is bound to the trusted service/API actor scope, request/version, and canonical payload | `proof/SB04`: PostgreSQL create/replay/conflict; `proof/SB05`: real typed Web/service/PostgreSQL/MAF same-operation replay and changed-payload 409 with no second operation/events | Proven |
| RQ-029 | SB04 | Recoverable accepted/claimed/resuming/retryable/terminal operation transitions and all four declared crash windows | `proof/SB04`: continuation fault injection plus PostgreSQL recovery proof | Proven |
| RQ-030 | SB04 | Exactly-one executor decorator with stable invocation key, lease, replay, mismatch rejection, and participating-effect deduplication | `proof/SB04`: direct decorator tests, production composition, PostgreSQL race, and participating probe | Proven |
| RQ-031 | SB04/SB06 | Architecture states the precise guarantee as exactly-once response acceptance and deduplicated participating governed effects; arbitrary external exactly-once effects remain excluded | `proof/SB04`; SB06 17-row E2E matrix, crash/replay cases, final architecture review, and FG-01 | Proven |
| RQ-032 | SB05 | Existing response POST evolved in place and focused operation-status GET added without a second mutation route | `proof/SB05`: route inventory, OpenAPI, source assertion, and 137/137 Integration selector | Proven |
| RQ-033 | SB05 | Strict bounded typed `JsonElement` body and single idempotency header; double-encoded DTO removed | `proof/SB05`: Web boundary/body-reader and OpenAPI facts in the final Integration selector | Proven |
| RQ-034 | SB05 | HTTP claims, agent governance, and server-owned UI context resolve trusted typed actors | `proof/SB05`: service/caller/actor tests in the 297/297 Unit and 137/137 Integration selectors | Proven |
| RQ-035 | SB05 | Profile/scope/capability authorizer over persisted server-owned launch/request policy | `proof/SB05`: authorization matrix plus real insufficient-scope 403 before operation creation | Proven |
| RQ-036 | SB05 | Autonomous workflow/model/agent self-approval prohibited before mutation or invocation | `proof/SB05`: authorizer and executor-authorization negative facts in the final Unit/Integration selectors | Proven |
| RQ-037 | SB05 | Kind/schema/version/size/depth/duplicate/linkage validation before operation creation | `proof/SB05`: validator, service no-mutation, Web transport, and HTTP matrix facts | Proven |
| RQ-038 | SB05 | Durable accepted-operation audit with protected hashes and structured redacted rejection diagnostics | `proof/SB05`: service logging/audit and safe-projection source/test assertions | Proven |
| RQ-039 | SB05 | Typed service results and exhaustive Web status mapper distinguish terminal, waiting, active, conflict, gone, unprocessable, retryable, and safe terminal failure | `proof/SB05`: required 18-case HTTP matrix in the final 137/137 Integration selector | Proven |
| RQ-040 | SB05 | Authorized operation/read-model GET with replay-independent safe result and next-pending projection | `proof/SB05`: service status, Web endpoint/OpenAPI, real operation status, and projection tests | Proven |
| RQ-041 | SB00–SB06 | Focused discovery floors and zero-test rejection retained through the final frozen gate | SB00 Unit 29/29 + API 16/16; `proof/SB03` 203/203; `proof/SB04` Unit 419/419 + Integration 16/16; `proof/SB05` Unit 297/297 + Integration 137/137; SB06 restart E2E 12/12, retained Unit 7/7, retained Integration 14/14, and FG-01 assembly counts | Proven |
| RQ-042 | SB06 | FG-01 five-command frozen broad gate over product and Stable solution graphs | Product build 0W/0E; Stable build 0W/0E; exact filtered Stable test 8,471/8,471 with zero failed/skipped during `2026-08-21T12:52:49.8229732Z`–`2026-08-21T13:59:43.2785414Z` | Proven |
| RQ-043 | SB01/SB05/SB06 | Version, API/control-plane, runtime, migration/legacy, and guarantee documentation | Wave A package docs; SB05 executable OpenAPI/API contract; final maintained-Markdown validation over 187 files and SB06 closeout audit | Proven |
| RQ-044 | SB01/SB02 | Separate Wave A diffs/closure | Execution report | Proven |
| RQ-045 | SB06 | Original request audited note by note against implementation and proof | `closeout/EXECUTION-REPORT.md` original-input closure table; RQ-001–RQ-045 all Proven; closure checklist complete | Proven |
| RQ-046 | SB07 | Standalone .NET 10 Blazor static-SSR sample, zero product project references | Sample Release build 0W/0E; 61/61 tests; frozen 72-file source-set assertion; three browser journeys; BG-SB07-02 982/983 with zero failures | Implemented — Governed proof incomplete |
| RQ-047 | SB07 | Twenty unique file-backed SimWiki articles with typed list/read/search endpoints | 61/61 sample tests and canonical final-run list/search readbacks | Implemented — Governed proof incomplete |
| RQ-048 | SB07 | Stable published workflow start plus LLM greeting and native HumanInput | Exact definition/two-component readback and mismatch tests; claim-state reconciliation tests; terminal live journeys; native-link migration/race | Implemented — Governed proof incomplete |
| RQ-049 | SB07 | Existing response API plus signal-driven SSE, operation-status reconciliation, and canonical readback | 61/61 sample, 71/71 Unit, 64/64 focused Integration; replay lock; exact 202 operation binding; unchanged-cursor SSE reconnect; final one-EventSource journeys | Implemented — Governed proof incomplete |
| RQ-050 | SB07 | Unrolled maximum-three governed search workflow with success/failure branches | Chess first hit, Cycling second hit, exactly-three miss; no fourth request | Implemented — Governed proof incomplete |
| RQ-051 | SB07 | Server-side token, stable identity/idempotency/cursor/artifact handling | Zero-hit credential/log scan; exact operation/cursor readbacks; message and public-URI redaction tests; final ledgers verified | Implemented — Governed proof incomplete |
| RQ-052 | SB07 | Bounded allow-listed pending-request prompt/contract projection | Safe mapper plus 50/50 Web-boundary facts within the 64/64 focused Integration selector | Implemented — Governed proof incomplete |
| RQ-053 | SB07 | Playwright direct-hit, later-hit, and three-miss journeys | Final run `20260822T055150662Z-3853d604`; three inspected 1280x900 screenshots; clean console; one EventSource per conversation | Implemented — Governed proof incomplete |
| RQ-054 | SB07 | Configuration/run docs and focused validation | Sample 61/61, Unit 71/71, focused Integration 64/64, broad Integration 982/983 with one declared opt-in skip, Playwright 3/3, five Release builds, hashes, and validators pass; failing-first Governed gate incomplete | Implemented — Governed proof incomplete |

## Closure rule

A requirement becomes **Proven** only when the implementation path and a concrete command/test/evidence reference are recorded. A subbundle status alone is insufficient.

Every blocked requirement must identify:

- the exact blocker;
- whether the original request is partially solved or not solved;
- affected downstream requirements;
- the next valid verification action.
