# Dev Instructions Snippets

Use these snippets when a repo should rely on the shared CanDoItAll backend.

## Shared backend rule

```markdown
This repo must not add its own dotnetwatch MCP entry. Use the shared machine-level `candoitall_dotnetwatch` server installed from the CanDoItAll workspace.
```

## Start pattern

```markdown
Start this repo by calling `candoitall_app_start` with the repo's absolute `projectPath`, its matching `workingDirectory`, `launchProfile: "https"` when available, and `reuseIfCompatible: true`.
```

## Repair rule

```markdown
If the shared `candoitall_dotnetwatch` MCP is missing or stale, use the `candoitall-dotnetwatch-setup` skill and rerun `tools\Reinstall-CanDoItAllMcps.ps1` from the CanDoItAll repo. The script builds MCP source from the sibling `CanDoItAll.Mcp` repo and syncs skills from the main repo. Do not create another repo-specific dotnetwatch MCP.
```

## UI loop rule

```markdown
For UI work with Playwright, use the `candoitall-watch-playwright-loop` skill so edits stay one-at-a-time and browser-validated.
```
