# Structure renderer tests

This isolated suite references only Structure.UI and its neutral component closure.
It renders the real canvas, toolbar, toolbox and structural dialogs without native
Workbench, Projects, persistence or runtime services. It covers independent receivers,
same-render snapshots, retired callbacks, busy/unknown outcomes and explicit retry.

Native admission, metadata and graph behavior remain in CanDoItAll.Tests.Components.
The suite is registered in Components, Stable and all component CI selections.

Run `dotnet test tests/Components/CanDoItAll.Workbench.Structure.UI.Tests` from the
repository root. See the maintained WB3 architecture record for exact proof inputs.
