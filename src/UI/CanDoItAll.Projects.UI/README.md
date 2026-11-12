# Projects portfolio UI

The Projects P1 renderer contains the portfolio board, cards and tree, read-only hierarchy,
overview, complete five-step editor, package controls and deletion-result presentation.
The routed Projects host composes these components in production; the
[scenario host](../../Sandboxes/CanDoItAll.Projects.UiSandbox/README.md) uses the same renderers.

## Boundary

The leaf references BaseLib, Projects.Contracts, the pure Editing kernel and Editing.UI.
Source-mode BaseLib adds Common; Projects.Contracts adds SharedKernel. The reviewed Editing
references provide typed comparison state and the shared field renderer. They add no owner
services or storage. The leaf has no reference to Infrastructure, EF,
Workspace, concrete Projects services, Workbench or AgentFramework. The shared namespace
preserves existing component consumers. The modal injects IJSRuntime for native form
validity/focus and its typed logger for active validation and cleanup diagnostics;
all product reads, writes and navigation are typed callbacks.

`ProjectsBoard.FilesContent` receives the same bounded `ProjectFileFilterProjection` used
by Cards. The production host supplies the existing `ProjectFilesPortfolioPane` and
`ProjectFilesDialog`. Their coordinators, access checks and leases remain in the Projects
implementation. Their rendering now lives in the separate
[Projects.Files.UI leaf](../CanDoItAll.Projects.Files.UI/README.md). Package targets are safe ID/label projections;
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

An acquired draft retains its actual immutable original definition. A rejected stale definition
also retains its original context and frozen submission. Explicit comparison reads and current
verification before Apply belong to the native host; selecting results does not change the live
model. The comparison has a separate EditContext and uses typed scalar/text fields, keyed phase
and option rows, and explicit phase order. Options retain their native category/id order.

Apply updates the same model and EditContext, advances the baseline and native expectation to
the verified current definition, and leaves Save as a separate operation. Later input, stale
comparisons, unknown native operations and unfinished dates block apply. Removed saved child
rows kept explicitly become new draft rows; retired IDs are not reused. Undo restores local
values while retaining the verified current authority basis, and is available before further
input or Save. Starter effects and retained receipts keep their existing owners. Reload remains
an explicit replacement action. A restored submission does not invent an original snapshot.

Choices carry across a refreshed comparison only when their original, local and current values
still match. Earlier custom result entries remain available for copying. The renderer cannot
fetch, save or adopt a new native token on its own.

The Project leave guard captures the actual draft, native operation state and comparison
revision before a decision. Continue retains the form; explicit discard permits leaving.
Later input or an accepted result invalidates an older decision. Unfinished dates, keyed
rows and blank starter plans count as local changes. Retained native operations keep their
original targets and recovery state. The native host supplies the same-editor route policy
and checks replacement intents before changing URL state; wizard and overview transitions
within the same acquired editor keep its context.

## Validation

```powershell
dotnet build src/UI/CanDoItAll.Projects.UI/CanDoItAll.Projects.UI.csproj --configuration Release /m:1
dotnet test tests/Components/CanDoItAll.Projects.UI.Tests/CanDoItAll.Projects.UI.Tests.csproj --configuration Release --list-tests /m:1
dotnet test tests/Components/CanDoItAll.Projects.UI.Tests/CanDoItAll.Projects.UI.Tests.csproj --configuration Release --no-build --no-restore
```

The [P1 record](../../../docs/architecture/projects-portfolio-ui-p1.md) separates renderer,
native owner, production browser, development-loop and qualified S0 evidence. Desktop
validation uses 1920×1080. Follow [Testing](../../../docs/testing.md) for current discovery,
owned PostgreSQL, portability-static and integration requirements.
