# Verify Office365 email summary creates project asset

## Status

- `Completed`

## Objective

- Prove that the Office365 category summary workflow runs from a project-structure workflow node, fetches the real categorized email, summarizes it, and creates a markdown asset under that workflow node.

## Success Criteria

- API-level integration test completes and creates the asset below the workflow node.
- Live development database run completes with `WorkflowRunState.Completed`.
- Created asset contains the Tetris request facts.
- Created asset parent equals the workflow node id and a workflow-to-asset link exists.

## Covered Inputs

- Requirement R4: preserve downstream project-structure context.
- Requirement R5: summary content captures client request facts.
- Requirement R6: asset is under the workflow node that started the workflow.
- User requirement: test against `candoitall_development` with Office365 OAuth.

## Prerequisites

- SB01 completed.
- Office365 plugin installed and enabled in `candoitall_development`.
- Office365 OAuth connection status is `Connected`.
- A message exists in category `CanDoItAllSummaryTest`.

## Exact Source References

- `repo://Templates/Workflows/workflows/default-workflows.yaml`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentApiIntegrationTests.cs`

## Deliverables

- API integration test for project-scoped LLM workflow asset creation.
- Live Office365 workflow proof from `candoitall_development`.
- Asset content proof showing Tetris, static hosting, keyboard controls, and deadline.
- Lease cleanup after live verification.

## Dependency Impact

- This is the closure subbundle. If it fails, the bug is not fixed for the user scenario even if unit tests pass.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Add API integration coverage using real workflow compiler and project-structure executor.
2. Run focused integration test.
3. Start rebuilt web app against `candoitall_development`.
4. Create verification project and workflow node.
5. Start Office365 category workflow in process.
6. Read project structure and verify created markdown asset content and parent.
7. Release temporary leases and stop the app.

## Scope Exceptions

- The live run intentionally moves the test message from `CanDoItAllSummaryTest` to `CanDoItAllSummaryTestProcessed` after successful processing.

## Do Not Do

- Do not use preview simulation as live proof.
- Do not bypass project-structure lease validation.
- Do not inspect or print OAuth tokens.

## Acceptance Checklist

- Integration test `ProjectStructureAgentApi_llm_workflow_uses_project_scope_and_creates_markdown_asset_under_workflow_node` passes.
- Live run id `af39efd8-a113-4d7b-9364-6228ee14a70a` has run state `4`.
- Created asset id `custom:8b8e41be6b28400f8ac40672c3ccab6b` has parent `custom:a25c66e96c5e44519ebbb8671f3910b0`.
- Asset proof reports `mentionsTetris=true`, `mentionsStaticHosting=true`, and `mentionsKeyboard=true`.

## Proof Required

- `bundle://proof/SB02/transcripts/integration-test.txt`
- `bundle://proof/SB02/transcripts/live-office365-api-run.txt`
- `bundle://proof/SB02/transcripts/live-office365-asset-proof.txt`
- `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB02/semantic-invariants.json`
- `bundle://proof/SB02/manifest.md`

## Browser Validation Logging

- N/A: backend/API workflow validation only.

## Progression Gate

- Final closure may proceed only after integration and live development database proof show completed workflow, created asset, correct parent, and expected summary content.

## Suggested Agent Prompt

```text
Implement SB02 only after SB01 passes. Validate the API-level project-structure workflow path, then run the real Office365 category workflow against the development database and capture asset content proof.
```
