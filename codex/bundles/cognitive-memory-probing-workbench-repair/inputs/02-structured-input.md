# Structured Input

## Core Objective

- Turn Cognitive Memory probing into an operator-usable maintenance conversation where users can ask random project questions, inspect what memory used, mark facts as correct or wrong, and submit review-gated repairs.

## Success Criteria

- The Cognitive Memory page can start a project-scoped probe session.
- The page can ask an arbitrary user question and show returned answer context, source refs, warnings, and recall trace metadata.
- The user can submit typed feedback actions plus free-text notes/correction text.
- Correction feedback creates a review-gated repair candidate.
- Approval applies the repair through the existing review/consolidation path.
- Validation uses AI Tap/Faucet and Curacao Glass realistic projects.

## Hard Constraints

- Probe feedback must not directly rewrite authoritative memory.
- Review approval must be required before correction feedback becomes active memory.
- Keep actions strongly typed; no magic-string action routing.
- Use existing CanDoItAll Blazor/BaseLib components and existing page CSS conventions.
- Do not add Radzen.
- Do not add sample project source data to production or automated test code.

## Allowed Side Effects

- Code changes in Cognitive Memory advanced services, review UI integration, Cognitive Memory page, and targeted tests.
- Bundle evidence and validation scripts may be added under this bundle.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- The follow-up bundle request must be preserved.
- The implementation request must be executed, not only planned.
- The realistic project validation requirement must use AI Tap/Faucet and Glass factory projects.

## Dependency And Sequencing Signals

- Backend repair semantics must land before UI correction controls.
- UI/browser proof must land before final closure.

## Validation Expectations

- Targeted unit tests.
- API smoke against the realistic projects.
- Browser proof for `/cognitive-memory?projectId=...`.

## Evidence Contract

- Prepared and completed bundle validator output.
- Test command output.
- API smoke JSON evidence.
- Browser screenshots and analytics rows.

## UI Validation Strategy

- First pass: desktop browser viewport around `1600x950`.
- Second pass: narrow viewport around `390x844`.
- Review questions: answer and trace do not overlap, source refs are readable, feedback controls are visible, warnings are not hidden, dense layout remains usable.

## Browser Validation Analytics

- Record route, viewport, actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- The PostgreSQL validation database still contains the two loaded realistic projects.
- The Cognitive Memory page is the correct surface for this workbench.
- Probe correction repair can reuse consolidation candidate review/application semantics.

## Primary Risks

- Feedback could remain performative if it does not create an applicable repair candidate.
- Browser proof could pass a synthetic case but fail on realistic projects with many source refs.
- Approval could mutate memory without enough evidence if review-gating is weakened.
