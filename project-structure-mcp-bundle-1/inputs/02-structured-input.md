# Structured Input

## Objectives

- Build a new MCP server dedicated to CanDoItAll project-structure access.
- Keep CanDoItAll web, DB, and managed files on one main machine while allowing local MCP instances on other machines to connect safely.
- Reuse existing `ProjectsService`, `ProjectWorkbenchService`, workspace settings, and MCP helper patterns instead of inventing a second domain model.
- Add centrally managed policy, approval, and locking so multiple agents cannot mutate the same project scope blindly.
- Provide filtered read tools, checklist extraction, asset access, revisioned asset updates, project and subproject management, and plan/report support.
- Ship setup and reinstall material for other machines.
- Close the work only after real chained end-to-end validation proves the main flows.

## Hard Constraints

- Prefer the smallest correct change that fits the existing architecture.
- Keep code strongly typed.
- Do not use silent fallback behavior that hides policy or locking failures.
- Central data authority must remain on the main CanDoItAll machine.
- Asset reads are readonly. Asset changes create new asset nodes under the original asset node.
- Validation must include real project-structure writes and readback, not only mocked proof.
- Settings for agent permissions must live inside CanDoItAll web.

## Working Assumptions

- The new MCP may be a thin local stdio client that talks HTTP to CanDoItAll web on the main machine.
- The CanDoItAll web app is the best central authority because it already owns DB configuration, managed file storage, workspace settings, and project/workbench services.
- A generated agent token or profile secret is an acceptable first access mechanism for a local-network deployment.
- Import support can start with structured textual/project-description formats that are feasible in the current repo, as long as the import surface is explicitly designed for future format expansion.
- Branch and repository collision prevention can be modeled as centrally coordinated leases, not as direct git integration inside the web app.

## Explicit Non-Goals For This Bundle

- Do not redesign the entire CanDoItAll domain model.
- Do not move existing project/workbench services into a new module unless the current seams prove insufficient.
- Do not require direct remote DB access from MCP machines.
- Do not block the initiative on a future vector database. Use a swappable static guidance provider now.

## Validation Expectations

- Add automated coverage for service, API, and MCP-client behavior, including policy enforcement, locking, checklist logic, and asset handling.
- Add end-to-end proof that creates a project structure chain including a delivery block plus document assets and reads the resulting structure back.
- Add browser proof for the settings UI that manages agent policy and setup instructions.
- Capture execution analytics and closure notes in the bundle execution report.

## Dependency Signals

- Central API, locking, and analytics are the foundation for every later phase.
- Agent policy settings must be implemented before the MCP client can honestly enforce permissions.
- The client/setup phase depends on stable central API contracts.
- Final closure depends on both automated proof and real browser plus tool-level validation.
