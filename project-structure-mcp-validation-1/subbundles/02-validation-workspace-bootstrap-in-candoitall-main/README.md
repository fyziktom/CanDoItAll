# Validation workspace bootstrap in CanDoItAll Main

## Status

- `Completed`

## Objective

- Create and prove the live validation workspace under `CanDoItAll Main`, including project linkage, lease control, and source-asset capture needed for later import and defect recording.

## Covered Inputs

- `N001`
- `N003`
- `N004`

## Prerequisites

- `01-source-analysis-and-project-structure-mapping-foundation` completed
- Prepared bundle validator passed after the final bundle-repair pass

## Exact Source References

- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\inputs\02-structured-input.md`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\architecture\01-target-solution.md`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\plan\01-phase-plan.md`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\inputs\source-artifacts\CanDoItAllInput.xmind`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`

## Deliverables

- Validation child project created and linked under `CanDoItAll Main`
- Project and repo-branch lease proof captured for the live mutation scope
- Source asset or source-reference capture in the validation workspace

## Dependency Impact

- Unblocks raw import and semantic shaping in subbundle 03
- Provides the workspace where defects, proof artifacts, and approval requests can be recorded if later steps fail

## Validation Depth

- Critical foundation
- Requires live mutation proof, readback proof, and browser proof of the created workspace before downstream work may continue

## Implementation Steps

1. Acquire the repo-branch lease for the current repository and branch.
2. Acquire the project lease for `CanDoItAll Main`.
3. Create the validation child project if it does not already exist.
4. Link the validation child project under `CanDoItAll Main`.
5. Read back the hierarchy and structure through the MCP.
6. Capture the source asset or source reference inside the validation workspace for traceability.
7. Open the resulting structure page in the browser and capture proof.

## Scope Exceptions

- This phase is limited to workspace bootstrap and traceability setup, not the broad XMind import.

## Do Not Do

- Do not import the full XMind structure yet.
- Do not shape high-value feature branches under the main project until the workspace and lease proof are stable.

## Acceptance Checklist

- The validation project exists and is reachable from `CanDoItAll Main`.
- Active lease proof exists for the repo branch and the target project scope.
- The validation workspace contains a source reference or captured source asset.
- MCP readback and browser proof agree that the workspace exists.

## Proof Required

- MCP responses for repo-branch lease, project lease, project creation or detection, and subproject link
- Live hierarchy and structure readback after the link
- Large-screen browser screenshot of the validation workspace structure route

## Browser Validation Logging

- Route: `/projects/{validation-project-id}/structure`
- Viewports: large desktop first, then a narrower-width follow-up only if layout meaningfully changes
- Actions: navigate to the route, snapshot the page, confirm the project title and child nodes are visible, capture screenshots
- Evidence paths: record screenshot paths in `reviews/01-execution-report.md`
- Visual review questions: confirm the validation project is visible, readable, and not clipped in the structure canvas

## Progression Gate

- Validation workspace creation and link are readable through the MCP
- Lease proof is active and documented
- Browser proof confirms the workspace is visible in the running app

## Suggested Agent Prompt

```text
Implement this subbundle only. Bootstrap the live validation workspace under CanDoItAll Main, prove the required leases, capture the source package for traceability, and record MCP plus browser evidence before moving on.
```
