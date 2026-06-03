# Scenario Packet: Expense Tracker Lite

Build a client-only Blazor WebAssembly PWA expense tracker for local personal entries.

## Required Behavior

- Add amount, category, description, and date.
- Show total and category totals.
- Delete an entry.
- Persist entries in local storage after reload.

## Forbidden Scope

- No banking API.
- No authentication.
- No backend database.

## SB04 Production-Path Harness Constraints

- Create generated app source in the current-run generated app output root named `GeneratedBlazorApp`.
- Treat that root as the product root. The runnable host project must be directly under that root as `GeneratedBlazorApp/GeneratedBlazorApp.csproj` or another single direct non-test `.csproj`. Do not create sibling app roots or sibling test projects beside it.
- Put generated tests under `GeneratedBlazorApp/tests`. Do not create `*.Tests` beside `GeneratedBlazorApp`.
- Do not create probe, template-test, scratch, copied scaffold, or scenario-named project directories anywhere under this process run. Scaffold directly into `GeneratedBlazorApp` and repair that root in place.
- Do not set `BaseOutputPath`, `BaseIntermediateOutputPath`, or `MSBuildProjectExtensionsPath` in the generated host project. Use normal `bin`/`obj` output so generated build artifacts are not compiled as source on reruns.
- Do not ask the proof harness to write app source code.
- Record current-run artifacts that identify the generated source root, build command, runtime command or URL, browser proof expectation, and cleanup notes.
- Keep the app client-only and avoid backend/database/authentication integration unless the scenario packet asks for it.
- Before finalizing, inspect the app entry document and ensure every local stylesheet, script, manifest, icon, service-worker, and generated-style reference resolves at the exact served path. Do not leave browser console 404s for missing local static assets.
