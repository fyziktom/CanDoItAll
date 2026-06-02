# Source Evidence Summary

## Latest Commit Meaning

The latest development-branch commit adds `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/`. That packet explicitly says it is an input-information packet and intentionally does not propose architecture, subbundles, or implementation work.

This bundle is the missing implementation-ready hardening/refactoring layer built on top of that packet.

## Important Runtime Evidence

Observed by the input packet:

- Host for evidence: `http://localhost:5032`
- Database profile: `candoitall_development`
- Successful process run: `6724b4c8-c774-4880-becc-940a3d7bf155`
- Successful workflow run: `e58cb776-9dcd-4c99-acc4-e3fa0bddead0`
- Workflow input category: `CanDoItAllSummaryTest`
- Process run definition: `b1e435a7-18b7-45fb-bf04-e0b745278c99`
- Process definition version: `78603ab5-053a-4aa5-8c51-e9e419f209d4`
- Project: `bd4b3eea-e18e-47b4-bcd8-d2e749243bb4`
- Completed steps: `9 / 16`
- Capability gaps: `0`
- Missing artifacts: `0`
- Actual cost reported by process: `0.082678`
- Estimated cost/token-like field reported by process: `5360`

The successful process produced a static Blazor WASM Tetris app with SVG rendering, keyboard input, automatic fall behavior, IndexedDB best-score persistence, PWA/static-host assets, build/runtime proof, browser proof, security review, release readiness, rollout, and post-release learning.

## Important Friction Evidence

- A first QA run failed and required recovery before final QA passed.
- A stale or alternate process run id `49fd1354-3625-45c2-b986-7e7f0c0246a7` appeared in agent output lineage; direct process lookup returned 404.
- A `dotnet run` path hit build-output locks because another host process was already running.
- Port/database drift was observed: `5034` was running against `candoitall_codex_graphs_20260601`, while the relevant evidence required `5032` with `candoitall_development`.
- Office365 workflow execution mutated mailbox state by moving a message to a processed category.
- Some executors appear in the workflow catalog but are unavailable in the environment.
- Browser proof rules are distributed across templates, prompts, runtime policy, tool catalog, and agent instructions.

## Repository Delta Highlights Since `6e4f6dae9a4b654fde4243a421d72add4074d8cf`

Focused movement:

- `Templates/Processes`: 73 files, 2,365 insertions, 1,014 deletions.
- `Templates/Agents`: 5 files, 14 insertions, 5 deletions.
- `codex/skills`: 5 files, 470 insertions, 4 deletions.
- `src/CanDoItAll.AgentFramework.Core`: 10 files, 226 insertions, 68 deletions.
- `src/CanDoItAll.AgentFramework.Maf`: 4 files, 62 insertions, 10 deletions.
- `src/CanDoItAll.AgentFramework.Models`: 8 files, 480 insertions, 12 deletions.
- `src/CanDoItAll.Modules.AgentFramework`: 24 files, 3,219 insertions, 97 deletions.
- `src/CanDoItAll.Modules.Processes`: 29 files, 1,443 insertions, 441 deletions.
- Tests increased across integration, unit, and component projects.

## Main Architectural Meaning

The current direction works, but many rules are now duplicated across:

- process template JSON
- process sidecar markdown
- agent instructions
- skill docs
- workflow executor catalog
- runtime policy classes
- dispatch prompt builders
- UI editors
- test fixtures
- API numeric/string enum shape
- project-structure writeback artifacts

The refactor must therefore establish canonical contracts before reshaping individual files.

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
