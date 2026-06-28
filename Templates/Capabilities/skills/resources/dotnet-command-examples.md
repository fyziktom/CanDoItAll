Choose the scaffold template that matches the requested deliverable:
- `blazor` for current Blazor Web App / Blazor SSR user-facing apps
- `webapi` for HTTP APIs
- `web` for minimal ASP.NET Core web apps
- `mvc` or `razor` only when the requirement asks for those UI models
- `worker` for background services
- `console` for command-line apps
- `classlib` for reusable libraries
- `mstest`, `xunit`, or `nunit` for test projects, using one runner per test project

Scaffold the selected app in the parent folder:
- tool: workspace_dotnet_new
- template: <selected-current-sdk-template>
- name: <AppName>
- parentDirectory: <scenario-parent-folder-or-mapped-external-target-parent>
- if the requested product root is `C:\work\apps\<AppName>`, use `parentDirectory: external-target/C/work/apps` and `name: <AppName>`
- do not rerun over a target that already contains a .NET project; inspect and repair the scaffold instead
- after this command, treat `<scenario-parent-folder-or-mapped-external-target-parent>/<AppName>` as `<product-root>` and target the generated project at `<product-root>/<AppName>.csproj`; do not create `<scenario-parent-folder>/<AppName>.csproj`, `<scenario-parent-folder>/Program.cs`, or unrelated sibling source beside it
- do not delete scaffold core files such as `.csproj`, `Program.cs`, generated app framework files, route/layout files, `appsettings*.json`, or static assets to make re-scaffolding succeed
- do not use recursive delete on a host project, test project, target root, or any directory containing a .NET project or solution file just to make scaffolding succeed
- after a stock scaffold exists, recovery should edit concrete source/project files such as a route/page/controller/command handler, `Domain/<Feature>Service.cs`, the test project `.csproj`, and test source before writing artifacts or rerunning validation

Keep support projects under the grounded product root:
- for small apps, prefer source folders in the host such as `<product-root>/Domain/<Feature>Service.cs`
- when a separate class library is justified, create a child parent first such as `<product-root>/src`
- tool: workspace_dotnet_new
- template: classlib
- name: `<AppName>.Domain`
- parentDirectory: `<product-root>/src`
- if the requested product root is `C:\work\apps\<AppName>`, use `parentDirectory: external-target/C/work/apps/<AppName>/src` and `name: <AppName>.Domain`
- do not use `parentDirectory: external-target/C/work/apps` and `name: <AppName>.Domain`; that creates a sibling beside the grounded product root
- if a policy denial names the product parent as ungrounded, switch to `<product-root>` or `<product-root>/src` before retrying

Build the generated app or library:
- tool: workspace_dotnet_build
- targetPath: `<AppName>.csproj`
- workingDirectory: `<product-root>`
- configuration: Debug

Run a browser-facing app or HTTP API after build:
- tool: workspace_dotnet_run
- targetPath: `<AppName>.csproj`
- workingDirectory: `<product-root>`
- waitForHttp: true
- noBuild: true
- startupTimeoutSeconds: 45
- keepAlive: false unless the same step immediately needs browser tools; if true, stop the app by calling `workspace_dotnet_stop` with the recorded `startup.json` receipt before finalizing
- use the returned URL, process id, stdout log, stderr log, and receipt paths for Playwright/browser proof

Run a console or worker-style app when runtime smoke is required:
- tool: workspace_dotnet_run
- targetPath: `<AppName>.csproj`
- workingDirectory: `<product-root>`
- waitForHttp: false
- noBuild: true

Create tests inside the grounded product root but outside production source folders:
- tool: workspace_dotnet_new
- template: xunit
- name: `<AppName>.Tests`
- parentDirectory: `<product-root>/tests`
- if the requested product root is `C:\work\apps\<AppName>`, use `parentDirectory: external-target/C/work/apps/<AppName>/tests` and `name: <AppName>.Tests`
- do not reuse the app scaffold parent directory for tests; `parentDirectory: external-target/C/work/apps` with `name: <AppName>.Tests` creates an ungrounded sibling and must be rejected before calling the tool
- use a sibling test project beside `<product-root>` only when the project structure explicitly grounds that sibling path

