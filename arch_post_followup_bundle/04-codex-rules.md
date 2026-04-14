# Codex rules

## Core execution rule

Execute the numbered subbundles in order. After every architecture review gate, if the answer to any gate question is weaker than an explicit **yes**, create and finish the corrective subbundle first, rerun the gate, and only then continue.

## Mandatory behavior

- Do not borrow trust from the prior closure claim.
- Do not widen scope beyond the active subbundle unless a stop rule forces corrective work.
- Prefer schema-backed invariants over application-only assumptions.
- Prefer targeted seams over broad rewrites.
- Keep the current improved canonical dependency model intact; do not reintroduce legacy dependency mirrors into core types.
- Keep the durable Process outbox intact; do not collapse it back into direct post-commit side effects.
- When doing query cohesion work, extend the extracted query seams instead of collapsing them back into `ProcessesService`.

## Review-gate rule

At Gate A, Gate B, and Gate C:

1. produce a written memo from live evidence;
2. answer every gate question explicitly;
3. fail the gate if any answer is not a clear yes;
4. create and finish the matching corrective subbundle before downstream work resumes.

## Closure rule

Do not close this bundle until:

- all numbered subbundles are complete or honestly skipped with justification;
- all gates pass from live evidence;
- fresh proof artifacts exist for the reopened scope;
- the final execution report matches the actual repository state.
