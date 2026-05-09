# Structured Input

## Core Objective

- Make project-structure page titles project-specific and make project-structure agents materially better at correct typed-node creation and complex node workflows.

## Success Criteria

- The document title for an opened project structure resolves to `PS - <project name>` and truncates long names with `...`.
- Agents can discover a canonical node catalog that includes all project object types and important subtypes, including `WorkItem/task`.
- The contextual project-structure chat prompt exposes the currently selected node IDs.
- A one-call operation can create a new subproject and move selected nodes, with descendants, into it while keeping parentage valid.
- Internal dependency links between moved nodes remain available in the target project and dependency query still supports Gantt inputs.
- A verified XLSX workbook lists generic scenarios where agents need prepared tools or strong project-structure skills.

## Hard Constraints

- Preserve literal `PS - ` prefix.
- Truncate long names with a substring plus `...`; do not only rely on visual CSS.
- Do not reintroduce the removed ProjectStructure MCP; use web API/internal tools.
- Project block variants remain `ProjectBlock` plus lowercase subtype; work tasks remain `WorkItem` plus `task`.
- Project-structure mutations must use leases where the existing API/service requires them.
- Selected-node prompts must not guess from visible titles when concrete selected node IDs are available.
- Dependency links are `DependsOn`, where the source node depends on the target node.

## Allowed Side Effects

- Add public project-structure DTOs and service methods needed by agent tools.
- Add HTTP/internal MAF tools for catalog and selected-node/subproject workflow.
- Add targeted tests and generated planning workbook output.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`

## Input Coverage Signals

- N001 page title is separate and can be closed independently.
- N002/N003 require both current-state analysis and a durable tool-facing fix.
- N005/N006 require selected-node context plus a mutation operation; context alone is not enough.
- N007 requires dependency preservation/communication, not merely node moves.
- N009 requires an XLSX artifact, not a markdown table only.

## Dependency And Sequencing Signals

- Page-title work is independent.
- Node catalog and selected-node context should land before relying on the new selected-subproject tool.
- The selected-subproject service is a critical foundation for the agent scenario workbook because it becomes the concrete first high-value one-call tool.
- The workbook should reflect shipped tool names and any explicit follow-up candidates.

## Validation Expectations

- Targeted component test for page title and selected-node context prompt/metadata.
- Targeted integration/service test for work-task creation semantics and node catalog contents.
- Targeted integration/API test for selected nodes to new subproject preserving parentage and internal dependency links.
- MAF runtime test updated to include newly attached internal tools.
- XLSX workbook generated under `outputs/` and verified.

## Evidence Contract

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructurePage`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter ProjectStructureAgent`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter MafAgentRuntimeTests`
- Workbook path recorded in `reviews/01-execution-report.md`.

## UI Validation Strategy

- Prefer component tests for PageTitle and prompt assembly.
- If the app is already running or a stable watch target is available, open `/projects/{id}/structure`, verify the browser title and contextual chat selected-node context, then record the route and screenshot.
- If browser proof is not captured, record the explicit validation gap and rely on focused component/build proof.

## Browser Validation Analytics

- Subbundle 01 records route `/projects/{projectId}/structure`, title assertion, and screenshot if browser proof is possible.
- Subbundle 02 records contextual prompt/metadata proof by test; browser proof is optional unless a rendered chat flow is opened.

## Working Assumptions

- A truncation ceiling of 48 characters including ellipsis is sufficient for browser titles and visible tab labels.
- Moving selected nodes should include descendants by default, because leaving descendants behind would surprise users and can orphan meaningful branch context.
- Cross-project links to nodes that did not move should be removed, matching existing descendant-transfer behavior.

## Primary Risks

- Tool schema changes may affect MAF runtime tool attachment tests.
- Moving arbitrary selected nodes could mishandle parentage if selected parent/child combinations are not normalized before the move.
- Workbook generation may be blocked if the artifact-tool workspace dependency is unavailable; a fallback must be recorded rather than guessed.
