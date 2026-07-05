# Create solution and .NET app project

Create the solution file and requested .NET application project with the agreed names, then ensure the solution contains the app project. This step owns solution-to-app membership; the downstream `add-test-project` step owns test-project creation and `ProjectReference` wiring with its deterministic current-run helper.

Mandatory order for this step:
1. Create or verify the product root and `src` directory.
2. Create or verify the solution scaffold with `workspace_dotnet_new`.
3. Create or verify the contracted app project scaffold with `workspace_dotnet_new`.
4. Write `DotNetCreateProjectScript` verbatim to `DotNetCreateProjectScriptRef`.
5. Verify the `.ps1` ref with `workspace_stat_path` or `workspace_read_file`.
6. Run the same `.ps1` ref with `workspace_pwsh_run_script` and `DotNetCreateProjectSideEffectManifest`.
7. Read back the solution and app project files.
8. Only then write `steps/create-dotnet-project.md` and submit `Completed`.

Do not write the primary `steps/create-dotnet-project.md` artifact, a progress artifact, or a `Completed` outcome before the `workspace_pwsh_run_script` receipt exists. If a retry diagnostic says `workspace_pwsh_run_script` is missing, do not rerun `workspace_dotnet_new` or use `force=true`; write or verify the helper script, run it, read back the solution membership, then rewrite the primary artifact.

Use only the product root recorded in the scaffold contract. If that root is an `external-target/...` alias, keep all product files under that alias. Create the grounded greenfield product root when the scaffold contract says it does not exist yet. Never switch to a guessed local folder or run artifact folder.

If `workspace_list_files`, `workspace_stat_path`, or another read probe returns `Workspace path '<ProductRootAlias>' does not exist` for the grounded product root alias, treat that as expected greenfield missing-directory state, not as unavailable access and not as a blocker. Immediately call `workspace_create_directory` for the exact product root alias, then call `workspace_create_directory` for `<ProductRootAlias>/src` before running `workspace_dotnet_new`. Do not write `Status: InProgress`, progress notes, placeholders, or a blocked step artifact before attempting the required directory creation. Return `Blocked` only if `workspace_create_directory` itself is denied or fails on a concrete policy, permission, or environment boundary.

The scaffold contract overrides generic scaffold shortcuts. Do not derive the app project name from the product-root folder leaf and do not create the app directly at the product root unless the contract explicitly says that is the layout.

Do not cite project-media file paths as source document context unless they are present in the current launch variables, current prompt context, inherited upstream artifacts, or a current-run tool receipt. Ignore source document paths from unrelated projects or prior runs.

Write the primary scaffold artifact using only grounded external-target aliases, managed process refs, project-structure node ids, and current-run tool receipt refs. Do not write native absolute paths such as `C:\...`, scoped storage paths under `artifacts/scopes/...`, tool-run stdout/stderr paths, managed-files paths, project-media file paths, or SourceDocLink values in the artifact body, reason, summary, next actions, or evidenceRefs. If a workspace tool returns a scoped storage path for a receipt or stdout/stderr file, do not read it and do not cite it; cite the tool receipt ref or summarize the tool result from the receipt returned by the tool.

This step creates the solution and app project, then wires the app project into the solution. Do not create the test project, implement requested feature behavior, edit generated starter UI/content, run `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet run`, launch a browser, or capture runtime proof here. Those concerns belong to the separate test-project, validation, implementation, or QA steps.

For a new .NET solution, use the bounded `workspace_dotnet_new` tool with template `sln` for the solution at the product root. The tool call must use `parentDirectory` set to the grounded product root alias, such as `WorkspaceAlias` / `<ProductRootAlias>`, and `name` set to `<SolutionName>`. Do not use the product root parent folder as the solution scaffold parent. After the tool succeeds, inspect both `<ProductRoot>/<SolutionName>.slnx` and `<ProductRoot>/<SolutionName>.sln`; current SDKs may create either file.

Greenfield setup requires two scaffold actions before this step can complete: create or verify the solution file, then create or verify the contracted app project with `workspace_dotnet_new` using the scaffold-contract app template. A missing `<ProductRoot>/src/<AppProjectName>/<AppProjectName>.csproj` after the solution scaffold is not a blocker and not a reason to write `Blocked`; it is the instruction to run the app-project scaffold under `<ProductRoot>/src` with `parentDirectory` set to the grounded `src` alias and `name` set to the contracted app project name. Return `Blocked` only if the app-project scaffold tool itself is denied or fails on a concrete policy, permission, template, SDK, or environment boundary.

The create-step tool receipt gate is template-specific. A solution-only `workspace_dotnet_new` receipt is not enough. If the runtime diagnostic says a required receipt such as `template=blazorwasm`, `template=blazor`, `template=webapi`, `template=worker`, `template=console`, or `template=classlib` is missing, run `workspace_dotnet_new` for the contracted app project template before writing the primary artifact again. Do not satisfy a missing app-template receipt by creating only `<ProductRoot>/src/<AppProjectName>` or by writing a summary that the app project is still missing.

