# Revalidate first build after setup repair

Rerun restore/build and the smallest targeted test command or discovery command after `repair-solution-setup`.

## Branching

- Return `Completed` with branch outcome `setup-validated` only when repaired restore, build, and targeted test discovery or initial test command are green enough for parent implementation.
- Return `Completed` with branch outcome `setup-repair-escalation` when repaired proof still fails, repair evidence is detached from the original failure, or another repair would exceed setup scope.
- Return `Blocked` only when an environment, permission, missing tool, or process-contract issue prevents recheck execution.

## Output

Write repaired first-build evidence to `artifacts/process-runs/<current-process-run-id>/steps/validate-first-build-after-repair.md` and include rerun commands, exit codes, relevant output, before/after assessment, and unresolved warnings.
