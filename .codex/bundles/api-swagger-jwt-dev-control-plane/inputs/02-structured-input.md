# Structured Input

## Core Objective

- Expose a stable HTTP development API for projects, project structure, processes, launch plans, and agents.

## Hard Constraints

- Do not duplicate project/process business rules in endpoint handlers.
- Do not silently fall back when JWT is misconfigured. If JWT is enabled without a valid signing key, fail startup predictably.
- Keep JWT off by default so the app still starts with current local settings.
- Keep endpoint payloads strongly typed. No magic command strings except route names, UI text, and external protocol labels.
- Settings token creation must mask secrets and never log bearer tokens.

## Source Artifacts

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Staffing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.Manager\Program.cs`

## Input Coverage Signals

- N001 API with Swagger and optional JWT for projects, processes, and agents.
- N002 Reuse existing UI/MCP/agent logic and avoid doubled project/process behavior.
- N003 API is for development access when MCP ports/launches are inconvenient.
- N004 Map development-helpful project/process controls, not only basic reads.
- N005 Include process run detail, manager chat/direct messages, and process editing.
- N006 Include project-structure node driven process flow, launch plan execution, and HR matching.
- N007 Include filtering to avoid oversized process testing context.
- N008 Add Settings JWT section and token creation when active.
- N009 Review architecture during execution and repair drift before continuing.

## Dependency And Sequencing Signals

- API auth/OpenAPI foundation must land before endpoint and Settings UI work.
- Endpoint surface must land before final architecture review can verify reuse.
- Settings UI depends on token issuer and auth options.

## Validation Expectations

- Prepared bundle validator passes before implementation.
- `dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj` passes.
- Targeted tests cover JWT options/token issuing, enabled/disabled auth behavior, OpenAPI metadata, and process run filtering.

## UI Validation Strategy

- Settings token UI requires a large-screen browser pass on `/settings?tab=api-access` if the app can launch.
- If browser launch is blocked, record the blocker and run component-level coverage instead.

## Browser Validation Analytics

- Record route, viewport, actions/assertions, screenshot paths, and result in `reviews/01-execution-report.md` for subbundle 03.

## Working Assumptions

- The first API increment lives in `CanDoItAll.Web` because it must follow the active web app port.
- Existing public services are the source of truth and are safe for endpoint use.
- Tokens are self-contained JWTs signed by configured app settings; no database-backed token registry is required for this development feature.

## Primary Risks

- Duplicating process/project behavior in HTTP route handlers.
- Accidentally leaving protected APIs anonymous when JWT is enabled.
- Returning too much process run data and overloading clients.
- Settings UI exposing or logging token signing material.
