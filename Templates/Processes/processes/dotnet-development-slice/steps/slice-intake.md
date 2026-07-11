# Capture implementation slice boundary

Restate the parent scope as one bounded implementation slice with assumptions, exclusions, and validation hooks.

This is a scope/intake step, not implementation or validation. Do not run restore, build, test, app startup, browser proof, or screenshot capture here. Do not treat a planned greenfield product root as failed runtime proof just because it does not exist yet.

When the parent scope is a simple full app or one-shot broad deliverable, retain every explicitly named core MVP behavior that must work for the product to be recognizable and useful. Use the upstream project-structure and architecture facts to keep that complete core path bounded; do not silently invent a later slice that the parent process did not schedule.

Exclude only optional polish or a capability assigned to an explicit remaining-slice schedule. Without such a schedule, interaction, typed state transitions, persistence, calculation, search, dashboard, reload restoration, and graceful recovery requirements named by the parent remain mandatory in this slice.

The chosen MVP behavior must not be scaffold-only, setup-only, naming-only, or readiness-only. Scaffolding may be a prerequisite handled by the setup subprocess, but it is not the feature behavior. For a requested app, game, tool, or workflow, the MVP behavior must include the smallest observable product-specific path a user can exercise. For a game, that means a playable primary loop with real input/state transitions; for a persistence requirement such as a local best score, do not defer persistence if it is part of the named core loop.

Separate the chosen MVP acceptance criteria from downstream validation targets and deferred backlog. Do not put future polish, extra modes, optional integrations, packaging, or release work into the acceptance criteria for this slice unless they are part of the single chosen behavior. Record those items under exclusions, deferred follow-up, or parent-level validation targets so later slice QA does not block the slice for work intentionally outside the child feature request. Do not move every gameplay, interaction, or persistence requirement into exclusions when those items are the essence of the requested product.

Record the intended product root, whether it is existing or greenfield, the requested app archetype, setup needs, acceptance criteria, excluded work, and downstream validation hooks. Escalate only when the core deliverable boundary, architecture requirement, app archetype, product root, or validation boundary is contradictory or missing enough that the next generic subprocess would create the wrong product.

Copy explicit project-structure facts exactly: features, non-features, solution/project names, target framework, test framework, argument meanings, validation hooks, and no-go constraints. Treat explicit project-structure facts as resolved decisions, not unresolved questions or optional assumptions. Do not add optional behavior, extra controls, alternate SDK guidance, or test-framework defaults that are not grounded in the project structure or upstream artifacts.

If project structure lists visual target assets, copy their ImageAsset node ids, media paths, and target-look notes into the slice acceptance criteria or validation hooks. Do not reduce a listed target image to a generic color/style hint when the requested deliverable has a visible UI.

Preserve interface contracts exactly for the chosen MVP behavior. If the project structure names accepted input modes, command-line flags, file names, validation commands, or UI controls for that behavior, do not replace them with easier alternatives. If the exact contract cannot be implemented by the downstream generic subprocess, escalate that mismatch instead of widening the acceptance criteria.
