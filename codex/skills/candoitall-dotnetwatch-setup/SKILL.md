---
name: candoitall-dotnetwatch-setup
description: Use when Codex needs to install, reinstall, repair, or validate the shared CanDoItAll dotnetwatch MCP and tray/backend setup on a machine, or when onboarding another repo to use the shared backend instead of creating a repo-specific dotnetwatch MCP.
---

# CanDoItAll DotNetWatch Setup

Use one shared `candoitall_dotnetwatch` MCP per machine. Runtime settings, installs, and skills are anchored to the CanDoItAll repo; MCP source is built from the sibling `CanDoItAll.Mcp` repo. The shared backend can start projects from sibling repos. Do not create extra repo-specific dotnetwatch MCP servers when the shared backend can manage the target project.

## Goal

- install or repair the shared CanDoItAll dotnetwatch MCP cleanly
- keep Codex config, VS Code config, synced skills, tray shortcuts, and repo instructions aligned
- onboard other repos by using `candoitall_app_start` with an explicit project path, not by creating `<repo>_dotnetwatch`

## Required flow

1. Audit the current machine and repo wiring:
   - `%USERPROFILE%\.codex\config.toml`
   - `<repo>\.vscode\mcp.json`
   - repo instructions
   - stale repo-specific dotnetwatch settings files or MCP entries
2. If setup is missing, stale, or inconsistent, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Reinstall-CanDoItAllMcps.ps1 -McpRepoRoot ..\CanDoItAll.Mcp
```

3. Validate the result:
   - shared `candoitall_dotnetwatch` entry points at the wrapper in the sibling `CanDoItAll.Mcp` repo
   - the wrapper args include both the CanDoItAll workspace root and the MCP repo root
   - `%USERPROFILE%\.codex\skills` contains repo-managed skills
   - tray startup shortcut exists
   - tray desktop shortcut exists
   - repo instructions describe the shared-backend pattern
   - if the immediate next task is UI-heavy, the setup can support one real headed watch plus Playwright smoke pass without inventing another MCP
4. For another repo, remove repo-specific dotnetwatch MCP wiring and replace it with shared-backend instructions.
5. If the next task is UI iteration, switch to `candoitall-watch-playwright-loop`.

## Rules

- Do not add a dedicated `zyphonote_dotnetwatch`-style MCP when `candoitall_dotnetwatch` can start that repo's `.csproj`.
- Prefer `candoitall_app_start` with explicit `projectPath`, `workingDirectory`, and `launchProfile` for non-CanDoItAll apps.
- Use the tray app or backend manager project picker only as a control surface for the same shared backend, not as a second installation path.
- Update docs and setup together. A repaired machine with stale repo instructions will regress.

## References

- Read [references/resetup-and-repair-checklist.md](references/resetup-and-repair-checklist.md) for the exact reinstall and repair sequence.
- Read [references/repo-onboarding-pattern.md](references/repo-onboarding-pattern.md) for how to migrate another repo off a dedicated dotnetwatch MCP.
- Read [references/dev-instructions-snippets.md](references/dev-instructions-snippets.md) for reusable repo-instruction text.
- Read [references/validation-checklist.md](references/validation-checklist.md) for the post-install validation checklist.
