# CanDoItAll.AgentFramework.Editor.UI

## Purpose

Agent Editor Core A1 renders the shared form, ten-section navigation, loading and failure
states, mutation feedback, footer, and the Identity, Runtime, Images and Voice sections.
It receives one host-owned `AgentEditorModel` and `EditContext`; it never rebuilds a
partial whole-agent request.

## Project Type

Razor class library targeting `net10.0`. Build with:

```powershell
dotnet build src/UI/CanDoItAll.AgentFramework.Editor.UI/CanDoItAll.AgentFramework.Editor.UI.csproj
```

## Dependencies

The [project file](CanDoItAll.AgentFramework.Editor.UI.csproj) references the existing
abstractions-only Agent models, neutral Conversations widgets and Components BaseLib.
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

Memory, Project Structure Access, Workspace Tools, Secrets, Process Access and Capabilities
are real production slots under the same form. Avatar generation and source-managed provider
refresh are also host integrations. They are not extracted or implemented by this leaf.
The relocated section identifiers retain their namespace, names and numeric values; the
module forwards the old assembly types for compatibility.

## Validation

The [independent sandbox](../../Sandboxes/CanDoItAll.AgentFramework.Editor.UiSandbox/README.md)
uses this renderer without product DI or a database. The light
[component tests](../../../tests/Components/CanDoItAll.AgentFramework.Editor.UI.Tests/README.md)
cover rendering, origin and graph contracts. Native host, persistence and real browser
consumer proof are described in the [A1 boundary record](../../../docs/architecture/agent-editor-core-ui-a1.md).
