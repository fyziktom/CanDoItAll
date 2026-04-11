# Assumptions And Risks

## Assumptions

- The additive advanced-node approach can be introduced without breaking legacy canvases that still rely on single-anchor nodes.
- The existing process persistence model does not need a new branch entity in storage if the branch node can be derived cleanly from existing step and branch-outcome definitions.
- The browser route `/processes` is stable enough to act as the primary validation surface for this feature.

## Critical Path Risks

- If subbundle `02-advanced-canvas-node-contract` gets the shared port contract wrong, every downstream branch-node screenshot and interaction proof becomes untrustworthy.
- If subbundle `03-process-branch-node-authoring-and-mapping` fakes branch-node creation in UI state without aligning with process-model semantics, the seeded scenarios and regression tests in subbundle `04` will prove the wrong behavior.
- If scenario definition is weak in subbundle `01`, later implementation may accidentally optimize for a simplified happy path and miss review and QA loops that the user explicitly asked for.

## Validation Risks

- Browser proof will be weak if the execution pass does not inspect screenshots for port spacing, label overlap, and line-routing readability.
- Shared-canvas renderer changes may require both component-test and real browser confirmation because the final geometry lives in JavaScript and browser layout.
- Seeding richer process scenarios may require controlled data setup in the workspace before Playwright can exercise the correct branch-node flows.

## Reopen Triggers

- Reopen subbundle `01` if implementation reveals missing branch categories beyond matched outputs, default, error, or role-to-decision input.
- Reopen subbundle `02` if named ports cannot be hit-tested, rendered, or connected cleanly in the browser without breaking legacy node rendering.
- Reopen subbundle `03` if right-click branch creation cannot persist or rebuild the same canvas state after reload.
- Reopen subbundle `04` if the seeded software-development scenarios still collapse into linear flows or cannot demonstrate loops back to repair and QA.
