# Create solution and .NET app project

Create the solution file and requested .NET application project with the agreed names and add the app project to the solution.

Use only the product root recorded in the scaffold contract. If that root is an `external-target/...` alias, keep all product files under that alias. Create the grounded greenfield product root when the scaffold contract says it does not exist yet. Never switch to a guessed local folder or run artifact folder.

The scaffold contract overrides generic scaffold shortcuts. Do not derive the app project name from the product-root folder leaf and do not create the app directly at the product root unless the contract explicitly says that is the layout.

This step creates the solution and app project only. Do not create the test project, implement requested feature behavior, edit generated starter UI/content, run `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet run`, launch a browser, or capture runtime proof here. Those concerns belong to the separate test-project, validation, implementation, or QA steps.

For a new .NET solution, use the bounded `workspace_dotnet_new` tool with template `sln` for the solution at the product root. After it succeeds, inspect both `<ProductRoot>/<SolutionName>.slnx` and `<ProductRoot>/<SolutionName>.sln`; current SDKs may create either file.

Create the app parent directory under the product root, normally `<ProductRoot>/src`, with `workspace_create_directory` when it does not exist. Then use `workspace_dotnet_new` with the template selected by the scaffold contract, `parentDirectory` set to that `src` directory, and `name` set to the app project name from the scaffold contract. Do not default to a UI template, and do not set the app `parentDirectory` to the product root after the solution has been created.

Do not pass `force` unless a current-run diagnostic proves an empty target can be safely overwritten. If adding the app project to the solution requires `dotnet sln add`, use a small reviewed script under the current-run artifact root, run it with `workspace_pwsh_run_script`, then inspect the resulting solution/project paths.

The required change-set artifact must list created solution/app files, the selected template, solution membership evidence, representative file readbacks, and a short statement that test creation and validation were intentionally deferred.
