# Implementation Results

Status:

- completed on March 25, 2026

Code changes:

- added the tray project under `tools/CanDoItAll.Mcp.DotNetWatch.Tray/` with:
  - `Program.cs`
  - `TrayOptions.cs`
  - `BackendTrayController.cs`
  - `BackendTrayApplicationContext.cs`
  - `TrayHeadlessRunner.cs`
  - `BackendCatalogModels.cs`
- added repo-managed Codex skill assets under `codex/skills/candoitall-watch-playwright-loop/`
- added the resetup script at `tools/Reinstall-CanDoItAllMcps.ps1`
- restored wrapper prepare-only support in `tools/CanDoItAll.Mcp.DotNetWatch/Start-CanDoItAllDotNetWatchMcp.ps1`
- extended the backend manager surface in:
  - `src/CanDoItAll.Mcp.DotNetWatch/Backend/BackendModels.cs`
  - `src/CanDoItAll.Mcp.DotNetWatch/Manager/BackendManagerService.cs`
  - `src/CanDoItAll.Mcp.DotNetWatch/Manager/BackendDashboardPage.cs`
  - `src/CanDoItAll.Mcp.DotNetWatch/Manager/ProjectPathPicker.cs`
  - `src/CanDoItAll.Mcp.DotNetWatch/Runtime/ProjectLaunchSettingsResolver.cs`
  - `src/CanDoItAll.Mcp.DotNetWatch/Security/SecurityServices.cs`
  - `src/CanDoItAll.Mcp.DotNetWatch/Program.cs`
  - `CanDoItAll.Mcp.DotNetWatch.settings.json`
- updated the repo MCP wiring and validation coverage in:
  - `.vscode/mcp.json`
  - `.github/copilot-instructions.md`
  - `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/ValidationHarness.cs`
  - `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/BootstrapValidationTests.cs`
- expanded focused coverage in:
  - `tests/CanDoItAll.Mcp.DotNetWatch.Tests/InfrastructureTests.cs`
- hardened backend-catalog cleanup by allowing delete-friendly reads in:
  - `src/CanDoItAll.Mcp.DotNetWatch/Backend/GlobalBackendCatalogStore.cs`
  - `tools/CanDoItAll.Mcp.DotNetWatch.Tray/BackendTrayController.cs`

Validation:

- tray build passed:
  - `dotnet build tools\\CanDoItAll.Mcp.DotNetWatch.Tray\\CanDoItAll.Mcp.DotNetWatch.Tray.csproj --nologo`
- unit tests passed:
  - `38/38` in `CanDoItAll.Mcp.DotNetWatch.Tests`
- focused wrapper and integration validation passed:
  - `5/5` in `CanDoItAll.Mcp.DotNetWatch.IntegrationTests`
- resetup script passed on the live repo path and published:
  - `.artifacts/mcp-installs/CanDoItAll.Mcp.SshOps/current/`
  - `.artifacts/mcp-installs/CanDoItAll.Manager/current/`
  - `.artifacts/mcp-installs/CanDoItAll.Mcp.DotNetWatch.Tray/current/`
- resetup also:
  - synced the repo skill to `%USERPROFILE%\\.codex\\skills\\candoitall-watch-playwright-loop`
  - updated `%USERPROFILE%\\.codex\\config.toml`
  - updated `.vscode\\mcp.json`
  - created or refreshed the tray startup shortcut
  - removed `317` stale backend-catalog records for this workspace during the final run
- installed tray validation succeeded:
  - headless `status` reported `Missing` before recovery
  - headless `recover` brought the workspace backend back to `Healthy`
  - headless `restart` replaced the backend PID and returned to `Healthy`
- manager validation succeeded against `pveinvoicing`:
  - native `Browse Project` selected `C:\repositories\pveinvoicing\PVEInvoicing\PVEInvoicing\PVEInvoicing.csproj`
  - `Start Project (HTTPS)` launched a live watch session with both `http://localhost:5139` and `https://localhost:7267`
  - Playwright navigation through the manager’s `Open HTTPS App` button reached `https://localhost:7267/Account/Login?ReturnUrl=%2F`
  - the launched app responded `200` with page title `Log in`
  - validation artifact:
    - `artifacts/manager-pveinvoicing-validation.json`
  - screenshots:
    - `artifacts/manager-pveinvoicing-browse.png`
    - `artifacts/manager-pveinvoicing-app.png`

Performance:

- summary artifact:
  - `artifacts/tray-benchmark-summary.json`
- final visible-after-reload measurements:
  - page header edit with tray active: `12765 ms`
  - page header edit with tray inactive: `11756 ms`
  - projects page edit with tray active: `17557 ms`
  - projects page edit with tray inactive: `17550 ms`
- tray impact versus tray-off:
  - page header delta: `+1009 ms`
  - projects page delta: `+7 ms`
- comparison to bundle-2 isolated benchmark:
  - page header delta: `+4620 ms`
  - projects page delta: `+5854 ms`
- conclusion:
  - the tray process does not materially affect the hot-reload loop
  - the current shared benchmark path is slower than the bundle-2 isolated baseline for both tray-on and tray-off runs, so that regression is outside the tray bundle itself

Residual risks:

- the shared benchmark path is still slower than the isolated bundle-2 baseline even when the tray is not running
- cold watch startup remains noisy in this benchmark path at roughly `85-97s`; bundle 3 did not target startup tuning
- headless `recover` and `restart` completed successfully but emitted blank stdout in shell redirection during validation; follow-up `status` confirmed success, so this is a headless-output quirk rather than a tray-UI failure
- native manager browse validation is sensitive to stale `Open` dialogs left behind by interrupted runs; before retrying the browse flow, clear old dialogs or the keyboard helper may attach to the wrong picker instance
