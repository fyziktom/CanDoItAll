# Generic Agent Scenarios Workbook

## Status

- `Blocked`

## Objective

- Create a verified XLSX workbook listing generic project-structure agent scenarios where one-call tools, catalog guidance, or skills reduce fragile multi-step agent behavior.

## Covered Inputs

- N008 architecture approach.
- N009 XLSX scenario inventory.
- Architect examples for file assets, movement/repositioning, and node type changes.
- R008.

## Prerequisites

- Subbundle 03 closure proof has identified shipped tool names and remaining gaps.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_node_tooling_bundle\inputs\00-original-request.md`
- `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_node_tooling_bundle\requirements\01-normalized-requirements.md`
- `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_node_tooling_bundle\architecture\01-target-solution.md`

## Deliverables

- One `.xlsx` workbook under `C:\repositories\CanDoItAll\outputs`.
- Scenario list includes shipped selected-node subproject tool, architect-provided examples, and additional candidate user stories.
- Workbook columns include scenario, prompt example, why low-level calls are fragile, proposed tool/skill split, priority, dependencies affected, and validation proof.
- Markdown fallback artifact: `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_node_tooling_bundle\outputs\project-structure-agent-generic-scenarios.md`.

## Dependency Impact

- This is a planning artifact and does not block runtime code after subbundle 03 is proven.

## Validation Depth

- Artifact generation and visual/structural verification.

## Implementation Steps

1. Build scenario rows from raw notes and implemented tool names.
2. Generate workbook with readable formatting.
3. Verify key sheets/ranges and export `.xlsx`.
4. Record workbook path in execution report.

## Scope Exceptions

- Workbook scenarios are recommendations unless explicitly implemented in subbundles 02 or 03.

## Do Not Do

- Do not present future scenarios as shipped tools.
- Do not omit dependency-related scenarios.

## Acceptance Checklist

- Workbook includes the selected-node subproject scenario.
- Workbook includes creating nodes with file assets.
- Workbook includes moving selected/multiple nodes with spacing.
- Workbook includes changing selected node types.
- Workbook includes at least five additional scenarios found during analysis.

## Proof Required

- Workbook exported to `.xlsx`.
- Compact verification output or rendered preview check recorded in execution report.

## Blocker Captured

- `@oai/artifact-tool` is unavailable to the Node runtime, so the Spreadsheets skill contract prevents XLSX generation with an alternate library.
- Scenario content was captured in the Markdown fallback artifact for later XLSX conversion when the spreadsheet runtime is available.

## Browser Validation Logging

- N/A.

## Progression Gate

- Final closure may start only after the workbook exists and is verified.

## Suggested Agent Prompt

```text
Create the generic project-structure agent scenarios workbook. Use shipped implementation names where available, distinguish shipped tools from recommended future tools, verify the workbook, and record the final path.
```
