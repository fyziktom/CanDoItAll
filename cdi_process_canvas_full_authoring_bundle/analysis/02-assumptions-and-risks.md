# Assumptions And Risks

## Assumptions

- The additive advanced-node approach can be extended from routers to steps and roles without breaking legacy canvases that still rely on single-anchor nodes.
- The process module can adopt a strongly-typed port inventory without forcing a major architectural rewrite across CanvasLib.
- The current `/processes` workspace route is still the correct real-browser proof surface for this initiative.
- The earlier branch-router work is stable enough to serve as a baseline rather than being reopened as a separate feature stream unless a new generalized-port phase exposes a regression.

## Critical Path Risks

- If subbundle `01-node-inventory-and-port-semantics` gets node applicability or cardinality wrong, every downstream implementation phase will encode the wrong graph contract and later proof will be misleading.
- If subbundle `02-canonical-port-model-and-persistence-foundation` avoids the missing artifact-consumption or port-semantics model work, the canvas may look editable while the database still cannot preserve all authored relationships.
- If subbundle `03-shared-step-node-multi-port-rendering-and-gesture-parity` hardcodes process-specific geometry instead of a reusable advanced-node contract, later node families will drift or duplicate logic.
- If subbundle `05-step-contract-artifact-and-routing-authoring` closes with only direct dependencies and role links working, the user’s literal `all processes via canvas primarily` target will still be false.

## Validation Risks

- Browser proof will be weak if execution validates only one node family instead of a mix of role, step, router, and runtime nodes on the same surface.
- Shared rendering changes live partly in JavaScript and browser layout, so unit or component tests alone will not prove badge alignment, zoom stability, or pill-level connector targeting.
- Artifact-link authoring may require migrations or service-level persistence changes; if so, the validation plan must include integration tests and reload-round-trip proof.
- Runtime projection parity may appear correct visually while still omitting some authored relationships if the scenario set is too small.

## Reopen Triggers

- Reopen subbundle `01-node-inventory-and-port-semantics` if execution uncovers another process node family, step-kind exception, or cardinality rule that was not captured in `architecture/02-node-port-matrix.md`.
- Reopen subbundle `02-canonical-port-model-and-persistence-foundation` if any authored link or node move still snaps back, disappears after reload, or survives only in transient UI state.
- Reopen subbundle `03-shared-step-node-multi-port-rendering-and-gesture-parity` if connector circles drift under zoom, overlap pills, or require node-family-specific hit-testing hacks.
- Reopen subbundle `04-role-participation-authoring-via-canvas` if role outputs still behave as a one-off decision-authority special case instead of a generalized participant-link model.
- Reopen subbundle `05-step-contract-artifact-and-routing-authoring` if artifact flows are still form-only or if step-kind restrictions are enforced only by invisible heuristics rather than visible port semantics.
- Reopen subbundle `06-runtime-projection-scenarios-and-closure` if the final seeded scenarios cannot demonstrate real review, QA, approval, and rework loops from the canvas.
