# Capture implementation slice boundary

Restate the parent scope as one bounded implementation slice with assumptions, exclusions, and validation hooks.

This is a scope/intake step, not implementation or validation. Do not run restore, build, test, app startup, browser proof, or screenshot capture here. Do not treat a planned greenfield product root as failed runtime proof just because it does not exist yet.

When the parent scope is a simple full app or one-shot broad deliverable, retain every explicitly named core MVP behavior that must work for the product to be recognizable and useful. Use the upstream project-structure and architecture facts to keep that complete core path bounded; do not silently invent a later slice that the parent process did not schedule.

Exclude only optional polish or a capability assigned to an explicit remaining-slice schedule. Without such a schedule, interaction, typed state transitions, persistence, calculation, search, dashboard, reload restoration, and graceful recovery requirements named by the parent remain mandatory in this slice.

The chosen MVP behavior must not be scaffold-only, setup-only, naming-only, or readiness-only. Scaffolding may be a prerequisite handled by the setup subprocess, but it is not the feature behavior. For a requested app, tool, service, or workflow, the MVP behavior must include the smallest observable product-specific path a user can exercise. Do not defer a named state transition, persistence behavior, calculation, search, recovery path, or other core interaction when it is necessary for the requested product to be recognizable and usable.

Separate the chosen MVP acceptance criteria from downstream validation targets and deferred backlog. Do not put future polish, optional modes, integrations, packaging, or release work into the acceptance criteria for this slice unless they are part of the single chosen behavior. Record those items under exclusions, deferred follow-up, or parent-level validation targets so later slice QA does not block the slice for work intentionally outside the child feature request. Do not move core interactions, state transitions, or persistence requirements into exclusions when those items are the essence of the requested product.

Record the intended product root, the requested app archetype, setup needs, acceptance criteria, excluded work, and downstream validation hooks. The scope packet must also contain one explicit `ProductTargetState` decision. Escalate only when the core deliverable boundary, architecture requirement, app archetype, product root, target state, or validation boundary is contradictory or missing enough that the next generic subprocess would create the wrong product.

## Product target state

Add this record to the scope packet before handing work to architecture:

```text
ProductTargetState
- state: greenfield | existing
- targetRoot: grounded ProductRoot alias or project-structure node
- evidence: current-run project-structure facts, artifacts, or workspace receipts
```

`greenfield` means no authoritative product baseline must be preserved and the declared baseline may be created, even when the target directory already exists or contains preliminary files. `existing` means an authoritative baseline must be retained and modified. `ProductTargetFilesystemState` is a read-only launch observation (`missing`, `empty`, `populated`, `not-directory`, or `unavailable`); use it as evidence, never as the sole decision rule. Do not classify a product as existing merely because a directory exists, a target alias is configured, or an output root is established. Select `existing` only when current-run project structure or an upstream artifact identifies the baseline to retain and its concrete topology or source evidence. Do not classify a product as greenfield merely because a path is missing. When the current project structure requests a new deliverable and supplies no authoritative existing baseline, record `greenfield` with both facts; an empty target reinforces that decision but is not the only evidence.

Copy explicit project-structure facts exactly: features, non-features, solution/project names, target framework, test framework, argument meanings, validation hooks, and no-go constraints. Treat explicit project-structure facts as resolved decisions, not unresolved questions or optional assumptions. Do not add optional behavior, extra controls, alternate SDK guidance, or test-framework defaults that are not grounded in the project structure or upstream artifacts.

If project structure lists visual target assets, copy their ImageAsset node ids, media paths, and target-look notes into the slice acceptance criteria or validation hooks. Do not reduce a listed target image to a generic color/style hint when the requested deliverable has a visible UI.

Preserve interface contracts exactly for the chosen MVP behavior. If the project structure names accepted input modes, command-line flags, file names, validation commands, or UI controls for that behavior, do not replace them with easier alternatives. If the exact contract cannot be implemented by the downstream generic subprocess, escalate that mismatch instead of widening the acceptance criteria.
