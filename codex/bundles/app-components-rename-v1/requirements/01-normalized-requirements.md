# Normalized Requirements

| ID | Requirement | Acceptance signal |
| --- | --- | --- |
| `REQ-001` | Rename the main-repo facade project from `CanDoItAll.Components` to `CanDoItAll.AppComponents`. | `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj` exists and uses `AssemblyName`/`RootNamespace` `CanDoItAll.AppComponents`. |
| `REQ-002` | Repair all direct project references and solution entries that point at the old facade path. | `repo://CanDoItAll.slnx`, web project, and component test project reference the renamed path. |
| `REQ-003` | Repair compiled C# and Razor consumers of the old facade namespace. | Direct exact imports and declarations use `CanDoItAll.AppComponents`; package namespace imports remain under `CanDoItAll.Components.*`. |
| `REQ-004` | Preserve sibling component-library references and do not edit the sibling repo. | `CanDoItAll.Components.*` package references and `CanDoItAll.Mcp.Components.settings.json` remain valid sibling-repo pointers. |
| `REQ-005` | Update local docs that identify the main-repo facade by the old project path/name. | Docs distinguish `CanDoItAll.AppComponents` from `CanDoItAll.Components.*` packages and the sibling repo. |
| `REQ-006` | Validate the rename with targeted build, tests, and stale-reference search. | Required transcripts exist under `bundle://proof/SB01/transcripts/` and completed bundle validation passes. |
