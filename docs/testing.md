# Testing

The default verification gate is the stable Release suite. Browser, live-process, long-running, and quarantined tests are explicit extended gates.

## Build

```powershell
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore /m:1
```

Use `/m:1` on shared developer machines when local MCP or watch processes are running, because concurrent builds can contend for the same `bin` and `obj` files.

## Default Gate

```powershell
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

This is the routine green gate. It intentionally excludes browser automation, process-spawning MCP stdio tests, live dotnet-watch integration tests, and any explicitly quarantined tests.

Current quarantines:

- `PromptFactoryPageTests`, the prompt-library-backed `PromptFactoryServiceIntegrationTests`, and `ProjectWorkbenchServiceIntegrationTests.CreateObjectAsync_links_prompt_flow_nodes_to_blank_prompt_sessions` require the generated `output/prompt-library/manifest.json` pack. The pack is ignored build output and is not produced by the Release build, so these tests stay out of the default gate until the prompt-library asset generation is wired into a repeatable build/test input.
- Several Playwright prompt-library, generated-artifact, CRM/HR, process, database-profile, and WebGL smoke flows are marked `Quarantined`. They currently need generated prompt-library output, refreshed browser artifact baselines, or selector/timing repairs before they can be part of the stable browser gate. Browser/runtime owners should remove the trait one test at a time with replacement evidence.
- Nine DotNetWatch live-process integration tests are marked `Quarantined` after the no-filter live/long gate exposed current-repository wrapper and resume instability. DotNetWatch owners should isolate wrapper state, harden resume expectations, and update the expected error taxonomy before returning them to the stable live gate.

## Extended Gates

```powershell
dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --no-build --filter "Category!=Quarantined" /m:1
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "Category=LiveProcess" /m:1
dotnet test ..\CanDoItAll.Mcp\tests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Quarantined" /m:1
```

Playwright hosts and MCP stdio tests infer the active build configuration from the test output path. Set `CANDOITALL_TEST_CONFIGURATION` only when running from a non-standard output layout. MCP tests live in the sibling `CanDoItAll.Mcp` repo; the DotNetWatch integration assembly uses this repo for workspace settings and runtime state. The DotNetWatch integration assembly is categorized as `LiveProcess` and `LongRunning`, so `Category!=Quarantined` is its stable extended gate.

## Full Suite

```powershell
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

Do not report the full suite as green unless this exact no-filter command passes. The current default contract is the filtered stable gate above; no-filter browser and DotNetWatch live/long runs are tracked separately because quarantined tests are still expected to fail.
