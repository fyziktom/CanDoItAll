# Revalidate first build after setup repair

Rerun restore/build and the smallest targeted test command or discovery command after `repair-solution-setup`.

Use the available `workspace_dotnet_restore` and `workspace_dotnet_build` tools against the contracted solution or product root before finalizing. Use `workspace_dotnet_test` against `DotNetTestProjectFileAlias` or `DotNetTestProjectFile` when present, preferably with `noBuild=true` after a successful solution build; use the contracted solution or product root for tests only when no test project target exists. The branch-scoped `ProductCompletionRequiredToolReceipts` policy is a hard validation gate: `setup-validated` requires successful current-run restore, build, test, and solution-readback receipts; `setup-repair-escalation` requires current-run attempts for those tools and accepts failed execution receipts as unresolved-defect evidence. Before command execution, inspect the repaired solution file and affected project files. A solution with no app/test project entries is still failed repair evidence even if `dotnet build` exits successfully as a no-op. If repaired proof still fails, write the command or readback evidence and return `Completed` with branch outcome `setup-repair-escalation`; that branch is the subprocess no-go path. A policy-denied or unavailable-tool receipt is not defect evidence and must remain `Blocked`.

Revalidate only the original setup defect and the pre-implementation baseline. Generated starter/demo UI or content and a passing placeholder template test are expected until downstream feature implementation. They are not unresolved setup defects and must not select `setup-repair-escalation`. Do not evaluate product acceptance criteria, feature completeness, substantive product-test coverage, or the absence of product-specific source in this step. If the original setup defect is repaired and topology/readbacks plus restore/build/test-runner discovery are green, select `setup-validated`.

## Branching

- Return `Completed` with branch outcome `setup-validated` when repaired restore, build, targeted test discovery or the initial test command, solution membership, and project-reference readbacks are green. This includes a generated starter UI and passing placeholder template tests.
- Return `Completed` with branch outcome `setup-repair-escalation` only when the original setup proof still fails, solution membership/project-reference readback is still missing, repair evidence is detached from the original structural or command failure, or another setup-only repair would exceed scope. Missing feature behavior or substantive product tests are never setup defects.
- Return `Blocked` only when an environment, permission, missing tool, or process-contract issue prevents recheck execution.

In the managed artifact, include the exact selected branch key on its own line as `Branch outcome key: setup-validated` or `Branch outcome key: setup-repair-escalation` before finalizing.

## Output

Write repaired first-build evidence to `artifacts/process-runs/<current-process-run-id>/steps/validate-first-build-after-repair.md` and include rerun commands, exit codes, relevant output, before/after assessment, and unresolved warnings.
