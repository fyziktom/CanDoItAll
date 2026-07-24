# C# Current-State Inventory

## Evidence Source

CodeAnalytics MCP was unavailable after targeted tool discovery. Evidence is from exact source reads, `rg`, project files, compiler/test output, and independent read-only architecture reviews.

## Responsibility Map

| Source | Members/responsibility | Dependencies | Target owner | Test seam | Risk |
| --- | --- | --- | --- | --- | --- |
| `ProjectStructurePage.Processes.cs` | summary traversal, visual-target selection, text redaction, output-root resolution and aliases | `ProjectStructureSurface`, `ProjectStructureNode`, graph conventions, context node filter | launch-context builder | construct surface/nodes directly | high |
| `ProjectStructureProcessNodeService.cs` | near-verbatim duplicate of the same algorithms | same plus service orchestration | launch-context builder | construct surface/nodes directly | high |
| `ProjectStructurePage.ProjectHierarchy.cs` | self/duplicate/cycle/current-parent candidate rules | hierarchy link summaries | hierarchy selection policy | pass ids and link list directly | medium |
| remaining page parts | mutable Blazor state, lifecycle, dialogs, service orchestration | many UI/application dependencies | unchanged | existing component tests | protected |

## Size And Construction

- one routable Razor component plus 22 explicit partial-class files;
- 11,137 aggregate source lines before extraction;
- page component is activated by the Blazor router; tests render it through bUnit;
- the selected pure algorithms need no constructor dependency;
- direct unit tests can instantiate only records and call the extracted types.

## Existing/Missing Tests

- Existing: broad component regressions and one integration process-context characterization path.
- Missing: direct summary/output-root tests, hierarchy graph-policy tests, and an anti-duplication architecture checkpoint.

## Canonicality Warning

`ProjectStructureSurface` is a mixed read projection. The extraction may read it but may not mutate persistence or claim it as canonical project truth.
