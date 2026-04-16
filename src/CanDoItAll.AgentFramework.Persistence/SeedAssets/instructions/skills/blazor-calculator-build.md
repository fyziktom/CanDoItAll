Use this skill when building the sample calculator application.

1. Create or verify only the requested parent directory with `workspace_create_directory`. If the requested parent is the `app` folder, do not pre-create a nested `SimpleCalculatorApp` project directory because `workspace_dotnet_new` must create that folder itself.
2. Before any manual file edits, inspect whether the expected project already exists at `SimpleCalculatorApp\SimpleCalculatorApp.csproj` under the requested parent folder.
3. If the project does not exist yet, use `workspace_dotnet_new` with `template=blazor` and `name=SimpleCalculatorApp` to scaffold a .NET 10 Blazor Web App at the requested parent folder. Keep the parent directory pointed at the `app` folder, not at a deeper nested `SimpleCalculatorApp` folder.
4. Do not add an alternate output path and do not create a second app. The expected project file must be `SimpleCalculatorApp\SimpleCalculatorApp.csproj` under the requested parent folder.
5. If `workspace_dotnet_new` reports overwrite conflicts, inspect the existing `SimpleCalculatorApp` folder and repair that app in place. Do not rerun the scaffold into `SimpleCalculatorApp\SimpleCalculatorApp` or any other deeper nested path.
6. Immediately read `SimpleCalculatorApp.csproj`, `Program.cs`, and `Components/Pages/Home.razor` with `workspace_read_file` so you are editing the real scaffold instead of guessing.
7. Keep the app static SSR only: do not add any @rendermode directive, do not add interactive render modes, do not rely on @onclick handlers, do not change TargetFramework away from net10.0, and do not add Microsoft.AspNetCore.Components.Server. `@rendermode Static` is invalid here and `@rendermode InteractiveServer` breaks the requirement.
8. If the scaffold includes `AddInteractiveServerComponents` or `AddInteractiveServerRenderMode` in `Program.cs`, remove them so the app stays static SSR only.
9. Do not use `@model`, page models, `HomeModel`, or `AddAdditionalPageModelBinder`.
10. Do not replace Program.cs, App.razor, or SimpleCalculatorApp.csproj with legacy Blazor Server or WebAssembly templates. Preserve the modern scaffold shape from the attached resources and copy the attached net10-home-page-example closely instead of inventing alternate Razor patterns.
11. Implement the calculator in `Components/Pages/Home.razor` using a normal GET form, query-string-backed inputs, and explicit `[SupplyParameterFromQuery(Name = "left")]`, `[SupplyParameterFromQuery(Name = "right")]`, and `[SupplyParameterFromQuery(Name = "operation")]` properties. Do not use plain `[Parameter]`, `HttpContext.Request`, `Context.Request.Query`, `Request.Query`, `NavigationManager`, or alternate manual query parsing for those values.
12. Keep the main route at `/`.
13. Keep the implementation minimal and close to the example. Do not add injected services, alternate query helpers, extra redesign sections, or reads from artifacts/baseline unless the user explicitly asks for comparison work.
14. Remove scaffold leftovers that violate the showcase scope, including unused navigation entries and template pages such as `Counter` and `Weather`.
15. The page must include an explicit clear or reset path that returns the user to an empty calculator state without stale query-string results.
16. Include the exact text `Division by zero is not allowed.` and ensure the form keeps `method="get"` and the select element keeps `name="operation"`.
17. Use `decimal` for the calculator values and handle divide-by-zero predictably in the rendered UI instead of throwing, returning `NaN`, or relying on hidden framework behavior.
18. Before any repair pass on an existing app, run `workspace_dotnet_build` once. A successful build is necessary but not sufficient for completion; continue until the requested acceptance behavior is present.
19. Add targeted validation for the calculator behavior. Prefer lightweight automated tests when the scaffold allows it cleanly; otherwise capture a concise manual validation note with explicit operations covered.
20. If `workspace_dotnet_build` fails, read the file named in the error and fix that file in place instead of downgrading frameworks or creating a second app.
21. Stop only after the app builds successfully and the calculator meets the route, scope, clear/reset, divide-by-zero, and validation requirements. Do not run `dotnet publish`.
