# Structured Input

## Scenario Apps

| Scenario | Technology | Topic | Output root | Known pages |
| --- | --- | --- | --- | --- |
| `scenario-01` | .NET Razor Pages | Trailhead Snack Box Inventory | `C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box` | `/inventory` |
| `scenario-02` | .NET Blazor | Tool Calibration Log | `C:\programovani\candoitall-dev-55-output\scenario-02-dotnet-tool-calibration-log` | `/`, `/calibrations`, `/calibrations/new`, `/calibrations/{RecordId}` |
| `scenario-03` | JavaScript/Vite | Rain Barrel Chore Splitter | `C:\programovani\candoitall-dev-55-output\scenario-03-js-rain-barrel-chore-splitter` | `/` |

## Current Scenario Observations

- The prior Dev55 bundle reports that the three scenario apps were produced by internal CanDoItAll agents.
- The prior Dev55 bundle also reports process/runtime issues that were repaired or documented, including Playwright MCP policy/finalizer problems and project-structure metadata persistence gaps.
- Scenario 01 has a README describing a Razor Pages inventory app with stock health, snack-box readiness, and quick quantity adjustments.
- Scenario 02 exposes Blazor routes for dashboard, list, new calibration, and detail views.
- Scenario 03 is a Vite app with in-memory state and no authentication, persistence, or outbound app-network calls.

## Hard Requirements

- Add image-generation providers as first-class, typed configuration.
- Make image generation an agent-allowable default tool/capability with per-agent preferred image provider.
- Seed OpenAI as the first default image-generation provider, using `gpt-image-1-mini` unless validation shows the model is unavailable.
- Keep ComfyUI as an extension point, not a half-implemented fallback.
- Add screenshot capture process templates for one page and multiple pages.
- Add agent templates for app-page screenshot capture, screenshot review/storage, and layout image generation.
- Add project records and project-structure nodes for the three apps.
- Use project structure image asset nodes and file storage driver-backed content for screenshots and generated layout recommendations.
- Run and observe the first scenario process before claiming closure.

## Explicit Non-Goals

- Do not hard-code scenario-page behavior into the generic process runtime.
- Do not make process core aware of OpenAI, ComfyUI, Playwright, screenshot pages, or layout design semantics.
- Do not silently continue when OpenAI credentials or image model access are missing; record a blocked provider-health proof step.
