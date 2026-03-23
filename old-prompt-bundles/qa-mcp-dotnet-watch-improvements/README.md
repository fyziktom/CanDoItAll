# QA Package: DotNet Watch MCP Improvements

This package is the handoff for improving `CanDoItAll.Mcp.DotNetWatch` so an AI agent can trust live-edit synchronization instead of guessing.

## Executive Summary

The MCP server already delivers a real productivity win for simple UI work:

- A tiny Razor text edit was detected by `dotnet watch` in about 3-6 seconds.
- The same edit through a plain PowerShell stop/start flow took about 86 seconds to become ready again.

However, the current synchronization contract is not reliable enough yet for complex live editing:

- `candoitall_app_wait` can report success while `dotnet watch` is still evaluating, rebuilding, or restarting the app.
- `Healthy` state becomes stale after the first successful start and is reused without re-probing after later file changes.
- The watch lifecycle parser does not match the real `dotnet watch` messages observed during C# add/delete and restart-needed flows.
- Running `candoitall_tests_run` against the MCP server's own project graph fails because the live server locks its own output assemblies.
- The repo config exposes `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests`, but `CanDoItAll.slnx` does not include that project.

## Recommended Order

1. Fix correctness of wait semantics and watch lifecycle tracking.
2. Surface watch-generation/runtime identity details to the agent.
3. Fix self-host test isolation so the server can validate itself while running.
4. Expand regression coverage around real watch flows, not just static unit behavior.

## Package Contents

- `01-findings.md`
- `02-speed-and-evidence.md`
- `03-reproduction-playbook.md`
- `04-implementation-plan.md`
- `05-regression-checklists.md`
- `06-implementation-prompts.md`
- `07-self-review.md`
