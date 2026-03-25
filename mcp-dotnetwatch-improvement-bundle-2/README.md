# MCP Dotnet Watch Improvement Bundle 2

This bundle isolates why the current managed `dotnet watch` and managed build flows are slower or less reliable than plain local commands.

## Executive Summary

1. The biggest hot-reload problem is not generic MCP overhead. It is the current `SourceWatch` launch shape. The managed lane starts `dotnet watch` with `--artifacts-path`, and that alone was enough to reproduce a broken browser-visible hot reload loop outside MCP.
2. Plain `dotnet watch` still hit the user's expected simple-change loop: about 9-15 seconds from file change to visible text change. The MCP-style watch hit a similar `Hot reload succeeded` log time, but the changed HTML never became visible.
3. The build lane is also paying for an expensive shape: isolated artifacts output, restore on every isolated output tree, and MSBuild server disabled. The current build path is slower mostly because of those choices, not because "managed analysis" inherently costs that much.
4. `RevisionConfirmed` is currently a false confidence signal for in-process hot reload. The runtime endpoint only exposes `DOTNET_WATCH_ITERATION`, which does not change for normal in-process hot reload edits.

## Top Findings

- `--artifacts-path` on `dotnet watch` is the primary hot-reload regression.
- `DOTNET_CLI_USE_MSBUILD_SERVER=0` adds measurable warm-build cost.
- Isolated artifacts output prevents a cheap `--no-restore` no-op build unless restore is also performed into that isolated tree.
- MCP currently clears `watch.pendingChange` on the log line `Hot reload succeeded.` before it has any end-to-end confirmation that the browser-visible result changed.

## Bundle Map

- `01-watch-benchmark-matrix.md`
- `02-managed-watch-live-run.md`
- `03-build-benchmark-findings.md`
- `04-code-path-root-causes.md`
- `05-architecture-options.md`
- `06-followup-agent-plan.md`

## Key Artifacts

- `artifacts/plain-projects-page.json`
- `artifacts/plain-page-header.json`
- `artifacts/managedenvonly-projects-page.json`
- `artifacts/artifactsonly-projects-page.json`
- `artifacts/artifactsonly-page-header.json`
- `artifacts/managedlike-projects-page.json`
- `artifacts/mcp-managed-projects-page-live-run-2026-03-25.json`
- `artifacts/build-bench/summary.json`
- `artifacts/build-factor/summary.json`
- `tools/watch_benchmark.js`
- `tools/run_build_benchmarks.ps1`

## Recommended Direction

The repair path should start with lane separation:

- `SourceWatch` should behave like plain local `dotnet watch` and optimize for UI inner-loop speed and correctness.
- isolated artifacts output should stay in build/test/atomic lanes where isolation matters more than hot-reload fidelity.
- runtime confirmation for hot reload should use a real hot-reload generation token, not `DOTNET_WATCH_ITERATION`.
