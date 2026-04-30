# Scope Inventory

## Projects

| Project | Expected change |
| --- | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\CanDoItAll.Components.Charts.csproj` | New Razor Class Library wrapper project. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\CanDoItAll.Components.Sandbox.csproj` | Reference new charts project. |
| `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj` | Reference charts project for contract tests if tests are added there. |
| `C:\repositories\CanDoItAll\CanDoItAll.slnx` | Include new charts project. |

## Expected Files

| File | Expected change |
| --- | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\Components\CdaChart.razor` | New main wrapper. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\Components\ChartsHeadAssets.razor` | New host asset component. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\Models\*.cs` | New public chart models/enums/options helpers. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\Infrastructure\ServiceCollectionExtensions.cs` | New DI registration. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\App.razor` | Include chart assets. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Program.cs` | Register chart services. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\_Imports.razor` | Import chart namespace. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs` | Add Charts group/examples. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Charts.razor` | New sandbox page. |
| `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ChartsWrapperTests.cs` | Targeted tests if feasible. |

## Commands

- `dotnet build src/CanDoItAll.Components.Charts/CanDoItAll.Components.Charts.csproj`
- `dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter Charts`

## Browser Proof Surface

- Route: `http://localhost:<port>/groups/charts`
- Desktop viewport: approximately `1600x900` or larger.
- Mobile viewport: approximately `390x844`.
- Assertions: route loads, headings visible, Apex chart containers generated, at least one nonblank SVG per major case, no console errors related to Apex asset loading.
