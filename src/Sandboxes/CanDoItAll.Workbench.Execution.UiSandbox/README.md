# Workbench Execution sandbox

Independent development host for the production Execution renderers. `/workflow` provides
two separate add/start specimens with raw invalid input and original-intent observation.
`/process` provides linkage, confirmation and long staffing plans with actual picker,
details, switch and HR confirmation children. All effects are explicit in-memory fixtures;
this host neither prepares native launches nor executes models or processes.

From the repository root, build Parity assets with `npm run tailwind:build`, or Fast assets
with `npm run execution:css:build`. Run with `ASPNETCORE_ENVIRONMENT=Development` and
`dotnet run --project src/Sandboxes/CanDoItAll.Workbench.Execution.UiSandbox --no-launch-profile --urls http://127.0.0.1:56146`.
Select an available owned loopback port. Use `-p:ExecutionAssetMode=Fast` for Fast assets;
Parity is the default. Each mode has separate output and intermediate directories.

Use 1920×1080, DPR 1 for WB6 visual validation. Development `/_dev/runtime` exposes only
safe process and asset diagnostics. Published assets and the bounded watch loop require
separate proof; source startup alone does not establish them.
See [the architecture record](../../../docs/architecture/workbench-execution-ui.md).
