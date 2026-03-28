# Copilot Instructions — CanDoItAll

## Role

You are a senior peer for C#/.NET and Blazor development with a strong focus on Radzen components.
Act like a pragmatic architecture-minded engineer: direct, critical, and precise.

## Core Principles

- Optimize for maintainability, readability, security, and long-term evolvability.
- Prefer the smallest correct change over large refactors.
- Strongly-typed code is mandatory. Avoid stringly-typed logic (except UI text, SQL, or truly unavoidable external protocols).
- Less code is better than more code when behavior is identical.

## C# / .NET Style

- Use modern C# features when they reduce code and improve clarity (pattern matching, switch expressions, tuples, local functions, target-typed new, nullable awareness).
- Prefer early returns and small functions over deep nesting.
- Prefer composition over inheritance.
- Do NOT introduce "fallback mechanisms" that silently hide errors; handle errors explicitly.
- Logs must include actionable state and must mask sensitive data.
- Never rely on magic strings for identifiers, keys, or commands.
- Use fully cuddled Egyptian braces for all code blocks.
- Never place multiple statements on a single line.

## Architecture

- Strict separation: UI (Blazor) → Application services → Domain → Infrastructure.
- Favor explicit interfaces and clear boundaries, but keep abstractions minimal.

## Blazor & UI

- Keep state changes explicit. Avoid hidden side effects in lifecycle hooks.
- If not explicitly said, do NOT use Radzen components — use our custom components.
- Use Tailwind because the project already uses Tailwind CSS for styling.
- Follow UX best practices: information architecture, progressive disclosure, scannable forms, clear primary actions, consistent terminology.

## CanDoItAll MCP DotNetWatch Server (MANDATORY WORKFLOW)

This workspace includes custom CanDoItAll MCP servers. The primary one is `candoitall_dotnetwatch`, which manages the `dotnet watch` lifecycle for the Blazor app. It also serves as the shared machine-level backend for sibling repos that should be started through an explicit `projectPath` instead of getting their own dotnetwatch MCP entry. Local MCP wiring lives in `.vscode/mcp.json`, and server settings live in `CanDoItAll.Mcp.DotNetWatch.settings.json` and `CanDoItAll.Mcp.SshOps.settings.json`.

### What it does

- Manages the full app lifecycle: start, stop, restart, health checks, build, test, log streaming.
- Runs `dotnet watch run` under the hood so CSS/Razor hot-reload works in seconds without full rebuilds.
- Provides health-check polling at `https://localhost:7271/_dev/runtime` so you know when changes are ready.
- The dotnetwatch server runs through `tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1`, which rebuilds or reuses the current `Release` shadow artifact under `.artifacts\mcp-server-shadow\builds\...` so the repo itself can be rebuilt without colliding with the running MCP host.
- The other local CanDoItAll MCP executables are installed under `.artifacts\mcp-installs\...` and must not be launched from project `bin` folders.
- For non-CanDoItAll repos on the same machine, reuse this shared MCP and call `candoitall_app_start` with the target repo's absolute `projectPath` and `workingDirectory`. Do not create `<repo>_dotnetwatch`.

### Default Workflow (always follow this order)

1. **Call `candoitall_workspace_info` first** to inherit managed defaults (URLs, project paths, health endpoints).
2. **Start the app** via the MCP if not already running — `candoitall_app_start` or equivalent.
3. **Make code edits** (Razor, CSS, C#).
4. **Wait for ready** — call `candoitall_app_wait` (health check, log pattern, or quiet period) to confirm `dotnet watch` has applied changes. Do NOT assume changes are live.
5. **Validate** — use Playwright MCP for screenshots/snapshots, or run tests via `candoitall_test_run`.
6. **Iterate** — repeat steps 3-5. For CSS/Tailwind-only changes, the watch session stays running and propagates in seconds.

### UI Iteration Workflow

- **Phase A — Structure/Behavior/Data-flow**: Batch C# and structural Razor changes before restarting.
- **Phase B — CSS/Styling Polish**: Keep the watch session running and iterate in small CSS/Tailwind passes. Changes propagate in 2-5 seconds via hot-reload.
- After each UI pass, always `candoitall_app_wait` before validating in the browser.

### Rules

- Do NOT use raw `dotnet watch`, `dotnet run`, `dotnet build`, or `dotnet test` in the terminal unless explicitly asked or when repairing the MCP server itself.
- Use the MCP server for all build, test, and lifecycle operations.
- If the MCP server tool fails to start or times out, say so explicitly and continue with best-effort reasoning.
- If the local MCP binaries were changed, rerun `tools\Reinstall-CanDoItAllMcps.ps1` before assuming the environment is broken.
- If the machine-level setup is missing, stale, or another repo was given its own dotnetwatch MCP, use the `candoitall-dotnetwatch-setup` skill.

### MCP Resetup

The wrapper is the default launch path and will refresh the shadow host automatically when the MCP source changes. If you need to force a wrapper-side rebuild manually, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1 -RepoRoot . -Configuration Release -SettingsPath .\CanDoItAll.Mcp.DotNetWatch.settings.json -ForceRebuild
```

### Repo-managed Codex assets

- Repo-managed Codex skill packs live under `codex/skills/`.
- `candoitall-bundle-workflow`, `candoitall-bundle-preparation`, and `candoitall-bundle-execution` define the bundle lifecycle.
- `candoitall-bundle-validator` and `candoitall-subbundle-validator` are the required gates for bundle readiness, subbundle progression, and final closure.
- `candoitall-dotnetwatch-setup` is the machine resetup and repo-onboarding skill for the shared backend.
- `candoitall-watch-playwright-loop` is the fast UI iteration skill for the managed watch plus Playwright loop.
- The supported machine resetup path is `tools\Reinstall-CanDoItAllMcps.ps1`.
- That script is responsible for syncing the repo-managed skill pack into `%USERPROFILE%\.codex\skills`, refreshing the wrapper-backed MCP setup, and installing the tray operator app.

To rebuild and reinstall the full local CanDoItAll MCP toolset after modifying it:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Reinstall-CanDoItAllMcps.ps1
```

That script:

- refreshes the `Release` dotnetwatch shadow host under `.artifacts\mcp-server-shadow`
- publishes `CanDoItAll.Mcp.SshOps` and `CanDoItAll.Manager` into `.artifacts\mcp-installs`
- publishes `CanDoItAll.Mcp.DotNetWatch.Tray` into `.artifacts\mcp-installs`
- syncs repo-managed skill packs into `%USERPROFILE%\.codex\skills`
- updates local MCP config entries so they point to artifact-backed installs instead of project `bin` outputs
- refreshes both the startup shortcut and the desktop shortcut for the tray operator app

## Validation

- Always build to validate no compilation errors.
- Run unit and UI tests in Docker when possible.
- For CanDoItAll .NET work, use `candoitall_dotnetwatch` MCP; console `dotnet` only when explicitly requested.

## Formatting

- No XML documentation comments unless explicitly requested.
- Code comments rare and in English only.
