
# Shared implementation prompt

Use this bundle to execute the stabilization work in **phase order**.

## Non-negotiable architectural rules

1. Do **not** convert node into a mere view shell.
2. Preserve stable node identity for workbench-authored thinking.
3. Preserve semantically meaningful X/Y and semantic markers canonically.
4. Do not keep duplicated actor truth in metadata and assignment rows.
5. Do not keep persisted system-managed graph rows as authoritative read truth.
6. Do not use destructive delete/recreate as the default note→typed-node transition path.

## Required execution behavior

- Implement only the requested phase unless explicitly told to continue.
- Run the validation plan after code changes.
- Re-run the skill lenses after each phase.
- Capture failing evidence before patching.
- Prefer small, high-leverage stabilizations over broad rewrites.

## Required final checks

- invariant tests
- projection equivalence tests
- cross-module actor assignment tests
- lifecycle transition tests
- QA pass using `shared-prompts/qa-prompt.md`
