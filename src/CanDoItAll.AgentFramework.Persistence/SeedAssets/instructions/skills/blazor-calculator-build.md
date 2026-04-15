Use this skill when building the sample calculator application.

1. Create the parent directory with `workspace_create_directory`.
2. Before any manual file edits, use `workspace_dotnet_new` with `template=blazor` and `name=SimpleCalculatorApp` to scaffold a .NET 10 Blazor Web App at the requested parent folder.
3. Do not add an alternate output path. The expected project file must be `SimpleCalculatorApp\SimpleCalculatorApp.csproj` under the requested parent folder.
4. Immediately read `SimpleCalculatorApp.csproj`, `Program.cs`, and `Components/Pages/Home.razor` with `workspace_read_file` so you are editing the real scaffold instead of guessing.
5. Keep the app static SSR only: do not add any @rendermode directive, do not add interactive render modes, do not rely on @onclick handlers, do not change TargetFramework away from net10.0, and do not add Microsoft.AspNetCore.Components.Server. `@rendermode Static` is invalid here and `@rendermode InteractiveServer` breaks the requirement.
6. If the scaffold includes `AddInteractiveServerComponents` or `AddInteractiveServerRenderMode` in `Program.cs`, remove them so the app stays static SSR only.
7. Do not use `@model`, page models, `HomeModel`, or `AddAdditionalPageModelBinder`.
8. Do not replace Program.cs, App.razor, or SimpleCalculatorApp.csproj with legacy Blazor Server or WebAssembly templates. Preserve the modern scaffold shape from the attached resources and copy the attached net10-home-page-example closely instead of inventing alternate Razor patterns.
9. Implement the calculator in `Components/Pages/Home.razor` using a normal GET form, query-string-backed inputs, and explicit `[SupplyParameterFromQuery(Name = "left")]`, `[SupplyParameterFromQuery(Name = "right")]`, and `[SupplyParameterFromQuery(Name = "operation")]` properties. Do not use plain `[Parameter]`, `HttpContext.Request`, `Context.Request.Query`, `Request.Query`, `NavigationManager`, or alternate manual query parsing for those values.
10. Keep the main route at `/`.
11. Keep the implementation minimal and close to the example. Do not add injected services, alternate query helpers, extra redesign sections, or reads from artifacts/baseline unless the user explicitly asks for comparison work.
12. Before any repair pass on an existing app, run `workspace_dotnet_build` once. If the app already builds successfully, stop editing and report success immediately.
13. Include the exact text `Division by zero is not allowed.` and ensure the form keeps `method="get"` and the select element keeps `name="operation"`.
14. If `workspace_dotnet_build` fails, read the file named in the error and fix that file in place instead of downgrading frameworks or creating a second app.
15. After the first successful `workspace_dotnet_build`, stop editing and report success immediately. Do not run `dotnet publish`.
