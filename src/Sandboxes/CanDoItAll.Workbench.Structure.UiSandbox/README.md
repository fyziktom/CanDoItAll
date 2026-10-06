# Structure authoring sandbox

This backend-free host references only Structure.UI and its neutral component
dependencies. Two independent workspaces render the actual canvas, toolbox,
composer and structural dialogs. Controls cover loading, unavailable/retry, error,
empty, nested and wide scenes, delayed results and nested project-editor return.
Receipts are scenario observations, not native database evidence.

From the repository root:

```powershell
dotnet run --project src/Sandboxes/CanDoItAll.Workbench.Structure.UiSandbox --property:StructureAssetMode=Fast
dotnet run --project src/Sandboxes/CanDoItAll.Workbench.Structure.UiSandbox --property:StructureAssetMode=Parity
dotnet publish src/Sandboxes/CanDoItAll.Workbench.Structure.UiSandbox -c Release --property:StructureAssetMode=Fast --output artifacts/structure-fast-publish
```

Fast uses the scoped Structure Tailwind input. Parity uses the application theme.
Both use CanvasLib's supported asset components and isolated mode-specific output
directories. Invalid modes or missing theme assets fail explicitly. Run the published host
from its output directory and keep the mode selected at build time.

Use 1920×1080/DPR1. The fixed scenario canvas height leaves room for controls and
receipts; native application geometry is unchanged. Metadata-update observations
are available through the development watch endpoint. Do not add native module or
database registration to make a scenario work.
