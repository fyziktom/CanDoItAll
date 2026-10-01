# Agent Editor Core A1 sandbox

## Purpose

Independent scenario host for the genuine Agent core renderer and shared controls at
1920×1080. It composes only Editor.UI and BaseLib registration. It requires no product
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
The six deferred sections, provider refresh and avatar generation are explicitly labeled
production integrations; the sandbox never claims to execute them. No image, audio, model,
file or other external effect is performed.

## Validation

See the [A1 boundary record](../../../docs/architecture/agent-editor-core-ui-a1.md) for graph,
published assets, large-desktop browser and measured development-loop evidence. Run the
[light tests](../../../tests/Components/CanDoItAll.AgentFramework.Editor.UI.Tests/README.md)
and the `AgentEditorSandboxBrowserTests` selection using current build-backed discovery.
