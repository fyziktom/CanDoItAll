# Scope Inventory

| Surface | Current reference | Required action | Notes |
| --- | --- | --- | --- |
| Solution | `repo://CanDoItAll.slnx` references `src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj` | Completed | Build graph input. |
| Facade project | `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj` | Completed | Primary requested rename. |
| Web app project | `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj` | Completed | Direct consumer. |
| Component tests project | `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` | Completed | Direct test consumer. |
| Web app imports | `repo://src/CanDoItAll.Web` exact facade imports | Completed | Package imports such as `.BaseLib` remain. |
| Component test imports | `repo://tests/CanDoItAll.Tests.Components` exact facade imports | Completed | Sandbox and package imports remain. |
| Local docs | README and shared-component docs that named old facade | Completed | Sibling-repo package docs remain intact. |
| Sibling repo pointers | `repo://CanDoItAll.Mcp.Components.settings.json` and `C:\repositories\CanDoItAll.Components` docs | No change | Explicitly out of scope. |
