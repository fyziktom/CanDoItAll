# Create solution and Blazor SSR app

Create the solution file and Blazor SSR application project with the agreed names and add the app project to the solution.

Use only the product root recorded in the scaffold contract. If that root is an `external-target/...` alias, keep all product files under that alias. If no external root was grounded, use the current-run managed output root from the dispatcher prompt and never switch to a guessed local folder.

For a new .NET solution, use the bounded `workspace_dotnet_new` tool with template `sln` for the solution at the product root. After it succeeds, inspect both `<ProductRoot>/<SolutionName>.slnx` and `<ProductRoot>/<SolutionName>.sln`; current SDKs may create either file.

Create the app parent directory under the product root, normally `<ProductRoot>/src`, with `workspace_create_directory` when it does not exist. Then use `workspace_dotnet_new` with template `blazor`, `parentDirectory` set to that `src` directory, and `name` set to the app project name from the scaffold contract. Do not set the app `parentDirectory` to the product root after the solution has been created.

Do not pass `force` unless a current-run diagnostic proves an empty target can be safely overwritten. If adding the app project to the solution requires `dotnet sln add`, use a small reviewed script under the current-run artifact root, run it with `workspace_pwsh_run_script`, then inspect the resulting solution/project paths.
