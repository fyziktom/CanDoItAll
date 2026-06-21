# Capture implementation slice boundary

Restate the parent scope as one bounded implementation slice with assumptions, exclusions, and validation hooks.

This is a scope/intake step, not implementation or validation. Do not run restore, build, test, app startup, browser proof, or screenshot capture here. Do not treat a planned greenfield product root as failed runtime proof just because it does not exist yet.

Record the intended product root, whether it is existing or greenfield, the requested app archetype, setup needs, acceptance criteria, excluded work, and downstream validation hooks. Escalate only when the core deliverable boundary, architecture requirement, app archetype, product root, or validation boundary is contradictory or missing enough that the next generic subprocess would create the wrong product.

Copy explicit project-structure facts exactly: features, non-features, solution/project names, target framework, test framework, argument meanings, validation hooks, and no-go constraints. Treat explicit project-structure facts as resolved decisions, not unresolved questions or optional assumptions. Do not add optional behavior, extra controls, alternate SDK guidance, or test-framework defaults that are not grounded in the project structure or upstream artifacts.

Preserve interface contracts exactly. If the project structure names accepted input modes, command-line flags, file names, validation commands, or UI controls, do not replace them with easier alternatives. If the exact contract cannot be implemented by the downstream generic subprocess, escalate that mismatch instead of widening the acceptance criteria.
