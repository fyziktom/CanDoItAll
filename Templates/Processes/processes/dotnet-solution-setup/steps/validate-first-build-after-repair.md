# Revalidate first build after setup repair

Rerun restore/build and the smallest targeted test command or discovery command after `repair-solution-setup`.

Use the available `workspace_dotnet_restore` and `workspace_dotnet_build` tools against the contracted solution or product root before finalizing. Use `workspace_dotnet_test` against `DotNetTestProjectFileAlias` or `DotNetTestProjectFile` when present, preferably with `noBuild=true` after a successful solution build; use the contracted solution or product root for tests only when no test project target exists. The `ProductCompletionRequiredToolReceipts` launch variable is a hard validation gate for this step; successful current-run receipts for restore, build, and test are required before `Completed`. Before command execution, inspect the repaired solution file and affected project files. A solution with no app/test project entries is still failed repair evidence even if `dotnet build` exits successfully as a no-op. If repaired proof still fails, write the command or readback evidence and return `Completed` with branch outcome `setup-repair-escalation`; that branch is the subprocess no-go path. Return `Blocked` for missing execution capability only after a current tool boundary or denied tool receipt proves the required validation tool is unavailable.

## Branching

- Return `Completed` with branch outcome `setup-validated` only when repaired restore, build, and targeted test discovery or initial test command are green enough for parent implementation.
- Return `Completed` with branch outcome `setup-repair-escalation` when repaired proof still fails, solution membership/project reference readback is still missing, repair evidence is detached from the original failure, or another repair would exceed setup scope.
- Return `Blocked` only when an environment, permission, missing tool, or process-contract issue prevents recheck execution.

In the managed artifact, include the exact selected branch key on its own line as `Branch outcome key: setup-validated` or `Branch outcome key: setup-repair-escalation` before finalizing.

## Output

Write repaired first-build evidence to `artifacts/process-runs/<current-process-run-id>/steps/validate-first-build-after-repair.md` and include rerun commands, exit codes, relevant output, before/after assessment, and unresolved warnings.
