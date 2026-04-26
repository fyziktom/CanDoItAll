# root-and-project-readme-refresh

## Status

- `Completed`

## Objective

- Improve root/docs architecture navigation and add README coverage for every tracked project directory.

## Covered Inputs

- `N001`: out-of-date docs.
- `N002`: repair docs to match actual architecture.
- `N005`: improve README with overview diagram.
- `N006`: all project/library READMEs.

## Prerequisites

- `02-architecture-diagram-and-process-doc-refresh` completed and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\docs\ui-shared-components\README.md`
- `C:\repositories\CanDoItAll\docs\ui-shared-components\architecture\stack-and-architecture.md`
- `C:\repositories\CanDoItAll\Tailwind\README.md`
- `C:\repositories\CanDoItAll\Templates\README.md`

## Deliverables

- Improved root `README.md` with overview diagram and architecture links.
- New `docs/README.md`.
- New `architecture/README.md`.
- Repaired shared-component architecture docs.
- Project README files under every tracked `.csproj` directory in `src`, `tests`, and `tools`.

## Dependency Impact

- `04` depends on this subbundle for README coverage validation and raw note closure.

## Validation Depth

- `Documentation coverage`

## Implementation Steps

1. Rewrite root README around current architecture and workflows.
2. Add docs and architecture indexes.
3. Repair UI shared-component docs for the split component-library architecture.
4. Generate concise project README files from actual `.csproj` metadata.
5. Run README coverage check.

## Scope Exceptions

- Historical bundle docs and architecture review reports are not rewritten unless they actively point readers at stale current architecture.

## Do Not Do

- Do not edit generated build output under `bin` or `obj`.
- Do not remove historical ADRs or review records.
- Do not change product code.

## Acceptance Checklist

- Root README contains a current overview diagram.
- Docs index links to architecture-beta and key docs.
- Shared-component docs describe `Common`, `BaseLib`, `CanvasLib`, `OverlayLib`, `WebGlLib`, facade, and sandbox roles.
- Project README coverage script reports zero missing files.

## Proof Required

- README coverage script output.
- `git diff --check`.

## Browser Validation Logging

- N/A. This subbundle changes Markdown documentation only.

## Progression Gate

- Passed. Root README, docs indexes, shared-component docs, and 61 project READMEs are complete, and the README coverage script reports zero missing files.

## Suggested Agent Prompt

```text
Execute subbundle 03 only. Improve root/docs navigation, repair stale shared-component docs, and add concise README files for every tracked project directory.
```
