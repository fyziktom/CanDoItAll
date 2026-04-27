# Requirements Traceability

| Requirement | Raw audit finding | Owning subbundle | Closure status | Proof |
|---|---|---|---|---|
| R01 | C1 required finalizer is not enabled for the main process path | 01 | Closed | `ExecutionInvocationPolicy`, `ExecutionInvocationMetadata`, and process dispatch set required finalizer mode for governed process-step runs; focused finalizer tests pass. |
| R02 | C2 assistant transcript can diverge from finalized output | 02 | Closed | Execution validation/finalization now runs before assistant message creation on initial and approval-continuation paths; integration tests prove persisted transcript uses finalizer JSON. |
| R03 | C3 output repair/retry is not implemented | 03 | Closed | `IAgentOutputRepairService` and `DefaultAgentOutputRepairService` run bounded repair with revalidation; unit and integration repair tests pass. |
| R04 | C4 provider capability matrix is inconsistent with MAF docs | 04 | Closed | Provider matrix splits function tools, structured output, JSON-schema response format, and approval support; provider matrix tests pass. |
| R05 | C5 `RequireApproval` policy is not enforced by middleware unless wrapper works | 04, 05 | Closed | Tool policy context tracks effective approval paths; MAF middleware blocks `RequireApproval` without one and tags approval effectiveness; unit/static tests pass. |
| R06 | C6 validators are not null-safe | 06 | Closed | Validators now return structured errors for null/missing collections, and validator exceptions become `agent.output.validator_exception`; tests pass. |
| R07 | C7 finalizer support covers only `ProcessStepOutcomeResult`; M2 registry is too narrow | 06, 07 | Closed | Critical contracts are registered in `AgentStructuredOutputContracts.All`; typed finalizer tools exist for each critical DTO; finalizer resolution tests pass. |
| R08 | Build/test evidence is mandatory | 08 | Closed with repo-wide caveat | Restore/build/focused tests pass; repo-wide integration still has unrelated existing failures documented in `docs/agent-runtime-hardening-verification.md`. |
| R09 | M4 calculator recovery guidance is still tied to process automation | 09 | Closed | Recovery guidance provider abstraction added; calculator guidance is selected through provider strategy; static regression test passes. |
| R10 | Observability must include repair/finalizer/tool-policy results | 01, 03, 05, 08 | Closed | Added finalizer, repair, provider capability, and approval effectiveness trace/log tags; static tests and focused validation pass. |

## Raw Note Closure

| Raw note | Status | Evidence |
|---|---|---|
| Required finalizer not enabled on process automation path | Solved | Process dispatch uses `ExecutionInvocationPolicy(FinalizerMode: Required, ...)`; process mock integration passes 7/7. |
| Persisted assistant message can use stale pre-finalizer text | Solved | Assistant messages are created after machine-output finalization; AgentFramework integration passes 8/8. |
| Output repair/retry is modeled but not implemented | Solved | Repair service and bounded completion loop implemented; repair tests pass. |
| Provider matrix oversimplifies structured output and approval support | Solved | Matrix split into explicit capability flags; provider tests pass. |
| `RequireApproval` can continue to `next(...)` without effective approval | Solved | Middleware blocks ineffective approval paths; tool policy tests pass. |
| Validators can throw null-reference exceptions | Solved | Null-safe validators and exception conversion implemented; contract tests pass. |
| Critical DTO finalizers are missing or undocumented | Solved | Typed finalizers and docs added for critical DTOs. |
| Real command proof is required | Solved with caveat | `dotnet --info`, restore, build, focused tests, and repo-wide integration status captured in verification doc. |
| Domain-specific recovery guidance needs a provider abstraction | Solved | Recovery guidance provider abstraction and calculator strategy added. |
