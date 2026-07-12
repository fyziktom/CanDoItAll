# Repair solution setup findings

Repair only the setup findings recorded by `validate-first-build`.

## Scope

- Fix scaffold, project file, package/reference, template-integrity, and test-discovery issues needed for first build proof.
- If validation reported an empty solution, missing solution membership, or a disconnected test project, mutate the product target to add the missing solution entries and project references. Existing project files alone are not a repair.
- For solution membership and project-reference repair, use the supplied deterministic helper instead of inventing a new helper. Write `DotNetAddTestProjectScript` verbatim to `DotNetAddTestProjectScriptRef`, verify that `.ps1` ref, invoke it with `workspace_pwsh_run_script` and `DotNetAddTestProjectSideEffectManifest`, then read back the solution and test project files.
- When validation reported missing solution membership or a missing project reference, write a reviewed PowerShell helper script under the current-run artifact root and run it with `workspace_pwsh_run_script` using a `sideEffectManifest` with `version` set to `1`, `mode` set to `ProductMutation`, native absolute solution/project paths in `declaredReadPaths` and `declaredWritePaths`, and `allowShellDelegation` set to `true` when the helper invokes `dotnet`.
- In the `workspace_pwsh_run_script` tool call, use the reviewed current-run `.ps1` helper ref for `path`, for example `artifacts/process-runs/{CurrentProcessRunId}/scripts/repair-solution-setup.wire-solution.ps1`; never use the primary step markdown artifact under `steps/*.md` as the script path. Use current-run artifact refs for `outputPaths` and the grounded `external-target/...` alias for `workingDirectory`. Do not pass a native absolute path as a structured workspace tool `path` or `workingDirectory`.
- The helper must choose the existing contracted `.slnx` or `.sln`, run the missing `dotnet sln <solution-file> add <project-file>` commands, run the missing `dotnet add <test-project-file> reference <app-project-file>` command when needed, and verify the final `dotnet sln <solution-file> list` output.
- The helper must verify solution membership with product-root-relative project paths, not native absolute paths only. Compute relative paths with `[System.IO.Path]::GetRelativePath($ProductRoot, <project-file>)`, normalize both `\` and `/` separators in expected paths and readback text, and accept normalized relative-path matches from `dotnet sln list` or solution file readback. When verifying `ProjectReference`, compute the app path relative to the test project directory with `[System.IO.Path]::GetRelativePath((Split-Path -Parent $TestProjectFile), $AppProjectFile)`, normalize that computed value and the project file readback, and do not hardcode an escaped relative string such as `..\\..\\src\\...` into the check.
- Completion without the successful `workspace_pwsh_run_script` receipt is invalid when the validation finding requires solution membership or project-reference mutation.
- Keep changes minimal and tied to the validation failure packet.
- Do not implement feature behavior, replace starter UI beyond template repair, launch runtime, or capture browser proof.

## Output

Before reporting `Completed`, read back the repaired solution file and affected project files. If validation reported missing solution membership, the solution readback must show the repaired app/test project entries. If validation reported a missing project reference, the test project readback must show the repaired `ProjectReference`. If the required mutation tool is unavailable, report `Blocked` with the missing tool/capability and exact paths that still need repair.

Write the setup repair change set to `artifacts/process-runs/<current-process-run-id>/steps/repair-solution-setup.md` and include changed files, root cause, exact repair actions, readback evidence, commands to rerun, and remaining setup risks.
