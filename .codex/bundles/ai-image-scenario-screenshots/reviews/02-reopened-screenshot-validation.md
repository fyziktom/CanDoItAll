# Reopened Screenshot Validation

## Status

- Execution state: `Completed`
- Reopened reason: the previous closure proved Scenario 01 but did not prove the generic screenshot process on the later .NET and JavaScript real cases, and Scenario 02/03 had no project-structure `ImageAsset` nodes.
- Additional UI issue: the Live Processes activity card list clipped instead of scrolling when many process cards were visible.

## Previous Failure Analysis

- Required-tool validation accepted a textual "Project image asset storage receipt" as enough proof for `project_structure_asset_create`; a process could appear complete without a real image asset node.
- The processes API exposed run execution but not enough explicit escalation, assignment, rework, and approval actions for a human advisor to unblock agents safely.
- Screenshot review/storage agents could store files but had no direct image-inspection tool to verify uploaded PNG/JPEG/GIF metadata before creating project-structure assets.
- Capture was assigned through a generic software-engineer role, which allowed wrong-stack agents and wrong hard-required tools.
- JavaScript-only screenshot runs could inherit `workspace_dotnet_*` hard requirements.
- Browser MCP screenshots were written outside the scoped workspace artifact root, so later workspace/file-driver tools could not reliably inspect or pass them to project structure.
- Capture agents sometimes attempted `project_structure_asset_create` even though the intended model is split: QA captures, review/storage writes project structure.
- The .NET app runner forced `--no-launch-profile` without setting Development environment defaults; Blazor static assets failed when the app started as Production.
- A .NET QA run invented `/css/app.css` and `/css/site.css` checks instead of validating assets referenced by rendered HTML.

## Implemented Repairs

- Tightened process required-tool validation so only actual successful `project_structure_asset_create` evidence satisfies screenshot asset storage.
- Added process escalation and operator approval API endpoints for list, create, assign, resolve, reopen, rework, and approval decisions.
- Added `workspace_inspect_image` as a strongly typed workspace tool and seeded it into screenshot review/storage agent access.
- Updated screenshot process templates so capture uses `qa-lead`, capture does not write project-structure assets, and review/storage must inspect images before asset creation.
- Filtered .NET hard-required tools out of clear JavaScript-only run contexts.
- Mirrored Browser MCP screenshots into the scoped workspace artifact root after `browser_take_screenshot`.
- Updated JavaScript QA instructions to avoid the reserved PowerShell `$PID` variable.
- Updated .NET QA instructions to validate static assets from rendered HTML/framework manifests instead of guessed paths.
- Set Development defaults in `workspace_dotnet_run` generated startup plans when the environment is otherwise unset.
- Fixed Live Processes page height ownership so the route wrapper stays viewport-sized and the tabs panel scrolls.

## Real Process Proof

### Scenario 03 JavaScript

- Project: `bf4f6179-4532-43e4-8a5f-0d61d1f952dd`
- Launch plan: `6272aeb9-5315-49c0-8f89-68724ab5828b`
- Run: `5c7b0b3f-ccc0-49db-856e-cbb4f382fce3`
- Capture agent selected by HR: `JavaScript QA Review Lead`
- Result: completed `5/5` steps and created image asset node `custom:f383022ddabd48f1aef856b49baa9ed6`.
- Stored screenshot: `/ - Rain Barrel Chore Splitter - main route (1280x720)`, PNG, `339351` bytes.
- Independent screenshot comparison: `1280x720`, RMS delta `0.897`.
- Evidence: `evidence/reopened-scenario03-fresh-run-final-poll.json`, `evidence/reopened-scenario03-fresh-project-structure-image-assets.json`, `evidence/reopened-scenario03-human-baseline-comparison.json`.

### Scenario 02 .NET

- Project: `7ad50800-d0ed-40b7-96a9-73137e2d41a4`
- Launch plan: `f4bce8ca-0c79-48e2-9575-78a9d057c678`
- Run: `05440ccf-dfbc-415f-aadd-9a5cba3727ab`
- Capture agent selected by HR: `.NET QA Review Lead`
- Result: completed all process steps after human-advisor rework and created four image asset nodes.
- Image assets:
  - `/`: `custom:ea15e8f20bae4095871399c0797f27e6`, `home.png`, PNG, `111025` bytes.
  - `/calibrations`: `custom:2699952f30f343aeb1383b24bb174ffc`, `calibrations.png`, PNG, `81041` bytes.
  - `/calibrations/new`: `custom:9718409d3de243c8aab6f990b24c2685`, `new.png`, PNG, `67653` bytes.
  - `/calibrations/CLR-1001`: `custom:1eded867cb434839b6427af754f475ee`, `detail.png`, PNG, `76169` bytes.
- Independent screenshot comparisons:
  - `/`: RMS delta `0.628`.
  - `/calibrations`: RMS delta `0.609`.
  - `/calibrations/new`: RMS delta `0.475`.
  - `/calibrations/CLR-1001`: RMS delta `0.551`.
- Evidence: `evidence/reopened-scenario02-fresh-run-final-poll-02.json`, `evidence/reopened-scenario02-fresh-project-structure-image-assets.json`, `evidence/reopened-scenario02-human-baseline-comparison.json`.

## Live Processes Scroll Proof

- Route: `http://localhost:5032/processes/live`
- Viewport: `1280x720`
- Visible activity cards: `45`
- Before repair: the route wrapper and `live-processes-page` expanded to about `7452px` inside a `564px` overflow-hidden shell slot.
- After repair: route wrapper and page are `564px`; the selected tab panel is `306px` high with `7178px` scroll height, `overflow-y: auto`, and `scrollTop` moves to `1500`.
- Evidence: `evidence/live-processes-scroll-final.json` and `evidence/live-processes-scroll-final.png`.

## Validation Commands

| Command | Outcome | Notes |
| --- | --- | --- |
| `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~WorkspaceCommandExecutionServiceTests"` | `Passed` | 13 tests. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"` | `Passed` | 330 tests. |
| `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore` | `Passed` | 0 warnings, 0 errors. |
| `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --stage prepared .codex\bundles\ai-image-scenario-screenshots` | `Passed` | Prepared-stage bundle validation. |
| `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --profile initiative --stage completed .codex\bundles\ai-image-scenario-screenshots` | `Passed` | Completed-stage bundle validation. |

## Residual Risks

- The route-wrapper CSS uses `:has()`. Current Chromium, Edge, Safari, and Firefox support it; older browsers would need a layout-level class hook instead.
- The generic screenshot flow is now proven on one .NET and one JavaScript app, plus the previous Scenario 01 proof. More stacks should add stack-specific QA instructions rather than weakening the generic process core.
