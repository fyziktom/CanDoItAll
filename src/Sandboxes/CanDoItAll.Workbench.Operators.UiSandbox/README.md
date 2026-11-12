# Workbench Operators sandbox

Independent development host for the actual Operators UI leaf. Two separate party specimens
exercise drafts, loading, failure, missing references, sensitive projection and known/unknown
fixture outcomes. Scenario IDs and effects are fixtures; this host does not bootstrap native
directory, project, vault or runtime services.

From the repository root, build shared Parity assets with `npm run css:build`, or Fast assets
with `npm run operators:css:build`. Run this project using
`dotnet run --project src/Sandboxes/CanDoItAll.Workbench.Operators.UiSandbox -p:OperatorsAssetMode=Parity`.
Choose an available owned loopback URL with `--urls`. Fast uses `OperatorsAssetMode=Fast`.
The modes have separate output/intermediate paths and require their compiled CSS files.

Use 1920×1080, DPR 1 for WB5 visual proof. Development `/_dev/runtime` exposes safe process,
asset and watch-generation diagnostics. It contains no native application identity or data.
See [the architecture record](../../../docs/architecture/workbench-operators-wb5.md).
