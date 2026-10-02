# Complete Agent editor sandbox

## Purpose

Independent scenario host for all ten real Agent editor sections and shared controls at
1920×1080. It composes Editor.UI, the existing capability list, StorageSelection, and
BaseLib registration. It requires no product
database, provider credentials, runtime owner or API connection.

## Run

```powershell
dotnet watch --project src/Sandboxes/CanDoItAll.AgentFramework.Editor.UiSandbox
```

The [project file](CanDoItAll.AgentFramework.Editor.UiSandbox.csproj) links the generated
product theme and fails explicitly if it is absent. Build the existing Tailwind output
before use. BaseLib fonts/styles and renderer scoped CSS are served as static web assets.
Normal `dotnet publish` creates a standalone host; run its DLL from the publish directory.

## Scenarios

Representative and large catalogs, loading and load failure, partial provider references,
unknown effort, an unavailable shared model, held save, known refusal, committed warning,
failed read-back and unconfirmed result use the same core form. The host reports synthetic
writes/reads and captured names. Retry refresh performs a synthetic read only. Clear/reopen
replaces the editor lifetime. Two editors have independent state and pending work.

Scenario effort options are fixed fixture metadata, not a second implementation of model
compatibility policy. Production policy and request mapping are tested through their owners.
The actual Memory bindings, external-root entry, Storage picker and three small confirmations
are composed here. Missing references, memory eligibility/failure, project/secret read failure,
held verification, failed proof read-back and unknown publication have explicit scenarios.
Committed fixture snapshots are copied independently and read back; the live form retains
its context and unadded entry candidates. Native root resolution, definition creation,
provider refresh and avatar generation are explicitly simulated or retained production
integrations. No image, audio, model, file or other external effect is performed.

## Validation

See the [A2 boundary record](../../../docs/architecture/agent-editor-completion-a2.md) for graph,
published assets, large-desktop browser and measured development-loop evidence. Run the
[light tests](../../../tests/Components/CanDoItAll.AgentFramework.Editor.UI.Tests/README.md)
and the `AgentEditorSandboxBrowserTests` selection using current build-backed discovery.
