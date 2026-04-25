# Executive summary

## Mission

Deliver a prepared-only initiative bundle for a concept branch that evaluates whether dense process diagrams from the current `Processes` module should be explored through a **WebGL-backed process workbench with real depth** in Blazor.

## Recommended concept shape

- Add a new universal RCL: `CanDoItAll.Components.WebGlLib`.
- Keep the frame loop, hit-testing, and high-frequency interaction in JavaScript.
- Add a dedicated sandbox project: `CanDoItAll.Components.WebGlSandbox`.
- Reuse real process-template projections rather than toy data.
- Keep all authoring changes **sandbox-only and in-memory**.
- Expose a semantic automation bridge and DOM mirror so Playwright/MCP can test the scene.

## Why this is the right scope

The repository already provides:

- a typed workbench pattern in `CanvasLib`,
- process editor semantics and stable IDs in `Processes`,
- real template processes in `Templates/Processes`,
- an existing Playwright strategy built around semantic canvas helper APIs.

That means the concept can reuse mature patterns while keeping production risk low.

## Non-goals

- No replacement of the production `ProcessWorkspace`.
- No persistence of WebGL sandbox edits into the real process editor.
- No claim that 3D is universally better than 2D.
- No broad UI redesign outside the concept sandbox.

## Go/no-go criteria for the concept

The concept is worth carrying forward only if it proves all of the following:

- dense templates become at least somewhat more readable,
- node movement and connection changes remain understandable in guided 3D,
- the automation bridge can verify real state changes,
- the universal library does not become process-specific.
