# Live MCP import, shaping, and repair loop

## Status

- `Completed`

## Objective

- Use the live MCP to import the real XMind package, then shape the imported content into richer subprojects and typed nodes under `CanDoItAll Main`, repairing any discovered MCP defect before closure.

## Covered Inputs

- `N002`
- `N003`
- `N004`
- `N005`

## Prerequisites

- `01-source-analysis-and-project-structure-mapping-foundation` completed
- `02-validation-workspace-bootstrap-in-candoitall-main` completed

## Exact Source References

- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\inputs\source-artifacts\CanDoItAllInput.xmind`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\analysis\03-xmind-summary.json`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\analysis\04-xmind-outline.md`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\architecture\01-target-solution.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureImportService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\ProjectObjectContracts.cs`

## Deliverables

- Successful XMind import run against the live MCP using the real source package
- Richer project and node structure shaped under `CanDoItAll Main`
- Captured repair details if any MCP tool or behavior fails during the process

## Dependency Impact

- Produces the actual live validation output that closure, checklist, analytics, and raw-note audit depend on
- If reopened, invalidates any downstream closure proof that assumed the structure was stable

## Validation Depth

- Critical foundation
- Requires live import proof, live readback proof, and at least one browser-visible confirmation of the shaped hierarchy before closure may continue

## Implementation Steps

1. Use the XMind import tool against the validation workspace with the real `.xmind` archive.
2. Read back the imported structure and confirm the generic import succeeded.
3. Create or link richer subprojects under `CanDoItAll Main` for the largest capability branches.
4. Create typed nodes for important repo, file, environment, infrastructure, and implementation concepts where the source meaning is explicit.
5. Read the resulting structure with filtering to confirm the created hierarchy and keep context small.
6. If any MCP tool misbehaves, repair the code or record the defect immediately, then rerun the affected proof.
7. Capture browser proof on the validation workspace and at least one shaped child project or subtree.

## Scope Exceptions

- This phase does not close the bundle; it only produces the live structure and repair loop evidence needed for final audit.

## Do Not Do

- Do not stop at a successful import response without reading back the resulting structure.
- Do not leave known MCP defects undocumented while continuing downstream.
- Do not flatten all large branches into generic work items when subprojects or typed nodes fit better.

## Acceptance Checklist

- The real XMind archive imports successfully through the live MCP.
- The imported result is readable through the MCP.
- At least the major source domains are represented with richer project or node semantics rather than only generic imported tasks.
- Any discovered failure is either repaired and revalidated or explicitly captured as a defect with proof.
- Browser proof confirms that the shaped structure is visible in the live app.

## Proof Required

- Import request and response against the live MCP
- Filtered structure reads showing the created nodes and subprojects
- Browser screenshots of the validation workspace and one shaped descendant structure route
- If repaired, focused test or run proof for the defect fix plus repeated live validation

## Browser Validation Logging

- Route: `/projects/{validation-project-id}/structure` and at least one shaped child route
- Viewports: large desktop first, then medium-width follow-up on the same routes
- Actions: navigate, snapshot, inspect imported container visibility, confirm shaped child nodes or subprojects, capture screenshots
- Evidence paths: record all screenshot paths and the corresponding route in `reviews/01-execution-report.md`
- Visual review questions: confirm the hierarchy is readable, major branches are visible, and no obvious clipping or overlap obscures the imported structure

## Progression Gate

- The live structure is readable through the MCP after mutation
- Browser proof confirms the visible structure matches the intended shaped hierarchy closely enough for closure audit
- Any newly discovered MCP defect is either repaired and revalidated or explicitly recorded as an open blocker

## Suggested Agent Prompt

```text
Implement this subbundle only. Run the live XMind import through the MCP, shape the resulting structure into richer CanDoItAll projects and node types, repair any discovered MCP defect immediately, and capture MCP plus browser proof before closure work starts.
```
