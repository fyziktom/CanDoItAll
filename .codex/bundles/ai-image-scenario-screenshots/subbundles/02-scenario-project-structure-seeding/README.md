# scenario-project-structure-seeding

## Status

- `Completed`

## Objective

Create one CanDoItAll project per Dev55 scenario app and populate each project structure with app description, technology/runtime, source root, pages/routes, and a delivery block described as `get screenshot of app pages`.

## Success Criteria

- Three project records exist and are readable through the API.
- Each project has nodes for description, technology/runtime, source root, pages/routes, and delivery.
- Each delivery block has exactly the user-requested description text.
- The affected nodes read back through project-structure focused read APIs.

## Covered Inputs

- R4, R5.
- Raw note `N003`.

## Prerequisites

- none

## Exact Source References

- `C:\programovani\candoitall-dev-55-output\run-manifest.json`
- `C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box\README.md`
- `C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box\src\TrailheadSnackBox.Web\Pages\Inventory.cshtml`
- `C:\programovani\candoitall-dev-55-output\scenario-02-dotnet-tool-calibration-log\Components\Pages\Home.razor`
- `C:\programovani\candoitall-dev-55-output\scenario-02-dotnet-tool-calibration-log\Components\Pages\Calibrations.razor`
- `C:\programovani\candoitall-dev-55-output\scenario-03-js-rain-barrel-chore-splitter\src\ui\render.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProjectsApi.cs`

## Deliverables

- Three scenario project records.
- Scenario project-structure nodes:
  - app description;
  - technology/runtime;
  - source root;
  - pages/routes;
  - delivery block.
- Readback artifact listing created project IDs and node IDs.

## Dependency Impact

- Subbundles 05 and 06 need these project IDs, delivery node IDs, and future asset targets.
- If the project structure is missing or only partially readable, process-node and asset proof cannot be trusted.

## Validation Depth

- `Project-structure foundation`

## Implementation Steps

1. Start the CanDoItAll web app if it is not running.
2. Check `/api/access/status` and configure bearer auth if required.
3. Use project APIs to create or reuse scenario project records by stable names.
4. Acquire project leases before project-structure mutation.
5. Create the required nodes using focused project-structure endpoints.
6. Read back each affected project and node set.
7. Record project IDs, delivery node IDs, and page route nodes in `reviews/01-execution-report.md`.

## Scope Exceptions

- Do not start screenshot capture in this phase.
- Do not create generated image assets in this phase.

## Do Not Do

- Do not mutate the external scenario app source roots.
- Do not store app descriptions only in chat or execution notes.
- Do not skip lease/readback proof.

## Acceptance Checklist

- [x] Scenario 01 project exists with `/inventory` page node.
- [x] Scenario 02 project exists with `/`, `/calibrations`, `/calibrations/new`, and detail-route notes.
- [x] Scenario 03 project exists with `/` page node.
- [x] Each delivery block says `get screenshot of app pages`.
- [x] Project-structure readback evidence is recorded.

## Proof Required

- HTTP request/response summaries for create/reuse and readback.
- `reviews/01-execution-report.md` rows with project IDs and delivery node IDs.

## Browser Validation Logging

- N/A unless the project-structure UI is used for manual confirmation. API readback is the primary proof.

## Progression Gate

- Subbundle 05 may not start until Scenario 01 has a readable delivery node.
- The Scenario 01 delivery node must be suitable for a process node and image assets.

## Suggested Agent Prompt

```text
Implement only the scenario-project-structure-seeding subbundle.
Use the CanDoItAll HTTP APIs to create or reuse three projects and their project-structure nodes. Preserve source roots and routes exactly, acquire leases before mutation, read back the affected nodes, and update the execution report.
```
