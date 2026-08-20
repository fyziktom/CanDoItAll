# Traceability Matrix

**Status vocabulary:** Prepared, In progress, Implemented, Proven, Blocked, Deferred.

| Requirement | Owner | Implementation evidence | Proof | Status |
|---|---|---|---|---|
| RQ-001 | SB01 | Central props + resolved package assets | Package scan; affected builds | Prepared |
| RQ-002 | SB01 | Central preview property + resolved package assets | Package scan; hosting/A2A build | Prepared |
| RQ-003 | SB01 | Compile adaptations | Affected project builds | Prepared |
| RQ-004 | SB01 | Resolved graph evidence | No 1.17 scan; assets inspection | Prepared |
| RQ-005 | SB01/SB02 | Migration adaptations and regression suite | Focused MAF/workflow tests | Prepared |
| RQ-006 | SB02 | Central serial policy | Concurrency policy test | Prepared |
| RQ-007 | SB02 | Options factory setting | Architecture/static test | Prepared |
| RQ-008 | SB02 | Custom invocation composition audit | Static scan + behavior probe | Prepared |
| RQ-009 | SB02 | Order/overlap regression fixture | Behavioral test | Prepared |
| RQ-010 | SB02 | No public toggle | Static/API scan | Prepared |
| RQ-011 | SB02 | Experiment remains disabled | Static scan | Prepared |
| RQ-012 | SB02 | Approval/session behavior | MafApprovalSessionRoundTripTests | Prepared |
| RQ-013 | SB03 | Native HumanInput RequestPort binding | Native HITL test | Prepared |
| RQ-014 | SB03/SB04 | Native approval gate binding/token | Approve/deny tests | Prepared |
| RQ-015 | SB03 | Streaming run driver | MAF event/checkpoint test | Prepared |
| RQ-016 | SB03/SB04 | Checkpoint capture and persistence | Adapter + persistence tests | Prepared |
| RQ-017 | SB03/SB04 | Exact definition-version recompile/resume | Rehydrate integration test | Prepared |
| RQ-018 | SB03/SB04 | Topology fingerprint | Mismatch negative test | Prepared |
| RQ-019 | SB03/SB04 | Next-request handling | Consecutive request test | Prepared |
| RQ-020 | SB03/SB05 | Typed approval decision | Approve/deny tests/API | Prepared |
| RQ-021 | SB04 | Cancellation/failure transitions | Lifecycle tests | Prepared |
| RQ-022 | SB03 | Backend descriptor | Descriptor architecture test | Prepared |
| RQ-023 | SB03/SB04 | Fail-closed recovery | Missing/corrupt checkpoint tests | Prepared |
| RQ-024 | SB03/SB04 | Framework-neutral payload port + MAF adapter | Isolation/adapter tests | Prepared |
| RQ-025 | SB03/SB04 | Explicit commit ordinal ordering | Checkpoint index test | Prepared |
| RQ-026 | SB04 | Persistent checkpoint schema | Migration + persistence test | Prepared |
| RQ-027 | SB04 | Response operation CAS/lease | Concurrency test | Prepared |
| RQ-028 | SB04/SB05 | Idempotency key/payload hashes | Service/API tests | Prepared |
| RQ-029 | SB04 | Recoverable operation states | Crash-window test | Prepared |
| RQ-030 | SB04 | Executor invocation deduplication | Side-effect probe | Prepared |
| RQ-031 | SB04/SB06 | Documented guarantee boundary | Architecture/E2E review | Prepared |
| RQ-032 | SB05 | Existing route evolution | API route inventory test | Prepared |
| RQ-033 | SB05 | Typed JsonElement DTO | API integration test | Prepared |
| RQ-034 | SB05 | Claims/service actor resolver | API/service test | Prepared |
| RQ-035 | SB05 | Authorizer | Authorization matrix | Prepared |
| RQ-036 | SB05 | Self-approval prohibition | Negative integration test | Prepared |
| RQ-037 | SB05 | Validator/schema/version/size policy | Validation tests | Prepared |
| RQ-038 | SB05 | Audit record/redaction | Audit test | Prepared |
| RQ-039 | SB05 | Typed outcomes/status mapper | API integration matrix | Prepared |
| RQ-040 | SB05 | Operation/read-model status | API integration test | Prepared |
| RQ-041 | SB00–SB06 | Focused filter/discovery records | Execution reports | Prepared |
| RQ-042 | SB06 | FG-01 frozen broad gate | Build/test transcript | Prepared |
| RQ-043 | SB01/SB05/SB06 | Version/API/runtime docs | Documentation diff | Prepared |
| RQ-044 | SB01/SB02 | Separate Wave A diffs/closure | Execution report | Prepared |
| RQ-045 | SB06 | Final input audit | Traceability closure | Prepared |

## Closure rule

A requirement becomes **Proven** only when the implementation path and a concrete command/test/evidence reference are recorded. A subbundle status alone is insufficient.

Every blocked requirement must identify:

- the exact blocker;
- whether the original request is partially solved or not solved;
- affected downstream requirements;
- the next valid verification action.
