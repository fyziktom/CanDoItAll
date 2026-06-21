# Current State

The running `5032` instance was stopped before edits. PID `25712` was the listening `CanDoItAll.Web` process and was terminated with `Stop-Process`.

The reference map workbook is prepared at `bundle://inventories/unused-module-reference-map.xlsx`, with a rendered preview at `bundle://inventories/unused-module-reference-map-preview.png`. The workbook contains direct references, proposed removal actions, and generic term matches kept separate from module-removal targets.

Direct module references are concentrated in:

- `repo://CanDoItAll.slnx`
- `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Composition/ModuleAssemblies.cs`
- `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `repo://src/CanDoItAll.Web/Program.cs`
- `repo://src/CanDoItAll.Web/Composition/ShellNavigation.cs`
- `repo://src/CanDoItAll.Web/Components/Layout`
- `repo://src/CanDoItAll.Web/Components/Pages/Home.razor`
- `repo://src/CanDoItAll.Modules.Workbench`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner`
- `repo://tests`
- `repo://tools/CanDoItAll.ScenarioSeeder/CanDoItAll.ScenarioSeeder.csproj`

The old Activity module implements the concrete `IActivityStream`, but infrastructure already registers `NullActivityStream` before module registration. Removing the Activity module therefore leaves explicit no-op activity publishing instead of breaking unrelated services that opportunistically emit activity events.

The SchedulerPlanner module currently depends on Automation types and the Automation trigger registry. That dependency must be replaced before the Automation project can be removed without breaking scheduler behavior.

Workbench has old Validation connections in project structure projection, node scope resolution, right-click creation actions, quick actions, and command routing. Those are the highest risk hidden references because they sit outside the Validation module project itself.
