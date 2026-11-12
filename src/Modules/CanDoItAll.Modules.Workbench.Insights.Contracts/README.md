# Workbench Insights contracts

Pure report presentation values shared by Workbench's native query owners and the
independent Insights renderer. This project preserves the original
`CanDoItAll.Modules.Workbench` namespaces; the original Workbench assembly forwards
the moved public types.

The assembly contains accepted report options, schedules, cost totals, activity
identity and display values. Native scope admission, coordinated queries, Process
continuation cursors, persistence and runtime services stay in Workbench. No
project, storage, file-content or execution authority crosses this boundary.

See [the WB2 architecture and proof record](../../../docs/architecture/workbench-insights-wb2.md)
and [UI component seams](../../../docs/architecture/ui-component-seams.md).
