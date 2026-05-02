# Validation And Closure

## Status

- `Completed`

## Objective

Run the final validation suite, close every raw note against implemented behavior, and synchronize the bundle documentation with proof.

## Covered Inputs

- `NOTE-05`
- Final closure for `NOTE-01` through `NOTE-04`

## Prerequisites

- `01-external-workspace-selection` completed.
- `02-project-structure-asset-output-contract` completed.
- `03-storage-and-file-tool-defaults` completed.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.ProjectStructure.Tests\CanDoItAll.Mcp.ProjectStructure.Tests.csproj`
- `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_workspace_assets_bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_workspace_assets_bundle\traceability\01-requirement-traceability.md`

## Deliverables

- Targeted tests executed and recorded.
- Raw note closure table updated to Solved, Partially solved, or Not solved.
- Subbundle gate rows updated.
- Bundle README validation summary updated.
- Final validator run recorded.

## Dependency Impact

- This subbundle closes the bundle.
- If proof is weak, reopen the owning earlier subbundle.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run targeted tests for changed unit/integration/MCP/component areas.
2. Run build if targeted tests do not compile all changed projects.
3. Update execution report proof.
4. Close each raw note with evidence.
5. Run `validate_bundle.py --stage completed`.
6. Repair any validation failures.

## Scope Exceptions

- Any unimplemented remote-storage browsing feature must be recorded as out of scope, not hidden.

## Do Not Do

- Do not mark partially solved raw notes as solved.
- Do not claim browser proof if none was needed or run.

## Acceptance Checklist

- Every subbundle has a closure gate result.
- Every raw note has a closure status and evidence.
- Final validator passes or the blocker is explicit.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter AgentWorkspaceToolAccessMetadataTests --no-restore -m:1` passed.
- `dotnet test tests/CanDoItAll.Mcp.ProjectStructure.Tests/CanDoItAll.Mcp.ProjectStructure.Tests.csproj --filter Node_create_and_update_descriptions_define_mermaid_file_asset_contract --no-restore -m:1` passed.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter CreateCapabilityState_attaches_configured --no-restore -m:1` passed.
- `python C:\Users\dell\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --stage completed C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_workspace_assets_bundle` passed.

## Browser Validation Logging

- N/A unless editor UI visual changes need screenshot proof.

## Progression Gate

- Bundle can close only after tests and bundle validator agree with the raw note closure table.

## Suggested Agent Prompt

```text
Implement subbundle 04 only: run closure validation, update proof, close raw notes, and repair any bundle/test failures before declaring completion.
```
