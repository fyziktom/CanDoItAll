# Add test project and reference

If `DotNetAddTestProjectExecutionPlan` is present, execute that plan in order before writing the primary step artifact: write `DotNetAddTestProjectScript` verbatim to `DotNetAddTestProjectScriptRef`, verify that `.ps1` ref, run `workspace_pwsh_run_script` with `DotNetAddTestProjectSideEffectManifest`, then read back the solution file and test project file. The generated helper creates the missing test project with the typed test template before it wires solution membership and the project reference. Do not write `Status: InProgress`, progress notes, placeholders, or a `Completed` artifact before the script receipt exists.

Create the test project using the test framework grounded by the parent contract or existing repository convention, add it to the solution, and add the required project reference to keep test work available from the first slice.

Do not hardcode xUnit, MSTest, or NUnit in this generic process. Use the named test framework when the current run provides one; otherwise use the existing repository convention and record the assumption. Escalate when a test project cannot safely reference the app project, such as UI-only projects that require tests to target a domain/application library not yet present.

If the test project is missing, the generated helper must create it with `dotnet new` using the current run's typed `DotNetTestTemplate`, `DotNetTestProjectName`, `DotNetTestProjectDirectory`, and target framework values. Then add it to the solution and add the required project reference. If the project already exists, verify the project file, solution membership, and reference instead of recreating it.

The `ProductCompletionRequiredPaths` launch variable is a hard completion gate for this step. Every listed path must exist before you submit `Completed`; for normal setup that means the solution file, app project file, and test project file must be present. If the app project path is missing, recover the missing app scaffold in place when the current step still has `MutateProductTarget`, then create and connect the test project. Block only for a concrete tool, permission, policy, or environment boundary.

The `ProductCompletionRequiredToolReceipts` launch variable is also a hard completion gate. For this step, a successful `workspace_pwsh_run_script` receipt is required after the helper creates or verifies the test project. Do not satisfy this by describing the script in the managed artifact. Invoke the tool in this step and cite the receipt.

The `ProductCompletionRequiredFileContentChecks` launch variable is a hard readback gate. For this step, the solution file must contain the contracted app and test project membership records, and the test project file must contain the required `ProjectReference`, before you submit `Completed`.

Adding the test project to the same solution and adding the project reference are part of this step.

Mandatory test wiring script:

- Write a small reviewed PowerShell helper script under the current-run artifact root, for example `artifacts/process-runs/{CurrentProcessRunId}/scripts/add-test-project.wire-solution.ps1`.
- Run that helper with `workspace_pwsh_run_script` before reporting `Completed`.
- Pass a `sideEffectManifest` object or JSON with `version` set to `1`, `mode` set to `ProductMutation`, native absolute solution/project paths in `declaredReadPaths` and `declaredWritePaths`, and `allowShellDelegation` set to `true` when the helper invokes `dotnet`.
- Inside the helper, use native absolute `ProductRoot` and `DotNet*` launch-variable paths. Do not place `external-target/...` aliases in PowerShell content.
- In the `workspace_pwsh_run_script` tool call, use the reviewed current-run `.ps1` helper ref for `path`, for example `artifacts/process-runs/{CurrentProcessRunId}/scripts/add-test-project.wire-solution.ps1`; never use the primary step markdown artifact under `steps/*.md` as the script path. Use current-run artifact refs for `outputPaths` and the grounded `external-target/...` alias for `workingDirectory`. Do not pass a native absolute path as a structured workspace tool `path` or `workingDirectory`.
- If the helper accepts product paths as script arguments, those argument values are consumed by PowerShell and must be native absolute `ProductRoot` or `DotNet*` paths, not `external-target/...` aliases.
- The helper must choose the existing contracted `.slnx` or `.sln`, create the missing test project with the typed test template before wiring, add the app project to the solution first when its membership is missing, execute `dotnet sln <solution-file> add <test-project-file>`, execute `dotnet add <test-project-file> reference <app-project-file>`, then execute `dotnet sln <solution-file> list`.
- The helper must compute product-root-relative app/test project paths with `[System.IO.Path]::GetRelativePath($ProductRoot, <project-file>)`, compute the test-project-relative reference path with `[System.IO.Path]::GetRelativePath((Split-Path -Parent $TestProjectFile), $AppProjectFile)`, normalize both `\` and `/` separators in those paths and the relevant readback text, and accept matches on the normalized relative paths. Do not compare only native absolute project paths because `dotnet sln list` and SDK project references normally emit relative paths.
- The helper must not fail only because the app project is absent from the solution before repair. It must run the idempotent membership repair first, then fail if the solution file is missing, either project file is missing, the solution membership command fails, the project-reference command fails, normalized `dotnet sln list` output does not show both contracted projects, or the normalized test project readback does not contain the expected `ProjectReference`.

Completion without the successful `workspace_pwsh_run_script` receipt is invalid, even when the app and test project files exist.

Before reporting `Completed`, read back the solution file and the test project file. The solution file must contain entries for both the contracted app project and the contracted test project, and the test project file must contain the required `ProjectReference` when an app reference is appropriate. If any of those records are missing, repair them in this step. If no available tool can add the solution membership or project reference, report `Blocked` with the missing tool/capability and exact paths still needing mutation. Do not report `Completed` for an empty solution or a disconnected test project.

This step creates and connects the test project only. Do not implement feature-specific tests, run `dotnet build`, `dotnet test`, `dotnet run`, launch a browser, or capture runtime proof here. Build/test discovery belongs to the validation step, and feature-specific tests belong to the feature implementation slice.

The primary managed artifact is a final outcome artifact, not a progress checkpoint. Do not write `Status: InProgress`, progress notes, placeholders, or partial work to `artifacts/process-runs/{run}/steps/add-test-project.md`. Write that primary artifact only after the test project has been created or verified, solution membership has been created or verified, the project reference has been created or verified or a concrete unsafe-reference reason has been recorded, and representative readbacks have been captured.

The required change-set artifact must list the test project file, solution membership evidence, project reference evidence when applicable, representative file readbacks, and a short statement that build/runtime validation was intentionally deferred. Its Status line must be one of `Completed`, `Blocked`, `Failed`, `WaitingApproval`, or `Refused`.
