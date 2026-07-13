# Normalized Requirements

## Requirement Table

| ID | Priority | Source | Requirement | Acceptance signal |
| --- | --- | --- | --- | --- |
| R01 | P0 | F01, F02 | Operator diagnostics must query AgentFramework observations by exact process run id and step instance id for blocked/failed operator actions. | A blocked step older than `TakePerRun` is still found by exact step query. |
| R02 | P0 | F01, F10 | When AgentFramework observation is absent, operator action and rework must use runtime `StrategyResultReceipt` diagnostics instead of a blind generic retry hint. | Operator summary names outcome, diagnostic code, expected/missing artifact/tool/child state, run id, step id, and next action. |
| R03 | P0 | F10 | Introduce a typed `BlockedStepPacket` or equivalent projection contract consumed by operator actions and manager rework. | Rework prompt includes exact step/run/receipt/artifact/tool/child state and forbids blind retry when diagnostic is missing. |
| R04 | P0 | F05 | Artifact ledger events must be based on the applied finalization result, not the original command result. | Downgraded `Succeeded -> NeedsManager` result does not ledger invalid produced artifacts. |
| R05 | P0 | F06, F07 | Runtime artifact contracts must include semantic descriptors: expectation key, title, primary managed ref, accepted source mapping, validation summary, and materialization mode. | Prompts, diagnostics, and rework packets name artifact keys/titles/refs, not only slot GUIDs. |
| R06 | P0 | F07 | Produced artifact refs must be grounded in actual managed artifact content and stable for unchanged content. | Produced artifact id/hash derives from managed ref and readback content hash after materialization. |
| R07 | P0 | F03, F04 | `StepKind=Subprocess` with a controlled `SubprocessContract` must be runtime-owned by default. | Adapter/coordinator can launch, defer, complete, or block parent without invoking a normal agent to call the launch tool. |
| R08 | P0 | F04, F09 | Parent subprocess artifact bridge must validate accepted/no-go child step and artifact expectation mappings before creating parent evidence. | Parent `solution-skeleton-evidence` is synthesized only from `setup-handoff` or `setup-handoff-after-repair`; `setup-repair-escalation` becomes concrete no-go blocker. |
| R09 | P0 | Local audit | All nine current subprocess parent steps must receive typed accepted/no-go/required-child-evidence planning, not only `prepare-solution-skeleton`. | Template inventory rows all map to typed contract metadata or explicit exception rows. |
| R10 | P1 | F08 | Runtime tool preflight must check exact composed provider/tool authorization for the actual governed process context before agent execution. | Missing/denied `project_structure_process_subprocess_launch`, `workspace_dotnet_build`, or browser/image tool blocks before LLM execution with deterministic diagnostic. |
| R11 | P1 | F09, F11 | Hard template gates must move from prose to typed metadata such as `SubprocessContract`, `CompletionGates`, `RequiredReceipts`, `RequiredPaths`, `RequiredFileContentChecks`, `BranchRules`, and materialization rules. | Template loader rejects subprocess steps or required-output manual skips that lack machine-readable contracts. |
| R12 | P1 | F09 | `AllowsManualSkip=true` on required output steps must be disabled or backed by a typed output-producing already-satisfied branch. | `prepare-solution-skeleton` cannot skip without `solution-skeleton-evidence` or an explicit already-existing skeleton proof contract. |
| R13 | P1 | F05, F06, F07 | Parent synthesized artifacts must include parent run/step ids, parent expectation key/title, child run id, accepted child step/artifact, exact child managed ref, materialization mode, and content hash. | Parent managed evidence file can be audited without inspecting unrelated child folders. |
| R14 | P1 | F12 | New logic must be responsibility-sliced into focused services/contracts and must not expand large partial clusters as final architecture. | Architecture gate confirms no new partial dumping, real test seams, and old large owners shrank or delegated. |
| R15 | P0 | B07 | Regression tests must reproduce the failure class without live LLM/network dependencies. | Tests cover observation truncation, runtime receipt fallback, accepted/repaired/no-go child bridge, artifact ledger downgrade, exact tool preflight, and template validation. |

## Literal Scope Coverage

- `all other processes templates`: covered by R09 and SB01/SB08 inventory/validation across `Templates/Processes/processes`.
- `artifacts templates,etc`: covered by R05, R06, R11, R13 and SB06/SB08 across `Templates/Processes/shared/artifacts` plus per-step artifact expectations.
- `all necessary steps/phases`: represented by SB01-SB09 with critical dependency gates.
- `Use our Csharp skills`: represented by architecture files, pattern selection records, testability plan, CodeAnalytics evidence, and C# architecture gate.

## Non-Goals

- Do not rewrite the entire process module or MAF runtime.
- Do not implement product-specific .NET behavior in generic process runtime core.
- Do not require live LLM calls for unit-level proof.
- Do not make `project_structure_process_subprocess_launch` disappear immediately; keep it as compatibility/manual path when a typed contract explicitly allows agent-owned fallback.
- Do not solve unrelated `Microsoft.OpenApi` advisory warnings in this bundle.
