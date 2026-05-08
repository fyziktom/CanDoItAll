# Structured Input

## Core Objective

- Improve automatic definition-canvas recomposition for complex process graphs.

## Success Criteria

- The main/default path is visually clear and left-to-right.
- Branch routers sit between decisions and routed dependents without covering the main spine.
- Role and executor-related nodes sit near the steps they bind to.
- Step column and lane spacing leaves more readable connector paths.

## Hard Constraints

- Tune automatic positions only; do not redesign the workbench UI or stored process model.
- Keep manual node movement and persistence behavior unchanged.
- Preserve explicit failure for cyclic dependency graphs.
- Do not introduce a new layout library for this pass.

## Allowed Side Effects

- Coordinate values produced by `Recomposition` may change.
- Targeted tests may be updated to assert the clearer geometry.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`

## Input Coverage Signals

- `main path`, `roles/agents/executors`, and `spacing between steps` must remain distinct acceptance concerns.
- The request explicitly says the canvas is generally good, so broad UI redesign is out of scope.

## Dependency And Sequencing Signals

- Layout ownership and algorithm contract must be established before code changes.
- Component-level geometry proof must pass before browser proof is meaningful.

## Validation Expectations

- Component tests must prove branch default routes stay on the main lane and custom routes fan out.
- Component tests must prove role nodes are anchored closer to related steps.
- Existing collision and spacing tests must continue to pass.
- UI proof should exercise the actual process canvas route and record screenshot review when the app can be launched.

## Evidence Contract

- Targeted component test command.
- Build or broader test command if needed.
- Browser route, viewport, actions, assertions, screenshot paths, and result.

## UI Validation Strategy

- Use a large desktop browser viewport first.
- Review whether the default path is readable, branches are separated, role nodes are close to related steps, and connectors are traceable.
- Run a narrower viewport pass only if canvas chrome or layout framing changes in a responsive way.

## Browser Validation Analytics

- Record analytics in `reviews/01-execution-report.md` under `Browser Validation Analytics`.

## Working Assumptions

- Main path means default-route and non-branch structural dependencies.
- Roles, agents, and executors are represented by role requirement nodes and their responsibility or decision-authority links.

## Primary Risks

- Component tests can prove geometry but not full visual clarity alone.
- Browser proof may be blocked by local app launch or seed data availability.
