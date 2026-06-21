# Create solution and .NET app project

Create the solution file and requested .NET application project with the agreed names and add the app project to the solution.

Use only the product root recorded in the scaffold contract. If that root is an `external-target/...` alias, keep all product files under that alias. Create the grounded greenfield product root when the scaffold contract says it does not exist yet. Never switch to a guessed local folder or run artifact folder.

The scaffold contract overrides generic scaffold shortcuts. Do not derive the app project name from the product-root folder leaf and do not create the app directly at the product root unless the contract explicitly says that is the layout.

This step creates the solution and app project only. Do not create the test project, implement requested feature behavior, edit generated starter UI/content, run `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet run`, launch a browser, or capture runtime proof here. Those concerns belong to the separate test-project, validation, implementation, or QA steps.

For a new .NET solution, use the bounded `workspace_dotnet_new` tool with template `sln` for the solution at the product root. After it succeeds, inspect both `<ProductRoot>/<SolutionName>.slnx` and `<ProductRoot>/<SolutionName>.sln`; current SDKs may create either file.

Create the app parent directory under the product root, normally `<ProductRoot>/src`, with `workspace_create_directory` when it does not exist. Then use `workspace_dotnet_new` with the template selected by the scaffold contract, `parentDirectory` set to that `src` directory, and `name` set to the app project name from the scaffold contract. Do not default to a UI template, and do not set the app `parentDirectory` to the product root after the solution has been created.

Do not treat existing solution/app files as sufficient proof. If the app project already exists, inspect the template-critical starter files before completing this step. For Blazor WebAssembly apps, verify `_Imports.razor`, `Program.cs`, `App.razor`, `wwwroot/index.html`, manifest, and service-worker files still match the current template shape closely enough to build. Repair stale or hand-authored scaffold drift in place before validation; common required wiring includes `@using Microsoft.AspNetCore.Components.Routing` in `_Imports.razor`, a resolvable `App` root component in `Program.cs`, the normal `<Router AppAssembly="@typeof(Program).Assembly">` / `<Found Context="routeData">` route block in `App.razor`, and the template-generated PWA assets. If `Router`, `Found`, `FocusOnNavigate`, `NotFound`, or `routeData` cannot resolve, repair `_Imports.razor` or the copied `App.razor` scaffold in this step before handing off to validation. Keep feature behavior and starter UI changes deferred.

Do not pass `force` unless a current-run diagnostic proves an empty target can be safely overwritten. If adding the app project to the solution requires `dotnet sln add`, use a small reviewed script under the current-run artifact root, run it with `workspace_pwsh_run_script`, then inspect the resulting solution/project paths.

The required change-set artifact must list created solution/app files, the selected template, solution membership evidence, representative file readbacks, and a short statement that test creation and validation were intentionally deferred.
