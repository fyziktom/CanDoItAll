# CanDoItAll.AgentFramework.Editor.UI

## Purpose

The complete technical Agent editor renders the shared form, all ten sections, loading
and failure states, mutation feedback, footer and small confirmations.
It receives one host-owned `AgentEditorModel` and `EditContext`; it never rebuilds a
partial whole-agent request.

## Project Type

Razor class library targeting `net10.0`. Build with:

```powershell
dotnet build src/UI/CanDoItAll.AgentFramework.Editor.UI/CanDoItAll.AgentFramework.Editor.UI.csproj
```

## Dependencies

The [project file](CanDoItAll.AgentFramework.Editor.UI.csproj) references the existing
abstractions-only Agent models, neutral Conversations widgets, RecordBrowsing and Components BaseLib.
It has no module implementation, Core, EF, Voice runtime, Canvas, provider registry or
service injection. Provider and thinking-effort presentations contain safe display values.
Thinking-effort policy remains in its original owner; the compatible MAF component wraps
the neutral renderer. Existing consumers need no upward dependency on this library.

## Architecture Notes

`AgentEditorCoreState` carries presentation and typed callbacks captured by the owner.
`AgentEditorOrigin` identifies the form lifetime. Each rendered section captures the
current presentation when its retained tab fragment executes; callbacks keep that origin.
The host owns load/cancel, read-back, admission, confirmations, whole-agent capture,
optimistic versions, commits and all authority. Direct bindings update only that canonical
draft. Identity opts into the shared input controls' immediate mode, including Enter before
blur. Shared conversation consumers retain their default change-on-blur behavior.

`AgentEditorAccessState` supplies typed intents and shared draft-only permission policy for
Memory, Project Structure Access, Workspace Tools, Secrets, Process Access and Capabilities.
`AgentMemorySection` and `AgentMemoryBindingList` render the actual memory controls;
`AgentExternalRootsField` renders root entry and selected references. The production wrappers
still own memory eligibility and native path/binding resolution. Small delete, auto-approval
and workspace-risk renderers return explicit decisions to the original dialog owner.

Narrow composition slots contain the actual capability list and StorageSelection picker.
Avatar generation, source-managed provider refresh and capability-definition authoring
remain host integrations. Saved-agent assignments save the whole draft; new-agent assignments
are staged. Verify is independent of Save and uses native attributable proof receipts.
The relocated section identifiers retain their namespace, names and numeric values; the
module forwards the old assembly types for compatibility.

## Validation

The [independent sandbox](../../Sandboxes/CanDoItAll.AgentFramework.Editor.UiSandbox/README.md)
uses this renderer without product DI or a database. The light
[component tests](../../../tests/Components/CanDoItAll.AgentFramework.Editor.UI.Tests/README.md)
cover rendering, origin and graph contracts. Native host, persistence and real browser
consumer proof are described in the [A2 boundary record](../../../docs/architecture/agent-editor-completion-a2.md).
