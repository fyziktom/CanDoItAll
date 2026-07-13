# SB11 Semantic Invariants

## Result

- Status: `Passed`
- Subbundle: `SB11 - MAF Compiler Backend Adapter Isolation`
- Closure date: `2026-06-29`

## Invariants

| Id | Invariant | Proof |
| --- | --- | --- |
| SB11-I01 | MAF workflow compiler/backend/LLM/event/handoff code is owned by `CanDoItAll.AgentFramework.Workflows.MafAdapter`, not `CanDoItAll.AgentFramework.Maf`. | `transcripts/static-architecture-check.txt`; adapter type assembly assertions; old MAF workflow files absent. |
| SB11-I02 | Workflow-owned projects do not reference MAF or the MAF adapter. | `transcripts/static-architecture-check.txt`; `MafWorkflowAdapterIsolationTests.Workflow_owned_projects_do_not_reference_maf_adapter_or_maf_project`. |
| SB11-I03 | Host and AgentFramework module composition delegates MAF workflow wiring through the adapter extension. | `transcripts/semantic-source-assertions.txt`; host uses singleton adapter registration and module uses scoped adapter registration. |
| SB11-I04 | Standard/default executors are composed through `WorkflowExecutors.Standard`, not a legacy MAF built-in fallback alias. | `transcripts/static-architecture-check.txt`; `AddBuiltInWorkflowExecutors` absent from source and tests. |
| SB11-I05 | The MAF backend executes through workflow runtime contracts and preserves handoff/plugin behavior. | `transcripts/integration-adapter-tests.txt`; `MafAgentRuntimeHandoffTests` and `PluginCatalogIntegrationTests` passed 32/32. |
| SB11-I06 | MAF compile failures produce typed, redacted, repairable workflow diagnostics with backend/run/workflow context. | `transcripts/adapter-regression-tests.txt`; `Runtime_compilation_failure_event_carries_typed_redacted_diagnostic_payload`; `MafWorkflowAdapterFailureDiagnostics`. |
| SB11-I07 | Executor/tool/plugin failures still flow through executor core/plugin diagnostics instead of being converted to generic MAF backend errors. | `transcripts/adapter-regression-tests.txt`; `WorkflowExecutorTests`; SB09 executor/plugin diagnostic proof remains intact. |
| SB11-I08 | The moved MAF backend is split by responsibility instead of copied as a monolith. | `transcripts/static-architecture-check.txt`; backend line guard is 421 lines; split files include artifact resolver, progress observer, external request capture, event normalizer, LLM invoker, compiler, and diagnostic mapper. |
| SB11-I09 | Template descriptor validation remains compatible with known but unavailable plugin descriptors after the adapter integration. | `transcripts/integration-adapter-tests.txt`; SB10 `WorkflowTemplatePackLoaderTests` rerun during SB11 repair; `WorkflowTemplatePackValidator` no longer blocks loadability on plugin availability. |

## Browser Scope

SB11 changed adapter/composition/runtime proof only. Browser-visible workflow/API/Workbench adoption remains owned by SB12/SB13/SB14. Small and medium viewport UI tests are skipped for this initiative per the user instruction that the app is large-screen-only.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB11-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB11.
- Expected behavior: The SB11 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB11 transcript set where applicable.
- Passing test: See bundle://proof/SB11/transcripts/ for the SB11 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB11/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB11 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
