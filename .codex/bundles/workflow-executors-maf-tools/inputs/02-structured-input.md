# Structured Input

## Core Objective

- Add plugin-ready workflow executor nodes that can run real tools during MAF workflow execution.

## Success Criteria

- Executor descriptors and implementations are registered through stable contracts.
- Workflow definitions can persist executor id, settings, and execution policy.
- MAF in-process workflow preview invokes executor implementations instead of pass-through delegates.
- Workflow canvas exposes executor creation through grouped right-click and toolbox UI.
- Document wrapper isolates ClosedXML and spreadsheet workflows can read/write `.xlsx`.

## Hard Constraints

- ClosedXML must only be referenced by `CanDoItAll.Tools.Documents`.
- Missing executor ids, invalid settings, unavailable providers, and timeouts must fail explicitly.
- Do not implement broad custom plugin loading in this phase.
- Do not weaken the 20-scenario and provider validation requirement.

## Allowed Side Effects

- Add a small new source project for document tools.
- Extend workflow model, validation, runtime, and canvas UI surfaces.
- Add targeted unit/component/integration tests and bundle evidence artifacts.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- The executor subsystem must cover files/storage, project structure, HTTP/HTTPS, image generation, and spreadsheets.
- Excel support must include multiple reads/writes and report-style extraction.
- UI must include both right-click second layer and component toolbox.
- Plugin support is a contract requirement, not runtime loading.

## Dependency And Sequencing Signals

- Executor contracts must land before built-in executors and UI.
- Document wrapper must land before spreadsheet executor proof.
- MAF runtime binding must land before scenario validation.
- UI proof must happen after descriptors are available to the canvas.

## Validation Expectations

- Prepared and completed bundle validators.
- Build and targeted tests.
- Browser proof for workflow canvas UI.
- 20 workflow scenario rows.
- gpt-5-mini and Ollama `gptoss20b64k` provider rows.

## Evidence Contract

- `workflow-executors-plan.xlsx` under bundle artifacts.
- `dotnet build CanDoItAll.slnx` or exact blocker.
- Targeted `dotnet test` commands for unit/components/integration.
- Browser screenshots and Playwright actions for UI changes.
- Provider/model command or API proof.

## UI Validation Strategy

- First pass at large desktop viewport on `/agents/workflows`.
- Capture right-click executor submenu open state.
- Capture grouped toolbox open state.
- Capture selected executor node setup panel.
- Add narrower-width pass if layout changes affect the inspector/toolbox.

## Browser Validation Analytics

- Record route, viewport, actions, assertions, screenshot paths, and pass/fail result in `reviews/01-execution-report.md`.

## Working Assumptions

- Existing in-process preview runtime is the implementation target for this bundle.
- Production durable execution remains a follow-up unless already available.
- Existing project-structure services can be reused by a workflow executor adapter.

## Primary Risks

- Executor settings may become stringly typed unless descriptor-backed validation is enforced.
- ClosedXML can leak into AgentFramework projects if the wrapper boundary is not scanned.
- Provider tests can be blocked by local credentials/model availability.
