# SB01 Source Assertions

- Invariant ID: SB01-PROJECT-IDENTITY
  - `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj` exists and declares `AssemblyName` plus `RootNamespace` as `CanDoItAll.AppComponents`.
  - `repo://CanDoItAll.slnx` references `src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`.

- Invariant ID: SB01-CONSUMER-REFERENCES
  - `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj` references `..\CanDoItAll.AppComponents\CanDoItAll.AppComponents.csproj`.
  - `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` references `..\..\src\CanDoItAll.AppComponents\CanDoItAll.AppComponents.csproj`.
  - Direct app-shell imports in the web app and AppShell component tests use `CanDoItAll.AppComponents`.

- Invariant ID: SB01-SIBLING-BOUNDARY
  - `CanDoItAll.Components.*` package references remain in the app facade, web app, and component test project.
  - `repo://CanDoItAll.Mcp.Components.settings.json` remains a sibling-repo pointer and was not edited.
