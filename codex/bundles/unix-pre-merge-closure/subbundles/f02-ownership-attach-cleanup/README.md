# Transactional process ownership attachment

## Goal

Ensure every failure after Process.Start leaves no process or native ownership resource.

## Entry

Read the root execution prompt, findings, requirements, invariants and
validation strategy. Reconfirm the exact repository anchor before editing.

## Tasks

1. Restructure StartSessionAsync so process and partial ownership state are visible to one total cleanup path.
2. Add an explicit abort operation or equivalent for ownership-start objects.
3. On Windows attachment failure, close the Job Object and terminate the unassigned root process.
4. On Unix attachment/resume failure, terminate the established process group when observable, otherwise terminate the root tree.
5. Use non-cancellable bounded cleanup.
6. Do not return an identity until boundary and executable identity are established.
7. Add deterministic Windows/Unix-friendly tests using an injected attach failure.

## Rules

- Preserve unrelated changes.
- Use focused failing-first tests.
- Keep source comments in English.
- Do not push or merge.
- Do not weaken a validator to make evidence pass.
