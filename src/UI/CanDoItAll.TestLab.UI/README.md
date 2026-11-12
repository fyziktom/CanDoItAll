# TestLab rendering library

`TestLabWorkspaceSurface` renders the plan list, filters and Overview, Cases, Evidence and
Runs through `ITestLabWorkspaceView`. It uses the actual BaseLib scaffold, list/detail
layout, tabs, forms and actions and injects no backend service. The host owns the draft,
selection, reads, writes, receipts and disposal.

`TestLabDraft` owns one model and `EditContext`, validation and raw timestamp input.
`TestLabSubmission` is the pure submission/reconciliation policy: deep copies, original
row references, committed IDs and field comparisons. It performs no IO. Rendered intents
capture the draft and target version; project changes retire that target even for A/B/A.
Before-blur input updates the draft immediately. Invalid timestamp text survives tab
unmounting; valid offset timestamps become UTC without losing the instant. Persistence
retains its existing PostgreSQL timestamp precision.

Dependencies are TestLab.Contracts, Projects.Contracts/SharedKernel and BaseLib/Common
plus Blazor. The renderer has no owned CSS or JavaScript. Production Tailwind scans `src`,
including this RCL; the sandbox links the production stylesheet in Parity mode.

```powershell
dotnet build src/UI/CanDoItAll.TestLab.UI/CanDoItAll.TestLab.UI.csproj --configuration Release /m:1
```

Use the [sandbox](../../Sandboxes/CanDoItAll.TestLab.UiSandbox/README.md) and
[boundary record](../../../docs/architecture/testlab-ui-boundary.md).