When the app project directory already exists but is empty or missing `<AppProjectName>.csproj`, still run `workspace_dotnet_new` with `parentDirectory` set to `<ProductRootAlias>/src`, `name` set to `<AppProjectName>`, `template` set to the contracted app template, and `force` omitted or false. Do not switch `parentDirectory` to `<ProductRootAlias>/src/<AppProjectName>` unless the tool contract explicitly requires an output directory rather than parent/name arguments.

If a prior attempt already left `<ProductRoot>/<SolutionName>.slnx` or `<ProductRoot>/<SolutionName>.sln`, do not rerun the solution scaffold command. Read the existing solution file, verify whether it is empty or already contains project entries, and continue by creating the missing app project under the contracted app parent directory. Record the partial-solution recovery explicitly in the change-set artifact. Block only when the existing solution file is corrupt, contradictory with the scaffold contract, or cannot be read.

Create the app parent directory under the product root, normally `<ProductRoot>/src`, with `workspace_create_directory` when it does not exist. Then use `workspace_dotnet_new` with the template selected by the scaffold contract, `parentDirectory` set to that `src` directory, and `name` set to the app project name from the scaffold contract. Do not default to a UI template, and do not set the app `parentDirectory` to the product root after the solution has been created.

The `ProductCompletionRequiredPaths` launch variable is a hard completion gate for this step. Every listed path must exist before you submit `Completed`. For this setup step, the solution file and app project file paths listed there must be created or verified in this step; neither is deferred to the test-project step.

The `ProductCompletionRequiredToolReceipts` launch variable is also a hard completion gate. For this step, the required receipts normally include template-specific `workspace_dotnet_new` receipts. Do not report `Completed` until every listed receipt is present for the current run.

When `ProductCompletionRequiredToolReceipts` lists template-specific strings, each string must be present in a successful current-run tool receipt. For example, `template=sln` proves the solution scaffold command ran, and `template=blazorwasm` proves the Blazor WebAssembly app scaffold command ran. A generic `workspace_dotnet_new` receipt for only one of those commands does not satisfy both.

The `ProductCompletionRequiredFileContentChecks` launch variable is a hard readback gate when it lists checks for this step key. In the current split setup contract, `create-dotnet-project` must receive a solution-membership check for the app project path. If the solution file does not contain the app project after scaffolding, run the deterministic helper from `DotNetCreateProjectScript` before writing the primary artifact.

Do not treat existing solution/app files as sufficient proof. If the app project already exists, inspect the template-critical starter files before completing this step. For Blazor WebAssembly apps, verify `_Imports.razor`, `Program.cs`, `App.razor`, `wwwroot/index.html`, manifest, and service-worker files still match the current template shape closely enough to build. Repair stale or hand-authored scaffold drift in place before validation; common required wiring includes `@using Microsoft.AspNetCore.Components.Routing` in `_Imports.razor`, a resolvable `App` root component in `Program.cs`, the normal `<Router AppAssembly="@typeof(Program).Assembly">` / `<Found Context="routeData">` route block in `App.razor`, and the template-generated PWA assets. If `Router`, `Found`, `FocusOnNavigate`, `NotFound`, or `routeData` cannot resolve, repair `_Imports.razor` or the copied `App.razor` scaffold in this step before handing off to validation. Keep feature behavior and starter UI changes deferred.

Do not pass `force=true` to `workspace_dotnet_new` in this governed setup step. If a prior attempt left a partial solution or app scaffold, inspect the existing files and repair precise scaffold drift with focused product-mutation tools instead of regenerating the scaffold. If solution app membership is missing, write `DotNetCreateProjectScript` verbatim to `DotNetCreateProjectScriptRef`, verify the `.ps1` ref, run it with `workspace_pwsh_run_script` and `DotNetCreateProjectSideEffectManifest`, then read back the solution file.

If `DotNetCreateProjectExecutionPlan` is present, execute that plan in order before writing the primary step artifact: create or verify the solution and app scaffold with the required `workspace_dotnet_new` tool calls, write and run the deterministic membership helper, then read back the solution file and app project file. Do not write `Status: InProgress`, progress notes, placeholders, or a `Completed` artifact before the required template receipts, helper receipt, required paths, and file-content checks pass.

Before reporting `Completed`, read back the solution file and the app project file. The solution file must contain the contracted app project entry. If it does not, run the deterministic helper and retry the readback before finalizing.

The required change-set artifact must list created solution/app files, the selected template, representative file readbacks, current app solution membership evidence, and a short statement that test creation, test-project wiring, and validation were intentionally deferred.
