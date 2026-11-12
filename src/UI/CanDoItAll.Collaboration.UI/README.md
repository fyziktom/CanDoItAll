# Collaboration rendering library

`CollaborationWorkspaceSurface` renders the complete Inbox, Threads and Escalations
workspace through `ICollaborationWorkspaceView`. Its real BaseLib children include
`PageScaffold`, `PageHeader`, `CompactStat`, `Tabs`, `ListDetailShell`, `SelectionListItem`,
`SectionCard` and the actual Blazor edit forms. It injects no application owner.

The host owns selection, drafts, `EditContext`, reads, writes and navigation. Event handlers
capture the rendered draft and target instances. Create validation remains mounted while
its section is hidden so validation survives tab changes. Each saving form disables its
own fieldset. Pure presentation helpers map the typed section to the existing tab indices.

Dependencies: Collaboration contracts, BaseLib/Common and the Blazor framework. No EF,
Infrastructure, application composition, scheduler or provider host belongs in this graph.
The feature has no scoped CSS or JavaScript; production Tailwind scans `src`, including
this library, while BaseLib supplies its own stylesheet and browser assets.

```powershell
dotnet build src/UI/CanDoItAll.Collaboration.UI/CanDoItAll.Collaboration.UI.csproj --configuration Release /m:1
```

Use the [sandbox](../../Sandboxes/CanDoItAll.Collaboration.UiSandbox/README.md) for the
development loop and the [boundary record](../../../docs/architecture/collaboration-ui-boundary.md)
for ownership and validation.
