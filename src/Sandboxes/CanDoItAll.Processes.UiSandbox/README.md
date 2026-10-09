# Processes UI sandbox

This host renders the production Processes and Live Processes views without the native
Processes module, database, provider credentials, runtime workers, or application composition.
It uses the existing neutral process projections and real Canvas, Charts, Mermaid,
FileBrowser, FileInteraction, conversation, activity, and overlay components.

The scenario session owns local sample state. Save, import, launch, cancellation, file
actions, and conversation actions here demonstrate presentation and intent handling;
they are not evidence of production persistence or operating-system execution.

## Run

From the repository root, with the configured Components and FileTools siblings:

```powershell
npm ci --prefix Tailwind
npm run tailwind:build
dotnet watch --project src/Sandboxes/CanDoItAll.Processes.UiSandbox --launch-profile "Processes sandbox"
```

The Parity profile listens on `http://localhost:5407`. It links the generated production
theme as content, without a Web project reference. Generate that theme after changing
Tailwind utilities. Scoped CSS and shared JavaScript retain their normal asset pipeline.

For the smaller feature-specific Tailwind input:

```powershell
npm run processes:css:build
dotnet watch --project src/Sandboxes/CanDoItAll.Processes.UiSandbox --property:ProcessesAssetMode=Fast --launch-profile "Processes sandbox Fast"
```

Fast listens on `http://localhost:5408`. In a separate terminal,
`npm run processes:css:watch` watches its Tailwind input. Fast includes the feature and
its shared renderers; it does not promise CSS coverage for unrelated application modules.
Both modes include fonts, Canvas, Charts, Mermaid, and FileTools assets. Mode-specific
output directories prevent one host's static asset manifest from replacing the other.
The host rejects an unsupported or mismatched requested asset mode.

## Scenarios and validation

- `/workspace`: definition, roles, steps/canvas, runs, graphs, analytics, exchange, and manager chat.
- `/live`: activity, agents, graphs, and tool history with details and cancellation presentation.
- `/two`: two independent workspace openings, each with its own drafts and conversation state.
- The toolbar changes scope, injects a refresh failure, opens authorized sample files,
  and selects conversation loading, unavailable, execution, approval, attachment, voice,
  failure, and adversarial-content states.

The development-only `/_dev/runtime` endpoint reports process identity, watch iteration,
asset mode, and hot-reload generation. It is absent from a Production publish.

Build the changed production renderer before its tests. See [Testing](../../../docs/testing.md)
for the focused selections, discovery requirement, owned database rule, browser lanes,
and mandatory portability gate. The [boundary record](../../../docs/architecture/processes-ui-boundary.md)
separates structural proof from native effect and development-loop evidence.
