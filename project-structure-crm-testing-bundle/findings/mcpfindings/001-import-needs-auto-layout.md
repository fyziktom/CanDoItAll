# MCP Finding 001: Imported Plans Need Auto-Layout Or An Immediate Recompose Step

## What Happened

- The umbrella project and B04 detail plan were successfully imported through the project-structure MCP path.
- Their first persisted layouts were nearly single-column stacks, with the important nodes sharing the same `x` position and only differing by `y`.
- In the browser this made the first-open canvas look broken or incomplete, even though the outline index proved the nodes were present.

## Evidence

- `artifacts/project-structure-crm-testing/evidence/playwright/umbrella-fit-max-1600.png`
- `artifacts/project-structure-crm-testing/evidence/playwright/b04-before-recompose-1600.png`
- Structure readback before repair showed umbrella nodes collapsing onto one `x` lane and B04 nodes doing the same.

## Why This Matters

- A backward-added plan that looks broken on first open will not be trusted by a project manager.
- The problem is not only cosmetic. It can make users think nodes are missing, disconnected, or floating off-screen.

## Repair Performed In This Run

- The umbrella project and B04 project were both repaired live with the built-in `Recompose` action and then re-fit in the browser.

## Recommendation

- After JsonOutline or Mermaid imports, auto-run the same layout normalization the canvas exposes through `Recompose`, or explicitly surface a post-import repair step.
- If auto-layout is not acceptable globally, return a first-class MCP warning when a newly imported structure has low horizontal spread and is likely unreadable on first open.
