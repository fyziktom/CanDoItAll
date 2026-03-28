# Limitations and assumptions

## What I could verify

I was able to inspect:
- repository structure,
- page/component code,
- JS interop code,
- service-layer code,
- existing component tests,
- existing Playwright/browser tests.

## What I could not verify here

The environment available to me did **not** include the `dotnet` CLI, so I could not directly run:

- `dotnet build`,
- `dotnet test`,
- Playwright browser execution,
- runtime profiling,
- database tracing,
- live screenshot capture from execution.

## Consequence

This bundle is a **static source audit + execution plan**.

It is intentionally strong on:
- line-level evidence,
- architecture diagnosis,
- feature preservation mapping,
- validation sequencing,
- task breakdown.

It is intentionally honest that runtime validation still must happen in your normal repository environment.

## Assumptions used in this bundle

- `CanDoItAll.Components.CanvasLib` is the active shared workbench implementation path.
- InteractiveServer remains the relevant runtime path for the current ProjectStructure surface.
- You want to preserve existing visible behavior while improving architecture and performance.
- You prefer plain JavaScript without TypeScript for the renderer-side work.
- You want to keep typed domain logic in C# wherever it does not hurt hot-path rendering.

## Recommended next step

Use this bundle in a real repo environment where Codex can:
- edit files,
- run the test projects,
- run Playwright,
- inspect screenshot artifacts,
- and rerun until all gates are green.
