# Structured Input

## Core Objective

- Produce a source-grounded Cognitive Memory documentation section that tells maintainers the true current stage, architecture, flows, integration boundaries, validation state, and next roadmap work.

## Success Criteria

- A dedicated `docs/cognitive-memory` folder exists with subfolders for current state, architecture, operations, and roadmap.
- The docs identify the real stage as validation-grade alpha, with explicit beta blockers.
- Mermaid diagrams include architecture-beta, flowchart, class, and sequence examples for the current implementation.
- Existing docs entry points link to the new section instead of leaving Cognitive Memory documentation scattered.
- The bundle preserves requirements, source evidence, subbundle gates, and closure proof.

## Hard Constraints

- Use `candoitall-bundle-workflow`.
- Ground claims in the actual repository, not aspirational design.
- Keep the change documentation-only unless the source audit exposes a blocker that must be fixed first.
- Do not run UI/browser validation for markdown-only changes unless a rendered UI surface is modified.

## Allowed Side Effects

- Markdown documentation and bundle artifacts may be created or updated.
- Runtime code, tests, project files, generated migrations, and UI components are out of scope.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- `src/CanDoItAll.Modules.CognitiveMemory`
- `src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`
- `tests/*/*CognitiveMemory*.cs`
- Prior Cognitive Memory bundle reports under `codex/bundles`.

## Input Coverage Signals

- The user explicitly asked for the bundle workflow.
- The user explicitly asked for the actual Cognitive Memory stage, including alpha caveats.
- The user explicitly asked for a dedicated docs folder with subfolders.
- The user explicitly asked for Mermaid class, sequence, flow, and architecture-beta diagrams.
- The user explicitly asked for roadmap content covering done work and next steps.

## Dependency And Sequencing Signals

- The source audit must complete before the docs can state the true stage.
- The stage assessment and implementation map must complete before diagrams and roadmap can be accurate.
- Existing docs pointers should be updated after the new section is present.

## Validation Expectations

- Bundle validator passes for prepared and completed stages.
- `git diff --check` passes.
- Documentation files contain the required new section, diagrams, and roadmap.
- No runtime test run is required because no code or UI behavior changes are in scope.

## Evidence Contract

- Source audit commands with `rg` and direct file reads.
- Completed bundle execution report.
- Bundle validator output.
- `git diff --check` output.

## UI Validation Strategy

- N/A. This is documentation-only and does not change browser-visible UI or host-visible behavior.

## Browser Validation Analytics

- Each subbundle logs `N/A - documentation-only` in the execution report.

## Working Assumptions

- The current repository source and prior bundle validation reports are sufficient for stage assessment.
- Historical test results can be cited as prior validation evidence, while current closure proof should focus on markdown and bundle integrity.
- The docs should be direct about gaps instead of treating all implemented surfaces as beta-ready.

## Primary Risks

- Overstating maturity would mislead future architecture work.
- Under-documenting projection and automation gaps would make Qdrant or scheduled settings look authoritative when they are not.
- Leaving old docs entry points untouched would keep the information fragmented.
