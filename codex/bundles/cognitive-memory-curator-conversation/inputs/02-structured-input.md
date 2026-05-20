# Structured Input

## Core Objective

- Add a fluent Cognitive Memory curator conversation mode where the operator can validate, correct, and teach memory through ordinary chat.

## Success Criteria

- A Curator tab exists in Cognitive Memory.
- The tab supports text chat and voice controls.
- The operator can choose `Agent` or `Direct LLM` runtime mode.
- The operator can choose `Short`, `Medium`, or `Long` response length/depth.
- Curator answers preserve recall trace and included memory ids.
- Conversation depth controls both reply guidance and how much recall/aggregation input is used for capture.
- User corrections/new knowledge are captured as trusted, high-confidence memory-improvement artifacts.
- Trusted curator captures skip manual approval while normal probe/review flows remain governed.

## Hard Constraints

- Use existing Cognitive Memory module services and AgentFramework voice services.
- Keep UI logic predictable and thin.
- Preserve actor credit, confidence, priority, recall trace, and affected memory ids.
- Fail predictably when provider, agent, project scope, or persistence prerequisites are missing.

## Allowed Side Effects

- Add Cognitive Memory service contracts, entities/configurations if needed, UI tab/component state, and targeted tests.
- Do not alter global probe feedback approval behavior.

## Source Artifacts

- `SRC-001` through `SRC-006` in `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- The requirement says "must" for two modes, voice both ways, high priority/confidence, approval bypass, correction of the wrong memory used in the answer, and response-length/depth modes that change memory input breadth. Each remains a separate requirement.

## Dependency And Sequencing Signals

- Capture/provenance must be built before runtime modes and UI.
- Runtime modes must share one result contract before UI starts.
- UI and browser proof depend on both backend phases.

## Validation Expectations

- Unit tests for capture classification/persistence and runtime mode behavior.
- Component tests for Curator tab rendering and controls.
- Build/test commands for touched projects.
- Browser proof for `/cognitive-memory`.

## Evidence Contract

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `dotnet build CanDoItAll.slnx`
- Browser route `/cognitive-memory` with desktop and narrow viewport screenshots.

## UI Validation Strategy

- First pass in a large viewport around `1600x900`.
- Verify tab visibility, mode control, transcript, composer, voice controls, captured improvement list, status badges, and no overlapping text.
- Follow with a narrower responsive pass.

## Browser Validation Analytics

- Record route, viewport, browser actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- `ProjectId` is required for meaningful memory recall and correction.
- Default provider/default agent settings are the natural defaults for runtime modes.
- Direct LLM mode can use provider diagnostics chat until a richer provider abstraction is introduced.

## Primary Risks

- Real voice provider proof may be blocked by credentials or microphone access.
- Automatic extraction can over-capture; tests must protect explicit correction/new-fact detection.
- Approval bypass can corrupt memory if implemented globally; scope it to curator conversation only.
