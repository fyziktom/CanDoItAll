# Assumptions and risks

## Working Assumptions

- A concept branch may add new projects without requiring production adoption in the same run.
- The new dedicated sandbox project may reference the Processes module for template projection if needed.
- In-memory sandbox edits are sufficient for proving authoring viability.
- Representative templates must include at least one simple, one medium, and one dense scenario.
- Labels may use an HTML/DOM overlay even though geometry is rendered in WebGL.

## Critical Path Risks

- Unrestricted 3D may make process diagrams less readable due to occlusion and camera complexity.
- A WebGL library that references Processes directly would become an architectural dead-end.
- If node drag or connection preview routes through Blazor per frame, the concept will feel broken.
- Asset loading can become fragile if the concept mixes ad-hoc scripts, CDN assets, and repository-native static assets.

## Validation Risks

- Screenshot-only proof is too weak for move/connect interactions.
- Pure WebGL text can look poor or become hard to test across environments.
- Browser timing and camera easing can make screenshots non-deterministic if deterministic mode is not planned early.
- A single trivial template could make the concept look better than it really is.

## Reopen Triggers

- The library stops being universal.
- The default scene becomes free-form 3D without deterministic layout rules.
- The sandbox starts depending on production persistence.
- The automation bridge cannot prove actual state changes.
- Gate A or Gate B fails.
