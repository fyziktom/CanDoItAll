# SB12 Semantic Invariants

## SB12-DIAG-001 - Typed Runtime Event Diagnostics Render In Workflow UI

- Source raw note: API/UI/Workbench adoption must display workflow failures through typed diagnostics with repair context, not raw exception strings.
- Expected behavior: workflow run summaries, event lists, event detail panels, and technical detail sections prefer `WorkflowEventRecord.PayloadJson` typed diagnostics and show redacted user-safe messages.
- Disallowed shallow implementation: call `WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent.Message)` or render `workflowEvent.Message` directly, which ignores typed event payload diagnostics.
- Adversarial negative proof: `bundle://proof/SB12/transcripts/adversarial-negative-check.txt`.
- Semantic positive proof: `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt`; `Workflow_history_displays_typed_failure_diagnostic_without_raw_message`.
- Passing transcript: `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB12/changed-file-hashes.txt`.
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`; `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`; `bundle://proof/SB12/transcripts/semantic-source-assertions.txt`.
- Red-team negative case: raw workflow event messages are no longer accepted as the UI display path.
- Downstream dependency check: SB13 must re-run no-fallback and no-generic-error adoption hardening before SB14 cleanup.

## SB12-DIAG-002 - Workbench Workflow Status Prefers Typed Event Diagnostics

- Source raw note: Workbench workflow nodes and agent-tool workflow add/create/start/status paths must consume isolated workflow services and preserve repairable failure display.
- Expected behavior: failed Workbench workflow-node status chooses the latest typed error or executor-failed event diagnostic before falling back to sanitized legacy summary text.
- Disallowed shallow implementation: assign run `message` or `workflowEvent.Message` directly into Workbench status summaries, losing payload diagnostics and redaction.
- Adversarial negative proof: `bundle://proof/SB12/transcripts/adversarial-negative-check.txt`.
- Semantic positive proof: `bundle://proof/SB12/transcripts/unit-diagnostics-tests.txt`; `Failed_workflow_status_message_prefers_typed_event_diagnostic`.
- Passing transcript: `bundle://proof/SB12/transcripts/unit-diagnostics-tests.txt`; `bundle://proof/SB12/transcripts/api-workbench-integration-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB12/changed-file-hashes.txt`.
- Production assertions: `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs`; `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentContracts.cs`; `bundle://proof/SB12/transcripts/semantic-source-assertions.txt`.
- Red-team negative case: message-only Workbench failure summary assignment is rejected by the static adversarial check.
- Downstream dependency check: SB13 must repeat focused browser and architecture checks on Workbench adoption before cleanup.

## SB12-DIAG-003 - UI Exception Paths Use Shared Redaction Formatter

- Source raw note: failed workflow/executor/plugin/template states must show user-safe repairable diagnostics and must not display raw secrets, tokens, provider payloads, file contents, or host-command sensitive arguments.
- Expected behavior: workflow page refresh/create/test/cancel/response catches, workflow canvas catches, Workbench workflow-node catches, and cached workflow metadata failure summaries use the shared formatter.
- Disallowed shallow implementation: display `exception.GetBaseException().Message` directly in Blazor or Workbench UI.
- Adversarial negative proof: `bundle://proof/SB12/transcripts/anti-stub-audit.txt`; `bundle://proof/SB12/transcripts/static-adoption-check.txt`.
- Semantic positive proof: `bundle://proof/SB12/transcripts/unit-diagnostics-tests.txt`; `TypedFailureDisplayUsesEventPayloadDiagnostic`.
- Passing transcript: `bundle://proof/SB12/transcripts/unit-diagnostics-tests.txt`; `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB12/changed-file-hashes.txt`.
- Production assertions: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`; `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.WorkflowNodes.cs`; `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.SelectionPanel.cs`.
- Red-team negative case: static adoption check verifies formatter adoption on UI and Workbench diagnostic display paths.
- Downstream dependency check: SB13 no-generic-error scan must verify no new direct exception-message display appears.

## SB12-ADOPT-004 - Browser Proof Is Large-Screen-Only And Covers Workflow/Workbench Adoption

- Source raw note: browser proof must cover workflow page and Workbench workflow-node success/failure paths; current user instruction narrows UI testing to large screens only.
- Expected behavior: workflow shell and Workbench project-structure workflow-node paths pass in large-screen desktop proof; small and medium viewport tests are skipped intentionally.
- Disallowed shallow implementation: claim UI adoption from component tests only, or keep a mobile/small viewport assertion contrary to current large-screen-only scope.
- Adversarial negative proof: `bundle://proof/SB12/transcripts/static-adoption-check.txt`; `bundle://proof/SB12/transcripts/browser-large-screen-scope.txt`.
- Semantic positive proof: `bundle://proof/SB12/transcripts/playwright-workflow-shell-large.txt`; `bundle://proof/SB12/transcripts/playwright-workbench-workflow-node-large.txt`.
- Passing transcript: `bundle://proof/SB12/transcripts/playwright-workflow-shell-large.txt`; `bundle://proof/SB12/transcripts/playwright-workbench-workflow-node-large.txt`.
- Changed source files and hashes: `bundle://proof/SB12/changed-file-hashes.txt`.
- Production assertions: `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs`; `bundle://proof/SB12/browser/workflow-shell-runtime-large.png`; `bundle://proof/SB12/browser/project-structure-workflow-selection-status.png`.
- Red-team negative case: static adoption check verifies the prior mobile/small viewport Workbench segment was removed.
- Downstream dependency check: SB13 and SB14 must repeat only large-screen browser proof for UI surfaces.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `WorkflowEventRecord.PayloadJson` diagnostic payload | `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `bundle://proof/SB12/transcripts/unit-diagnostics-tests.txt` | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`; `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs`; `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt` | `bundle://proof/SB12/transcripts/api-workbench-integration-tests.txt`; `bundle://proof/SB12/transcripts/playwright-workflow-shell-large.txt`; `bundle://proof/SB12/transcripts/playwright-workbench-workflow-node-large.txt` | `bundle://proof/SB12/transcripts/adversarial-negative-check.txt`; `bundle://proof/SB12/transcripts/anti-stub-audit.txt` |
| Shared workflow failure display formatter | `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `bundle://proof/SB12/transcripts/semantic-source-assertions.txt` | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`; `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.WorkflowNodes.cs`; `bundle://proof/SB12/transcripts/static-adoption-check.txt` | `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt`; `bundle://proof/SB12/transcripts/api-workbench-integration-tests.txt` | `bundle://proof/SB12/transcripts/adversarial-negative-check.txt` |

## Raw Note Closure

- API/UI/Workbench adoption: `Solved for SB12`; SB13 still owns adoption hardening and no-fallback checkpoint proof.
- Failure diagnostic display: `Solved for SB12`; typed runtime event diagnostics now drive workflow UI and Workbench status display, with SB13 rechecking no generic fallback.
- Browser proof: `Solved for large-screen-only SB12 scope`; small and medium viewport tests intentionally skipped by user instruction.
- XLSX mapping: `Updated through SB12`; final workbook closure remains SB14.
