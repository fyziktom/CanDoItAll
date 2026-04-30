# Round 4 Execution Report

Date: 2026-04-28
Repository: `C:\repositories\CanDoItAll`
Bundle: `codex/bundles/candoitall-maf-round4-recovery-test-stabilization`

## Summary

Implemented the round 4 recovery/test-stabilization work and kept the release claim on Policy B: documented default gate plus explicit stable extended gates. The no-filter suite is not claimed green because browser and DotNetWatch live-process quarantines remain.

Key changes:

- Removed the committed provider secret from appsettings before execution; `src/CanDoItAll.Web/appsettings.json` now contains only non-secret configuration.
- Tightened secret scanning in `tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs` to reject realistic OpenAI, GitHub, and Azure storage key patterns.
- Added `tests/CanDoItAll.Tests.Unit/SnapshotIntegrityTests.cs` for required round 4 deliverable files.
- Hardened process tool policy in `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` so unregistered `processes_*` tools classify as `Unknown` and are denied instead of silently behaving as read-like tools.
- Preserved existing typed recovery/rework/proof/ledger implementation in `src/CanDoItAll.Modules.Processes/Automation/Recovery/AgentRecoveryModels.cs` and dispatcher journal integration in `ProcessRunAutomationDispatchService.RecoveryPackets.cs`.
- Stabilized Release/no-build Playwright and MCP stdio path handling with shared path helpers and test host lane configuration.
- Categorized Playwright, live-process, long-running, and quarantined tests and documented the stable gates in `docs/testing.md`.
- Fixed targeted integration/component failures around seed artifacts, SQLite baseline migration detection, project-structure API host lifetime, storage runtime worker suppression, component expectations, and catalog reads under held locks.

## Commands and Outcomes

Prepared bundle validation:

```powershell
python codex\bundles\candoitall-maf-round4-recovery-test-stabilization\scripts\validate_bundle.py --stage prepared
```

Outcome: passed.

Environment:

```powershell
dotnet --info
```

Outcome: passed. SDK `10.0.203`, host `10.0.7`.

Restore:

```powershell
dotnet restore CanDoItAll.slnx
```

Outcome: passed with existing NuGet advisory warnings.

Build:

```powershell
dotnet build CanDoItAll.slnx --configuration Release --no-restore /m:1
```

Outcome: passed. Final run reported 56 warnings, 0 errors. Warnings are existing NuGet advisory/package-reference warnings including `NU1902`, `NU1904`, and DotNetWatch `NU1510`.

Focused policy/secret/snapshot tests:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~SecretScanningTests|FullyQualifiedName~SnapshotIntegrityTests|FullyQualifiedName~AgentToolInvocationPolicyTests" /m:1
```

Outcome: passed, 45 tests.

Focused process-host timing regression:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~LocalWorkspaceProcessHostTests.ExecuteAsync_returns_after_parent_exit_when_descendant_keeps_redirected_pipe_open" /m:1
```

Outcome: passed, 1 test.

Default green gate:

```powershell
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

Outcome: passed, 1,169 tests:

- `CanDoItAll.Mcp.Components.Tests`: 15 passed.
- `CanDoItAll.Mcp.DotNetWatch.Tests`: 43 passed.
- `CanDoItAll.Mcp.Processes.Tests`: 27 passed.
- `CanDoItAll.Mcp.ProjectStructure.Tests`: 12 passed.
- `CanDoItAll.Tests.Components`: 350 passed.
- `CanDoItAll.Tests.Integration`: 468 passed.
- `CanDoItAll.Tests.Unit`: 254 passed.

Stable Playwright extended gate:

```powershell
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration Release --no-build --filter "Category!=Quarantined" /m:1
```

Outcome: passed, 38 tests. Earlier stable browser attempts exposed additional CRM/HR, project-structure drag, and SC04 scenario harness instability; those specific flows were marked `Quarantined` and documented.

MCP stdio/live integration gate:

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "Category=LiveProcess" /m:1
```

Outcome: passed, 4 tests.

DotNetWatch stable live-process gate:

```powershell
dotnet test tests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Quarantined" /m:1
```

Outcome: passed, 23 tests.

No-filter full suite:

```powershell
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

Outcome: not claimed green. The full no-filter policy remains blocked by intentionally quarantined browser and live-process tests. Evidence captured during execution:

- Playwright no-filter project run failed before quarantine expansion: 19 failed, 44 passed, 63 total.
- DotNetWatch live/long no-filter project run failed before quarantine: 9 failed, 23 passed, 32 total.

Whitespace check:

```powershell
git diff --check
```

Outcome: passed; no whitespace errors. Git reported line-ending conversion warnings only.

## Quarantines

Playwright quarantines cover prompt-library generated asset dependencies, generated browser artifact baselines, CRM/HR persistence/routing flows, project-structure drag/browser artifact timing, database-profile smoke flows, WebGL timing, and SC04 approval scenario harness behavior. The owner is the browser/runtime feature area for each flow; remove each quarantine only with a focused fix and replacement browser evidence.

DotNetWatch quarantines cover current-repository wrapper/resume and validation-matrix live-process instability. The owner is DotNetWatch/runtime; next actions are to isolate wrapper state, harden resume expectations, and update expected error taxonomy.

Prompt-library-backed component/integration quarantines remain because `output/prompt-library/manifest.json` is ignored build output and is not produced by the Release build.

## Remaining Risks

- Existing NuGet advisory warnings remain unresolved, including critical `Microsoft.AspNetCore.DataProtection` advisory warnings and medium `OpenTelemetry.Api` advisory warnings.
- No-filter full suite remains non-green until quarantined browser/live-process flows are repaired.
- The previously exposed provider key must be treated as compromised and rotated or revoked outside the repository; the raw key is not reproduced in this report.