Do not create test files under:
- path: `<product-root>/<AppName>.Tests`

Remove stale nested tests before rebuilding the app:
- tool: workspace_delete_path
- path: `<product-root>/<AppName>.Tests`
- recursive: true
- allowed only when the `*.Tests` folder is misplaced directly inside the host project source directory; do not use this pattern for valid test projects under `<product-root>/tests` or for target roots

Clean duplicate scaffolded test sources before rerunning tests:
- tool: workspace_delete_path
- path: `<product-root>/tests/<AppName>.Tests/UnitTest1.cs`
- recursive: false
- tool: workspace_delete_path
- path: `<product-root>/tests/<AppName>.Tests/<AppName>.Tests.cs`
- recursive: false

Avoid root namespace/type collisions:
- prefer type name: `<Feature>Service`
- prefer namespace: `<AppName>.Domain`
- avoid type name: `<AppName>`
- for Blazor, avoid component file: `Components/<AppName>.razor`
- for Blazor, prefer component file: `Components/Pages/Home.razor` or `Components/Pages/<Feature>Page.razor`

Keep business logic testable:
- prefer source file: `Domain/<Feature>Service.cs`
- prefer tests targeting: `<AppName>.Domain.<Feature>Service`
- avoid tests targeting: `new <AppName>()`, a generated host type, or a UI component as a domain service
- required test project reference: compute a relative `ProjectReference` from the test `.csproj` to the real host or support-library `.csproj`; for `<product-root>/tests/<AppName>.Tests` to `<product-root>/<AppName>.csproj`, use `..\..\<AppName>.csproj`
- if tests fail with CS0118 "namespace but is used like a type": create/read `Domain/<Feature>Service.cs`, add the ProjectReference, update tests to instantiate `<Feature>Service`, then rerun build and test
- `workspace_dotnet_test` targetPath must be `<AppName>.Tests.csproj` or a solution file, never a source file or a plain directory

For Blazor Web App route locations:
- prefer route file: `Components/Pages/Home.razor`
- avoid legacy route files: `Pages/Home.razor` and `Pages/Index.razor`
- if duplicate `@page "/"` exists: delete or move the stale root `Pages/*.razor` route before launch validation
- after `dotnet new blazor`: inspect the generated `.csproj`, `Program.cs`, and `Components/Pages` route files before writing UI

For Blazor Web App hosting:
- avoid files: `Pages/_Host.cshtml` and `Startup.cs`
- avoid startup code: `UseStartup<Startup>()`
- avoid script: `blazor.server.js`
- avoid package references in a current Blazor Web App to old `Microsoft.AspNetCore.Components*` packages
- if inherited notes say Blazor Server, Blazor Server-Side, or Razor Pages while the scaffold/project structure says Blazor SSR or Blazor Web App: treat those notes as stale shorthand and keep the Blazor Web App shape
- if build mentions `Pages/_Host.cshtml`, `Startup.cs`, `typeof(App)`, or `UseStartup<Startup>()`: delete legacy host files and stale root routes, remove obsolete package references, restore generated minimal `Program.cs`, keep `Components/App.razor` and `Components/Routes.razor`, then rebuild

Avoid no-progress validation loops:
- after a failed `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run`, do not repeat the identical command until you inspect diagnostics or change files that directly address the failure
- after `workspace_dotnet_test` is denied because the test project path is missing, create or repair the test project under the grounded product root and add the required `ProjectReference` before rerunning
- after `workspace_dotnet_test` is denied because the target was a source file or directory, rerun it against the test `.csproj` after fixing project references and stale test sources
- do not rewrite the same source file with identical content in a loop

Keep layout references buildable:
- prefer: edit `MainLayout.razor` content/styles in place
- avoid: renaming `MainLayout.razor` unless `Routes.razor`, `NotFound.razor`, and `_Imports.razor` references are updated together

Validate both app and tests after test edits:
- tool: workspace_dotnet_build
- targetPath: `<AppName>.csproj`
- workingDirectory: `<product-root>`
- configuration: Debug
- tool: workspace_dotnet_test
- targetPath: `<AppName>.Tests.csproj`
- workingDirectory: `<product-root>/tests/<AppName>.Tests`
- configuration: Debug
