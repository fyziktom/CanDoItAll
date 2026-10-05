# Workbench Insights sandbox

This host references Insights.UI and its small reporting value contracts. It has no
Workbench, Projects, AgentFramework, provider, persistence or application host reference.
The displayed records are controlled scenarios, not evidence of native persistence.

From the repository root:

```powershell
npm ci --prefix Tailwind
npm run insights:css:build
dotnet run --project src/Sandboxes/CanDoItAll.Workbench.Insights.UiSandbox -p:InsightsAssetMode=Fast
```

Use `npm run tailwind:build` and `-p:InsightsAssetMode=Parity` for the application
theme. The default is Parity. Both modes have separate output/intermediate paths;
missing assets or a mismatched requested mode fail explicitly. Publishing uses the
same mode property and copies the selected stylesheet into the standalone output.

The selection/support controls are also the production renderers, including the
detail child, TreeView and real floating windows. Each support instance has its own
origin, selected IDs, placements and menu acknowledgement. Exercise no/single/multi
selection, advanced attachment/Workflow display, markers, borders, Health, menu
replacement and a delayed menu. Native task/file/runtime actions produce explicit
controlled feedback here; their owners are exercised in the application.

The report controls are the production renderers. Scenario controls affect the left
source; the right view has independent state. Delayed reads intentionally complete
after cancellation when released, so replacement and late-result behavior can be
observed. Activity has all four kinds, filtered aggregates and three pages of rows.
The host does not impersonate native Process cursors, file authority or project writes.

Development `/_dev/runtime` reports the process, watch iteration and hot-reload
generation. These diagnostics are excluded from the production endpoint map.
WB2 visual validation is restricted to 1920×1080 at device scale 1.
