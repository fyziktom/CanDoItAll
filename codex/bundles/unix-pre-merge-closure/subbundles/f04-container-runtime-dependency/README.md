# Linux container process-bootstrap contract

## Goal

Make the setsid dependency explicit and prove it in the shipped application image.

## Entry

Read the root execution prompt, findings, requirements, invariants and
validation strategy. Reconfirm the exact repository anchor before editing.

## Tasks

1. Prefer adding util-linux explicitly to the runtime image unless a native bootstrap is implemented within this bounded task.
2. Extend Docker validation to require the declared dependency or its native replacement.
3. Build the final app image in package mode.
4. Run command -v setsid inside the runtime image.
5. Run one disposable app+PostgreSQL health smoke and teardown.
6. Do not add desktop capability to the LinuxHeadless profile.

## Rules

- Preserve unrelated changes.
- Use focused failing-first tests.
- Keep source comments in English.
- Do not push or merge.
- Do not weaken a validator to make evidence pass.
