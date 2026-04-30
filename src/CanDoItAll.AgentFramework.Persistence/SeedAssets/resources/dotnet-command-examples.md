Scaffold the app in the parent folder:
- tool: workspace_dotnet_new
- template: blazor
- name: SampleBlazorApp
- parentDirectory: <scenario-parent-folder>
- do not rerun over a target that already contains a .NET project; inspect and repair the scaffold instead
- after this command, target the generated project at `<scenario-parent-folder>/SampleBlazorApp/SampleBlazorApp.csproj`; do not create `<scenario-parent-folder>/SampleBlazorApp.csproj`, `<scenario-parent-folder>/Program.cs`, or root `Pages/*.razor` beside it
- do not delete scaffold core files such as `.csproj`, `Program.cs`, `Components/App.razor`, `Components/Routes.razor`, `_Imports.razor`, `Components/Pages/Home.razor`, layout files, `appsettings*.json`, or `wwwroot/app.css` to make re-scaffolding succeed
- do not use recursive delete on a host project, sibling test project, target root, or any directory containing a .NET project or solution file just to make scaffolding succeed
- after a stock scaffold exists, recovery should edit source/project files such as Components/Pages/Home.razor, Domain/FeatureService.cs, the sibling test .csproj, and test source before writing artifacts or rerunning validation

Build the generated app:
- tool: workspace_dotnet_build
- targetPath: SampleBlazorApp.csproj
- workingDirectory: <scenario-parent-folder>\SampleBlazorApp
- configuration: Debug

Create tests beside the generated app, not inside it:
- tool: workspace_dotnet_new
- template: xunit
- name: SampleBlazorApp.Tests
- parentDirectory: <scenario-parent-folder>

Do not create test files under:
- path: <scenario-parent-folder>\SampleBlazorApp\SampleBlazorApp.Tests

Remove stale nested tests before rebuilding the app:
- tool: workspace_delete_path
- path: <scenario-parent-folder>\SampleBlazorApp\SampleBlazorApp.Tests
- recursive: true
- allowed only when the `*.Tests` folder is misplaced directly inside the host project directory; do not use this pattern for sibling test projects or target roots

Clean duplicate scaffolded test sources before rerunning tests:
- tool: workspace_delete_path
- path: <scenario-parent-folder>\SampleBlazorApp.Tests\UnitTest1.cs
- recursive: false
- tool: workspace_delete_path
- path: <scenario-parent-folder>\SampleBlazorApp.Tests\SampleBlazorApp.Tests.cs
- recursive: false

Avoid root namespace/type collisions in Blazor apps:
- prefer type name: FeatureService
- prefer namespace: SampleBlazorApp.Domain
- avoid type name: SampleBlazorApp
- avoid component file: Components/SampleBlazorApp.razor
- prefer component file: Components/Pages/Home.razor or Components/Pages/FeaturePage.razor

Keep business logic testable:
- prefer source file: Domain/FeatureService.cs
- prefer tests targeting: SampleBlazorApp.Domain.FeatureService
- avoid tests targeting: new SampleBlazorApp() or new SampleBlazorApp.Components.Pages.Home()
- required test project reference: <ProjectReference Include="..\SampleBlazorApp\SampleBlazorApp.csproj" />
- if tests fail with CS0118 "namespace but is used like a type": create/read Domain/FeatureService.cs, add the ProjectReference, update tests to instantiate FeatureService, then rerun build and test
- workspace_dotnet_test targetPath must be SampleBlazorApp.Tests.csproj or a solution file, never FeatureServiceTests.cs or a plain directory

Use Blazor Web App route locations:
- prefer route file: Components/Pages/Home.razor
- avoid legacy route files: Pages/Home.razor and Pages/Index.razor
- if duplicate @page "/" exists: delete or move the stale root Pages/*.razor route before launch validation
- after dotnet new blazor: inspect the generated .csproj, Program.cs, and Components/Pages route files before writing UI

Do not downgrade Blazor Web App hosting:
- avoid files: Pages/_Host.cshtml and Startup.cs
- avoid startup code: UseStartup<Startup>()
- avoid script: blazor.server.js
- avoid package references in net10 Blazor Web App: Microsoft.AspNetCore.Components.Web Version="7.0.0", Microsoft.AspNetCore.Components.WebAssembly Version="7.0.0", Microsoft.AspNetCore.Components Version="7.0.0"
- if inherited notes say Blazor Server, Blazor Server-Side, or Razor Pages while the scaffold/project structure says Blazor SSR or Blazor Web App: treat those notes as stale shorthand and keep the Blazor Web App shape
- if build mentions Pages/_Host.cshtml, Startup.cs, typeof(App), or UseStartup<Startup>(): delete Pages/_Host.cshtml, Startup.cs, legacy root Pages/*.cshtml files, and stale root Pages/*.razor routes, remove obsolete package references, restore generated minimal Program.cs, keep Components/App.razor and Components/Routes.razor, then rebuild

Avoid no-progress validation loops:
- after a failed workspace_dotnet_build, workspace_dotnet_test, or workspace_dotnet_run, do not repeat the identical command until you inspect diagnostics or change files that directly address the failure
- after workspace_dotnet_test is denied because the test project path is missing, create or repair the sibling test project and ProjectReference before rerunning
- after workspace_dotnet_test is denied because the target was a .cs file or directory, rerun it against the sibling test .csproj after fixing project references and stale test sources
- do not rewrite the same source file with identical content in a loop

Keep layout references buildable:
- prefer: edit MainLayout.razor content/styles in place
- avoid: renaming MainLayout.razor unless Routes.razor, NotFound.razor, and _Imports.razor references are updated together

Validate both projects after test edits:
- tool: workspace_dotnet_build
- targetPath: SampleBlazorApp.csproj
- workingDirectory: <scenario-parent-folder>\SampleBlazorApp
- configuration: Debug
- tool: workspace_dotnet_test
- targetPath: SampleBlazorApp.Tests.csproj
- workingDirectory: <scenario-parent-folder>\SampleBlazorApp.Tests
- configuration: Debug
