# CanDoItAll Process, Workflow, Agent Hardening Refactor Bundle V1

Status: **Completed / final red-team gate passed**  
Profile: **initiative**  
Prepared: `2026-06-02T00:05:41Z`  
Target repository: `fyziktom/CanDoItAll`  
Target branch: `development`

## Purpose

This bundle turns the latest development-branch input packet into an implementation-ready refactoring and hardening plan. The goal is not to add new product features first. The goal is to strengthen the current process/workflow/agent platform before additional processes, workflows, agents, skills, tools, MCP integrations, and application-generation scenarios are added.

The latest commit on `development` is treated as a source evidence packet, not as a refactor. It records a successful end-to-end Tetris-style process run, a successful Office365 workflow, current weak spots, runtime evidence, API captures, and repository delta observations. This bundle adds the missing architecture, subbundle sequence, proof gates, test scenario design, and QA contract.

## Non-negotiable Principles

1. **Canonical contracts first.** Do not split files or polish UI before identifying the canonical source for process operations, target scope, artifact satisfaction, tool ids, executor ids, browser proof, provider capability state, enum shape, and run lineage.
2. **No stale proof.** Every runtime proof must bind to the current process run id, process step id, execution run id, artifact path, project id, runtime host profile, database profile, and relevant response/provider ids.
3. **No fake proof.** Browser proof, runtime proof, build/test proof, and usage/cost proof must be artifact-backed and machine-auditable.
4. **Token/cost accounting is a production ledger, not a summary string.** Count every billable provider response that the system causes: normal run, continuation, background poll, structured-output repair, finalizer short-circuit, successful run, failed run after provider call, cancelled/background response when usage is returned, and workflow summarization/model calls.
5. **External side effects are explicit.** Office365/Gmail workflows must distinguish discovery, preview, dry-run, commit, retry, processed marker, idempotency, and unavailable-executor diagnostics.
6. **Generic app generation must remain generic.** The Tetris run is evidence, not a hidden special case. Regression must cover five domain-distinct app prompts.

## Bundle Layout

- `inputs/` preserves the raw request and source evidence summary.
- `analysis/` contains the architectural assessment, risk model, and token/cost audit.
- `requirements/` normalizes the hardening requirements and proof bars.
- `architecture/` defines the intended refactoring seams and canonical contract model.
- `inventories/` lists hotspot files, magic-string surfaces, API/tool surfaces, and E2E scenarios.
- `plan/` defines the dependency map and phase gates.
- `subbundles/` contains nine executable subbundles.
- `templates/` contains the five app-generation regression scenario packets.
- `shared-prompts/` contains implementation, QA, and cost-accounting review prompts.
- `traceability/` maps raw inputs to requirements and subbundles.
- `reviews/` contains the prepared-bundle self-review and seeded execution report.
- `scripts/validate_bundle.py` is a local structural readiness check.

## Subbundle Index

| ID | Name | Critical? | Summary |
| --- | --- | --- | --- |
| SB01 | Canonical contracts and inventory | Yes | Define/verify canonical identifiers, enums, policy contracts, and drift inventory. |
| SB02 | Process dispatch/runtime refactor | Yes | Split process dispatch responsibilities behind canonical contracts and state-transition services. |
| SB03 | Token/cost accounting and provider usage ledger | Yes | Replace summary-only costing with durable provider usage ledger and reconciliation. |
| SB04 | Tool policy, browser proof, and runtime host hardening | Yes | Make tool availability, runtime host identity, browser proof, and cleanup receipts trustworthy. |
| SB05 | Workflow executor side effects and idempotency | Yes | Harden Office365/Gmail/external side-effect executor lifecycle and diagnostics. |
| SB06 | Agent, skill, template canonicalization and active sync | Yes | Align agents/skills/templates/API docs to the canonical contracts and prove active skill sync. |
| SB07 | UI editor and observability hardening | No | Refactor workflow/process/provider UI around typed DTOs, source-of-truth display, and proof UX. |
| SB08 | Multi-domain process E2E regression suite | Yes | Run Tetris plus four more simple apps through the real process path. |
| SB09 | Final red-team QA and release gate | Yes | Red-team fake proof, stale lineage, cost reconciliation, side effects, and genericity. |

## Readiness Gate Result

Prepared-bundle structural validation result is recorded in:

- `bundle://reviews/02-prepared-validation-result.txt`

The bundle is ready for Codex execution only if the executor first reruns:

```powershell
python scripts/validate_bundle.py --stage prepared
```

and then applies the repository's `candoitall-bundle-validator` skill.

## Primary Source References

Use portable references only when implementing. Absolute local paths from captured evidence are context, not authority.

### Latest development commit evidence packet

- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/README.md`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/analysis/01-current-state-input-summary.md`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/analysis/02-observed-weak-spots.md`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/01-live-runtime-evidence.md`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/02-repository-delta-since-6e4f6dae.md`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/03-agent-tools-skills-mcp-evidence.md`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inventories/01-hotspot-files-and-apis.md`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/api-captures/process-run-6724-detail.json`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/api-captures/agent-execution-runs-for-process-6724.json`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/api-captures/workflow-run-e58-detail.json`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/api-captures/workflow-run-e58-events.json`

### Bundle skill references

- `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-execution/references/semantic-adequacy-proof.md`
- `repo://codex/skills/bundles/candoitall-bundle-execution/references/artifact-backed-proof-manifest.md`

### Runtime and code hotspot references

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`
- `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Staffing.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`

### Template and skill references

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/software-delivery/definition.md`
- `repo://Templates/Processes/seed-catalog/baseline-scenarios.json`
- `repo://Templates/Agents/manifest.json`
- `repo://Templates/Agents/teams/dotnet-delivery/members/blazor-application-developer/instructions.md`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/instructions.md`
- `repo://Templates/Agents/teams/delivery-platform/members/delivery-manager/instructions.md`
- `repo://codex/skills/candoitall-api-agents/SKILL.md`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`
- `repo://codex/skills/candoitall-api-workflows/SKILL.md`
- `repo://codex/skills/candoitall-api-project-structure/SKILL.md`
