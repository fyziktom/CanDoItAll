# SB06: Harden script execution beyond regex scanning.

## Objective

Harden script execution beyond regex scanning.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Introduce a governed script side-effect manifest format for `workspace_pwsh_run_script` and `workspace_python_run_file`.
- Require manifest for governed process script execution when step does not allow product mutation.
- Block encoded/nested/child scripts unless declared and inspected.
- Add post-execution diff/path audit for product target roots in non-mutating steps.
- Add red-team tests for PowerShell `[IO.File]::WriteAllText`, redirection, `cmd /c`, encoded command, and Python `Path.open('w')`.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
