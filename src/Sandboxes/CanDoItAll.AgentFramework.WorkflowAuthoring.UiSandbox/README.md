# Workflow authoring scenario host

Run from the repository root:

```powershell
dotnet run --project src/Sandboxes/CanDoItAll.AgentFramework.WorkflowAuthoring.UiSandbox -c Debug --no-launch-profile -- --urls http://127.0.0.1:63359
```

Set `ASPNETCORE_ENVIRONMENT=Development` for source static assets. Parity is the
default asset mode and links the generated production theme as content, without a
Web project reference. Generate it using `npm run tailwind:build` when missing.
The host references Authoring.UI and Prompts.UI; its evaluated source closure has
19 projects and no native Workflow owners, modules, persistence or Web assembly.

For the narrower asset scan, run `npm run workflow-authoring:css:build`, then use
`-p:WorkflowAuthoringAssetMode=Fast`. Parity and Fast have separate build output
directories. Fast scans only the authoring host, authoring UI, retained Workflow
shell, Prompt, Configuration and Conversations presentation. Both modes load the
real neutral Canvas/Overlay assets. Fast does not substitute for Parity acceptance.

Ready, held, rejected, unknown, unavailable, large-graph and long-input fixtures use
the actual production canvas and children. The second workspace has independent
document/window/viewport identities. Release completes only held fixture operations;
replacement retires the first fixture. Templates, run/event dialogs and the actual
Gallery search presentation are available from the toolbar. These are deterministic
presentation fixtures, not evidence of native execution or file authorization.

`Fixtures/image-executor.json` is the Web JSON serialization of the current native
`BuiltInWorkflowExecutorDescriptors.ImageGeneration`, including all eight schema
fields. It is input metadata, not a copied runtime implementation. Regenerate it
from that descriptor when the native schema changes, then compare the native and
independent renderer behavior.

Use 1920×1080 at scale 1. Source/publish, watch measurements and native consumer
results are recorded in [WF1 evidence](../../../docs/architecture/workflow-authoring-ui-wf1.md).
