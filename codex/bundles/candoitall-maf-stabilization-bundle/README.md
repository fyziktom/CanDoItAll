# CanDoItAll MAF Stabilization Execution Bundle

Date: 2026-04-27
Repository input: `/mnt/data/CanDoItAll-agents-integration (1).zip`
Prepared for: Codex implementation and verification

## Purpose

This bundle is an execution-grade plan for stabilizing the CanDoItAll Microsoft Agent Framework integration. It assumes the project is already on the right path: process-driven multi-agent cooperation exists, structured output contracts were recently added, MAF agents are used directly, tool approval exists, MCP integration exists, and the process dispatcher already coordinates real work.

The objective is not to rewrite the system. The objective is to harden the runtime around the areas where MAF already provides native primitives and where the current implementation is still partially custom, incomplete, or too permissive.

## How to use this bundle

1. Start with `shared-prompts/codex-master-prompt.md`.
2. Then run the subbundles in numeric order.
3. Each subbundle is independently scoped, but the recommended order is important because later work depends on earlier runtime contracts.
4. For each subbundle, Codex must produce an implementation report, changed files list, tests run, and remaining risks.
5. Do not skip tests. If tests cannot be run, the reason must be recorded exactly.

## Recommended implementation order

| Order | Subbundle | Why first/next |
|---:|---|---|
| 01 | `01-maf-middleware-tool-governance` | Centralizes policy and tool-call enforcement before more behavior depends on it. |
| 02 | `02-structured-output-continuations` | Fixes a concrete structured-output gap around approval continuations. |
| 03 | `03-contract-validation-repair-runner` | Makes validated typed outputs a general runtime invariant, not only a process dispatcher concern. |
| 04 | `04-finalizer-tools-critical-decisions` | Adds exact-once tool-based finalization for high-risk decisions. |
| 05 | `05-maf-workflows-alignment` | Aligns the process engine with MAF workflows incrementally without rewriting everything. |
| 06 | `06-session-history-context-stabilization` | Prevents session/history from becoming hidden state. |
| 07 | `07-provider-capability-matrix` | Makes model/provider behavior explicit and enforceable. |
| 08 | `08-observability-devui-test-harness` | Makes behavior diagnosable and regression-testable. |
| 09 | `09-runtime-domain-neutralization` | Removes calculator-specific recovery logic from the generic runtime. |
| 10 | `10-docs-tests-release-gates` | Closes the loop with docs and validation gates. |

## Highest-priority findings

1. The repository already applies MAF structured response format for `ProcessStepOutcomeResult`, but approval continuations currently pass `structuredOutput: null` in `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, which can drop the structured-output constraint after a manual approval path.
2. Tool governance is implemented mostly through approval wrappers, MCP validation, and post-run repeated-tool detection. It is not yet expressed as a central MAF-native function invocation policy layer.
3. `MafAgentRuntime.AgentFactory.cs` uses MAF builder middleware for logging, telemetry, and function-call progress, but it does not yet enforce structured-output policy, finalizer requirements, tool allow/deny rules, or destructive-tool policy centrally.
4. Output contracts and `AgentOutputJson` are strong, but concrete validators are still narrow. The only general validator discovered in the core output-validation file is `ProcessStatePatchValidator`; process-step outcome validation is nested in the dispatcher and remains minimal.
5. Finalizer tools are documented but not implemented for critical decisions.
6. The project references MAF workflow checkpointing for pending approvals, but process orchestration is still mostly custom. This is acceptable short-term, but should be aligned incrementally with MAF workflows/orchestrations at step boundaries and review subflows.
7. Built-in tool enablement currently ignores the configured enabled flag: `IsBuiltInToolEnabled(...) => true`.
8. Generic `MafAgentRuntime` contains calculator-specific loop recovery hints. Those should move into scenario/template-specific recovery policy.
9. Provider capability flags are split and somewhat inconsistent. Structured-output support is set as `model.Transport == Responses` in one registry, while managed SQLite defaults force it false.
10. Build/test verification could not be performed in this environment because the `dotnet` CLI is not installed. Codex must run the verification commands in the real repository environment.

## Non-goals

- Do not replace the entire process engine.
- Do not remove working calculator/process automation behavior.
- Do not introduce broad unrelated refactors.
- Do not make agents own process state.
- Do not parse workflow decisions from markdown.
- Do not silently accept invalid structured outputs.

## Source-code comment rule

All source-code comments added by Codex must be in English.
