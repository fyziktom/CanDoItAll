# 09 Runtime Hardening And Optimization Checkpoint

## Status

- `Ready after SB08`

## Objective

- Harden and optimize the reconnected MAF runtime path before exposing new UI/API setup flows, ensuring the migration reduced coupling instead of moving MAF logic, access decisions, or hidden suppression into adapters.

## Success Criteria

- MAF adapters are thin, split by capability kind, and do not own template parsing, seed materialization, or concrete external execution details.
- MAF adapters consume the effective capability set and do not independently hide skills, tools, MCP servers, or MCP tools.
- Runtime error propagation preserves structured diagnostics for template, internal implementation, external tool, MCP lifecycle, timeout, cancellation, and cleanup failures.
- Focused performance scan shows no obvious new per-call parsing, unbounded output reads, sync-over-async, repeated serializer option creation, or large LINQ/materialization in hot call paths.
- Codeanalytics/static review shows no new dependency cycles or large new MAF files introduced by reconnection.

## Covered Inputs

- R01, R02, R08, R09, R11, R12, R13, R14, R15.
- User requirement for hardening/refactoring before the rest of the app reconnects.

## Prerequisites

- SB08 MAF reconnection proof passes.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `bundle://analysis/03-codeanalytics-and-performance-review.md`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://inventories/04-capability-access-policy-test-inventory.md`

## Deliverables

- Runtime hardening report.
- Refactors that keep adapters focused and remove duplicated bridging logic.
- Diagnostics propagation tests for representative runtime failure states.
- Static scan and tests proving no hidden attach-time suppression remains outside the effective capability set path.
- Focused performance/static scan summary.
- Accepted-risk table for deferred runtime coupling/performance concerns.

## Dependency Impact

- SB10 UI/API setup must not be built against runtime services that still hide errors or leak resources.
- SB11 regression proof relies on SB09 as the runtime stability baseline.

## Validation Depth

- `Mandatory runtime hardening checkpoint`

## Implementation Steps

1. Review MAF adapters for direct template parsing, seed materialization, concrete process launching, concrete MCP lifecycle, private duplicated DTOs, or private capability suppression logic.
2. Split adapter helpers by capability kind if a file or method exceeds the guardrail threshold.
3. Run static searches for `AllowedOperations`, skill exclusion, workspace-tool gating, runtime tool name comparisons, and MCP allowlist filtering in MAF attach paths; convert survivors to effective-set inputs or document tested compatibility shims.
4. Run focused performance scans from `analysis/03-codeanalytics-and-performance-review.md` over changed MAF/capability files.
5. Run codeanalytics/static dependency checks to ensure reconnection did not introduce new cycles.
6. Add tests for diagnostics propagation from failed template validation, denied required capability, missing internal tool implementation, external process timeout, MCP list-tools failure, cancellation, and cleanup failure.
7. Confirm runtime composition and suppression diagnostics tests still pass after refactoring.
8. Update `proof/SB09/manifest.md` and `proof/SB09/semantic-invariants.md`.

## Scope Exceptions

- Do not add UI/API setup flows in this checkpoint.
- Do not remove compatibility shims until SB11 regression proof or SB12 cleanup unless tests prove they are unreachable.

## Do Not Do

- Do not accept adapters that call old hardcoded capability switches as fallback.
- Do not accept adapters that make deny/allow decisions outside the shared evaluator.
- Do not add broad refactors unrelated to runtime reconnection.
- Do not close with only successful runtime composition tests; failure propagation proof is required.

## Acceptance Checklist

- MAF adapters are thin and capability-kind scoped.
- No MAF attach path hides capabilities without an `EffectiveCapabilitySet` suppression diagnostic.
- Structured diagnostics survive MAF adapter boundaries.
- No local MCP process/service leaks remain after failure tests.
- Focused performance scan findings are fixed or accepted with rationale.
- Static review shows no new dependency cycle or overgrown MAF file caused by reconnection.

## Proof Required

- Runtime diagnostics test transcripts.
- Runtime composition integration transcripts.
- Static/codeanalytics scan summary.
- Hidden-filter static search summary and suppression diagnostics test transcripts.
- Focused performance scan summary.
- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`

## Browser Validation Logging

- N/A. Browser/UI setup proof starts in SB10.

## Progression Gate

- SB10 and SB11 may start only after SB09 proves runtime reconnection is hardened, diagnosable, and performance-reviewed.

## Suggested Agent Prompt

```text
Implement subbundle SB09 only. Harden the reconnected MAF runtime before UI/API work. Refactor overgrown adapters, preserve structured diagnostics, prove no hidden fallback or hidden suppression outside the effective capability set, run focused performance/static scans, and verify cleanup/cancellation failure paths.
```
