# Pro Brief: Tetris Escalation After Earlier Root-Cause Repairs

## Request
Analyze why the Tetris software-delivery process still escalates/blocks after earlier escalation-root-cause repairs. The user will provide the whole source tree, but this folder narrows the relevant runtime evidence and source files.

Important boundary: this folder is diagnostics only. Do not treat it as an implementation plan or executed bundle.

## Known Good Contrast
After the earlier root-cause bundle repairs, a calculator app software-delivery process was user-tested and passed without trouble. The Tetris app is a slightly more complex but still simple Blazor app. It still reaches escalation/blocking behavior.

## Current Failing Run
Root run: `c4888f4f-eabd-469f-80a6-3fccf6018a12`

State at capture:
- Status: `NeedsAttention`
- Current step: `qa-validation`
- Step instance: `1ebeadbe-98c9-4e9d-af3b-1e9f69a75c62`
- First event: `2026-07-09T05:09:50.686655-04:00`
- Last event: `2026-07-09T05:25:31.05606-04:00`
- Root run events captured: 62
- Root-run agent executions captured: 6
- Completed child runs captured: 4

Primary final block:
- `process.adapter.required_tool_receipt_missing`
- Missing required current-run process receipts: `workspace_dotnet_run`, `browser_navigate`, `browser_snapshot`, `browser_take_screenshot`, `browser_console_messages`, `workspace_dotnet_stop`
- Recovery decision: `ManagerRequired`
- Policy: `process.current-step-safe-retry-budget-exhausted`
- Retry budget observed: automatic retry `4/3`

## Critical Observed Sequence
1. Initial QA attempts correctly noticed missing runtime/browser proof and selected `repair-required`.
2. A later QA attempt, execution run `32e89fa4-03d2-4c9d-8d21-62f16d00d30d`, selected `quality-accepted` and cited restore/build/test/runtime/browser screenshot evidence.
3. The adapter rejected that acceptance because product file content checks still found default Blazor scaffold content in the generated Tetris app.
4. A final QA attempt, execution run `5499ef7c-0a72-4b44-8355-97589d5eb06d`, selected `repair-required` based on the product content/readback defect.
5. The adapter still blocked the final `repair-required` attempt for missing runtime/browser proof receipts and then exhausted the retry budget.

## Main Questions For Pro
1. Should `qa-validation` require runtime/browser proof receipts when the agent selects `repair-required` because a deterministic product content/readback defect already exists?
2. Is receipt enforcement branch-aware for QA branch decisions, or are required receipts enforced before branch semantics are considered?
3. Did the recovery packet for `process.adapter.product_required_file_content_missing` correctly instruct the QA agent to preserve `repair-required`, but then collide with unconditional capability-scope receipt enforcement?
4. Why did execution run `32e89fa4-03d2-4c9d-8d21-62f16d00d30d` claim the delivered route showed the Tetris product shell when the filesystem snapshot and content gate still show Counter/Weather/MainLayout scaffold content?
5. Are browser evidence refs from agent output being treated as text claims rather than actual current-run tool receipts in the adapter? Compare the agent output evidence refs with `tool-receipts-global.json` for that execution run.
6. Are child-run validation receipts and root-run QA receipts intentionally isolated? If so, does root QA need to rerun browser proof even when child validation already produced .NET proof?
7. Should the process route to `quality-repair` immediately after a branch-valid `repair-required` QA decision without requiring all acceptance-only browser receipts?
8. Does the current safe retry policy consume retry budget on diagnostics that should instead be branch routing decisions?

## Files To Inspect First
Runtime and gate code:
- source-context/repo-files/src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs
- source-context/repo-files/src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionParsing.cs
- source-context/repo-files/src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Types.cs
- source-context/repo-files/src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeStepAssignmentRepairService.cs
- source-context/repo-files/src/Processes/CanDoItAll.Processes.Application/ProcessStepRecoveryInstructionBuilder.cs
- source-context/repo-files/src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs
- source-context/repo-files/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs

Templates:
- source-context/repo-files/Templates/Processes/processes/software-delivery/definition.json
- source-context/repo-files/Templates/Processes/processes/software-delivery/steps/qa-validation.md
- source-context/repo-files/Templates/Processes/processes/software-delivery/steps/qa-recheck.md
- source-context/repo-files/Templates/Processes/processes/software-delivery/steps/quality-repair.md

Tests that encode the current behavior or prior repairs:
- source-context/repo-files/tests/Unit/CanDoItAll.Tests.Unit/DotNetProcessLaunchVariableContributorTests.cs
- source-context/repo-files/tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs
- source-context/repo-files/tests/Unit/CanDoItAll.Tests.Unit/ProcessStepRecoveryInstructionBuilderTests.cs
- source-context/repo-files/tests/Unit/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs

Raw runtime evidence:
- api/target-run.json
- api/target-history.json
- api/agent-execution-runs-list.json
- api/agent-runs/07_09_2026_05-24-10_32e89fa4-03d2-4c9d-8d21-62f16d00d30d/*
- api/agent-runs/07_09_2026_05-25-11_5499ef7c-0a72-4b44-8355-97589d5eb06d/*
- product-output-snapshot/forbidden-scaffold-scan.txt

## Prior Pro Analysis Included
Earlier root cause artifacts are copied into:
- source-context/prior-pro-root-cause/analysis/02-root-causes.md
- source-context/prior-pro-root-cause/plan/01-phase-plan.md
- source-context/prior-pro-root-cause/codex/02-completion-gate-aggregator.md
- source-context/prior-pro-root-cause/codex/03-safe-auto-rework-recovery.md
- source-context/prior-pro-root-cause/codex/04-diagnostic-specific-rework-packets.md
- source-context/prior-pro-root-cause/subbundles/04-sb04-diagnostic-rework-packets/README.md

The current failure looks like the same family: adapter completion gates, branch decisions, safe retry budget, and diagnostic-specific rework instructions still disagree in an edge case.
