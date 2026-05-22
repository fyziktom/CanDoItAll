# SB01 Semantic Invariants

## Invariants

| Id | Invariant | Proof |
| --- | --- | --- |
| INV-01 | A project-structure process or workflow that has a project id must pass project scope into MAF context contributors. | `final-workflow-project-scope.trx`, `final-process-context-scope.trx` |
| INV-02 | Missing project scope must not be silently hidden for unrelated untrusted or unscoped runs. | `AgentFinalizerPolicyTests` in `final-unit-process-hardening.trx` |
| INV-03 | Static client web implementation work must prefer an implementation-capable JavaScript agent over an architecture-only agent. | `final-integration-process-hardening.trx` |
| INV-04 | Browser proof launch helpers must be bounded; foreground long-running static servers are rejected at command planning time. | `final-unit-process-hardening.trx` |
| INV-05 | Process prompts must preserve explicit project-structure requirements and concrete output roots. | Live run context brief and completed output root validation |
| INV-06 | Repair attempts can complete when they mutate concrete product deliverables under the grounded external output root. | `final-integration-process-hardening.trx` |
| INV-07 | Security and release agents must scale approval to the declared boundary instead of requiring production controls for a static handoff. | `final-integration-process-hardening.trx` |
| INV-08 | The process-produced app must be validated by independent browser proof, not by process-agent claims alone. | `final-validation-browser-runtime.json`, `final-tetris-runtime.png` |

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle | Validation |
| --- | --- | --- | --- | --- |
| `agentContextWorkspaceScope` metadata | `ProcessRunAutomationDispatchService.ExecutionMetadata` and workflow LLM invoker | `AgentFrameworkWorkspaceExecutionService` and MAF runtime | Written into execution metadata before agent invocation | Project-scope unit and integration tests |
| Process assignment candidate score | `ProcessesService.Launch.Staffing` | Launch plan resolver | Computed at launch planning time | Direct static-client web integration test |
| PowerShell script boundedness decision | `WorkspaceCommandPlanBuilder` | Workspace command execution | Checked before execution command plan is created | Static-server guard unit test |
| Repair proof classification | `ProcessRunAutomationDispatchService.ImplementationProof` and artifact validation | Dispatch completion/retry logic | Evaluated on step outcome receipts | Repair deliverable mutation integration tests |
| Release boundary instructions | Agent and process templates | Security reviewer and release readiness agents | Seeded into catalog and consumed by process steps | Agent seed integration test and live run completion |
| Browser validation result | Codex independent validation | Bundle closure and user acceptance | Captured after live run completion without editing output | `final-validation-browser-runtime.json` |

## Semantic Adequacy

The proof is behavior-backed rather than prose-only. The final app was validated in a real browser path against a local static server rooted at `C:\programovani\dotnet-demo\output`. The browser validation checked loaded scripts, resource origins, canvas pixels, keyboard effects, score text, localStorage persistence, and reset behavior.

The generic runtime changes are not Tetris-specific. They address project-scope propagation, implementation-role staffing, bounded launch helpers, repair proof acceptance, project-structure authority, and release-boundary scaling for any process domain.

