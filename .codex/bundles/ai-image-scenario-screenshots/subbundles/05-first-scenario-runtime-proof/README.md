# first-scenario-runtime-proof

## Status

- `Completed`

## Objective

Attach a screenshot process node under Scenario 01’s delivery block, start the process, observe it, repair generic blockers, and prove the process captures, reviews, stores, and reads back the `/inventory` screenshot as an image asset node.

## Success Criteria

- Scenario 01 delivery block has a process node linked to the screenshot template.
- Process run starts and completes or blocks with a specific repaired defect.
- Playwright MCP captures a real `/inventory` screenshot from a running app.
- Review/storage step records that the screenshot is nonblank and relevant.
- The image is stored through project-structure/file storage as an image asset node and content reads back.

## Covered Inputs

- R10, R11, R3.
- Raw note `N006`.

## Prerequisites

- Subbundle 02 closure gate passed for Scenario 01 project/delivery node.
- Subbundle 03 closure gate passed for screenshot templates.
- Subbundle 04 closure gate passed for screenshot capture and review/storage agents.

## Exact Source References

- `C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box\README.md`
- `C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box\src\TrailheadSnackBox.Web\Pages\Inventory.cshtml`
- `C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box\src\TrailheadSnackBox.Web\TrailheadSnackBox.Web.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`

## Deliverables

- Process node under Scenario 01 delivery block.
- Started process run and run ID.
- Playwright MCP screenshot artifact.
- Screenshot review artifact.
- Project-structure image asset node and content readback artifact.
- Generic repairs for any observed failure, with tests/proof.

## Dependency Impact

- Subbundle 06 depends on the screenshot asset node and process proof from this phase.
- If the image exists only as a loose file, layout generation cannot start.

## Validation Depth

- `Process-critical browser and asset closure`

## Implementation Steps

1. Read Scenario 01 project/delivery node IDs from subbundle 02 proof.
2. Create/link a process node under the delivery block.
3. Start the screenshot process for Scenario 01 `/inventory`.
4. Observe run detail, step states, assignments, artifacts, tool receipts, and logs.
5. Repair only generic provider/access/template/storage/runtime defects encountered.
6. Confirm Playwright screenshot is nonblank, route-specific, and free of console errors that affect rendering.
7. Confirm storage/review agent writes an image asset node and content reads back.
8. Update execution report with run IDs, screenshot paths, asset node IDs, and repairs.

## Scope Exceptions

- Do not run all scenarios in this subbundle; this is first-app proof.
- Do not generate improved layouts yet.

## Do Not Do

- Do not manually fake process artifacts outside the run unless explicitly recording a blocker.
- Do not accept a screenshot file without project-structure asset readback.
- Do not add screenshot-specific branches to process core while repairing failures.

## Acceptance Checklist

- [x] Process node exists under Scenario 01 delivery block.
- [x] Process run starts from the project node.
- [x] Playwright MCP screenshot captured `/inventory`.
- [x] Screenshot review passes with explicit nonblank/relevance checks.
- [x] Image asset node and content read back.
- [x] Repairs, if any, are generic and tested.

## Proof Required

- Process run detail and step artifact readback.
- Playwright MCP action log: navigate, wait, console check, screenshot.
- Screenshot file path under bundle `evidence/`.
- Asset readback JSON under bundle `evidence/`.
- Targeted build/tests for any code repair.

## Browser Validation Logging

- Route: Scenario 01 `/inventory`.
- Required viewport: `1600x900` large-screen pass.
- Actions: start app, navigate to resolved URL plus `/inventory`, wait for inventory content, inspect console, capture screenshot.
- Screenshot path: `.codex\bundles\ai-image-scenario-screenshots\evidence\scenario-01-inventory-desktop.png`.
- Review questions: Is the screenshot nonblank? Does it show Trailhead Snack Box Inventory? Are key stock/readiness UI elements visible? Are console/rendering errors absent or nonblocking?

## Progression Gate

- Subbundle 06 may start only after Scenario 01 has a stored screenshot asset node with readable content.
- If this cannot pass, a specific unresolved generic blocker must be recorded and repaired before progression.

## Closure Proof

- Scenario 01 project: `3569901c-dcc2-4f88-a08a-01801bfae9b9`.
- Delivery block: `custom:942d6a0a2f39400ab075c9308a75ae6d`.
- Screenshot process run: `5e499b7a-1a5e-4b98-80bc-ce20f2aa356e`.
- Stored screenshot asset: `custom:ed5db391937e4c17b15641e60770b30b`.
- Managed screenshot storage: `managed-files/project-media/images/3569901cdcc24f88a08a01801bfae9b9/03-inventory-page-5954fabc19fd461b8eca1581d3aa5cd2.png`.
- Evidence: `evidence/scenario-01-final-screenshot-asset-node.json` and `evidence/scenario-01-final-screenshot-asset-content-check.json`.
- Generic repair proof: duplicate artifact projection was fixed in process artifact projection and covered by `ProcessRunAutomationDispatchServiceTests`.

## Suggested Agent Prompt

```text
Implement only the first-scenario-runtime-proof subbundle.
Use the existing project/process APIs to create a screenshot process node under Scenario 01 delivery, start the process, observe it, and repair generic defects only. Capture real Playwright MCP screenshot proof and verify project-structure image asset readback before closing.
```
