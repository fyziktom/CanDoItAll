# Projects portfolio UI

The Projects P1 renderer contains the portfolio board, cards and tree, read-only hierarchy,
overview, complete five-step editor, package controls and deletion-result presentation.
The routed Projects host composes these components in production; the
[scenario host](../../Sandboxes/CanDoItAll.Projects.UiSandbox/README.md) uses the same renderers.

## Boundary

The leaf references BaseLib and Projects.Contracts. Source-mode BaseLib adds Common;
Projects.Contracts adds SharedKernel. It has no reference to Infrastructure, EF,
Workspace, concrete Projects services, Workbench or AgentFramework. The shared namespace
preserves existing component consumers. The only injected service is IJSRuntime for
native form validity and focus; all product reads, writes and navigation are typed callbacks.

`ProjectsBoard.FilesContent` receives the same bounded `ProjectFileFilterProjection` used
by Cards. The production host supplies the existing `ProjectFilesPortfolioPane` and
`ProjectFilesDialog`. Their coordinators, access checks and leases remain in the Projects
implementation. Files P2 is deferred. Package targets are safe ID/label projections;
profile acquisition and package transfer remain with their current owners.

## Editing

Each acquired `ProjectEditorDraft` owns one model, EditContext, row identities and mutation
state. All five steps remain mounted while hidden, preserving raw invalid dates and field
validation. Text input updates the model before blur. Both submit buttons validate the same
form and capture a detached `ProjectEditorSubmission` before asynchronous browser validation.

The host admits writes, receives the native owner's acknowledgement and reconciles actual
project/lifetime/child IDs against the submitted rows and field revisions. Later input,
removed rows and replacement rows survive. Starter rows belong to this draft; acknowledged
seeds are retired and unknown outcomes require inspection. The sandbox explicitly simulates
these effects and does not claim database or file-authority proof.

## Validation

```powershell
dotnet build src/UI/CanDoItAll.Projects.UI/CanDoItAll.Projects.UI.csproj --configuration ProjectsUiProof /m:1
dotnet test tests/Components/CanDoItAll.Projects.UI.Tests/CanDoItAll.Projects.UI.Tests.csproj --configuration ProjectsUiProof --list-tests /m:1
dotnet test tests/Components/CanDoItAll.Projects.UI.Tests/CanDoItAll.Projects.UI.Tests.csproj --configuration ProjectsUiProof --no-build --no-restore
```

The [P1 record](../../../docs/architecture/projects-portfolio-ui-p1.md) separates renderer,
native owner, production browser, development-loop and qualified S0 evidence. Desktop
validation uses 1920×1080. Follow [Testing](../../../docs/testing.md) for current discovery,
owned PostgreSQL, portability-static and integration requirements.
