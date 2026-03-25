# Bundle 3: Tray Operator Manager

Status:

- completed on March 25, 2026

Goal:

- add a Windows tray operator shell for the CanDoItAll dotnetwatch backend
- make backend health, duplicate-instance problems, and manual recovery visible outside the MCP stdio session
- keep the hot-reload loop at bundle-2 performance levels

Acceptance gates:

- tray icon can open the backend manager page for the active workspace
- tray icon surfaces unhealthy or duplicate backend situations
- tray icon can trigger at least the core operator actions needed during long runs
- resetup script installs or updates the tray app, current MCP launch path, and repo-managed Codex skill pack
- hot-reload benchmark with tray active stays within the same practical range as bundle 2

Files:

- `01-problem-and-goals.md`
- `02-architecture-and-scope.md`
- `03-execution-subbundles.md`
- `04-validation-plan.md`
- `05-implementation-results.md`
- `subbundles/`
- `artifacts/`

Implementation summary:

- added a Windows tray operator app for the dotnetwatch backend with status polling, notifications, manager-page launch, log-folder launch, and recover or restart actions
- added installed and headless validation paths for the tray app so resetup and operator checks use the same published binary
- added a repo-managed Codex skill pack for the CanDoItAll watch plus Playwright loop and sync that pack into `%USERPROFILE%\.codex\skills`
- added `tools/Reinstall-CanDoItAllMcps.ps1` to prepare the wrapper shadow build, publish current MCP helpers, update MCP config, and create the tray startup shortcut
- added backend-catalog cleanup to resetup and delete-friendly catalog reads so stale records stop accumulating and blocking tray cleanup
- validated tray-on versus tray-off hot-reload timing and wrote the comparison to `artifacts/tray-benchmark-summary.json`
- extended the backend manager with:
  - `Open HTTP App` and `Open HTTPS App` buttons on live sessions
  - `Start Default App (HTTPS)` in the backend control bar
  - project browsing plus project-path launch controls for arbitrary `.csproj` files
  - support for sibling-repo project starts through configured external project roots
- validated the manager against `C:\repositories\pveinvoicing\PVEInvoicing\PVEInvoicing\PVEInvoicing.csproj` with a Playwright-driven browser flow plus native Windows dialog automation, and wrote the result to `artifacts/manager-pveinvoicing-validation.json`
