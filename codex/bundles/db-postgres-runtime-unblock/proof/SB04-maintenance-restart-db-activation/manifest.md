# SB04 Proof Manifest

## Subbundle

SB04-maintenance-restart-db-activation — Completed.

Owned requirements: R5, R7, R10.

Semantic invariant contract: `bundle://proof/SB04-maintenance-restart-db-activation/semantic-invariants.md`.

## Changed Files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | See `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv` | See hash inventory | Makes activation restart-first and disables hidden hot switch by default. |
| `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSwitchingAbstractions.cs` | See hash inventory | See hash inventory | Adds `RequiresRestart`, `RuntimeChangedInProcess`, and message contract. |
| `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor` | See hash inventory | See hash inventory | Shows restart-required Data Sources activation feedback. |
| `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs` | See hash inventory | See hash inventory | Exposes activated vs runtime database state over API. |
| `repo://src/CanDoItAll.Web/Components/Layout/MainLayout*.cs*` | See hash inventory | See hash inventory | Updates database activation dialog language. |
| `repo://tests/CanDoItAll.Tests.Components/SettingsPageDataSourcesTests.cs` | See hash inventory | See hash inventory | Tests UI restart message. |
| `repo://tests/CanDoItAll.Tests.Playwright/DatabaseSwitchWorkbenchPlaywrightTests.cs` | See hash inventory | See hash inventory | Browser proof for stale artifact and cross-tab restart behavior. |

## Commands

| Command | Transcript path | Result |
|---|---|---|
| Component Data Sources tests | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-components-database-sources.txt` | Passed 10 tests. |
| Playwright database switch test | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-playwright-database-switch.txt` | Passed 1 test. |
| Focused PostgreSQL integration sweep | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | Passed 452 tests. |

## Semantic Positive Proof

Activating a different database persists the active profile but returns `RequiresRestart=true` and `RuntimeChangedInProcess=false`. UI and API distinguish the activated profile from the currently running canonical profile.

## Adversarial Negative Proof

The Playwright test proves stale artifacts and cross-tab runtime state remain on the running canonical profile after activation, rejecting the old shallow hot-switch behavior where a click silently moves the process runtime.

## Canonicality Proof

Runtime profile changes are operator transitions. Normal requests do not straddle profiles because the process keeps its current canonical runtime until restart.

## Anti-Stub Audit

`bundle://proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt` found no stub markers in changed production files.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Restart-required activation result | `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor` and `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.DatabaseEndpoints.cs` | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-components-database-sources.txt` | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-playwright-database-switch.txt` |

## Browser Validation Analytics

| Route | Viewport | Actions | Assertions | Screenshot paths | Result |
|---|---|---|---|---|---|
| Data Sources / Project Structure switch path | Desktop and responsive | Activate alternate PostgreSQL profile, inspect stale artifact behavior, open second tab, resize | Restart-required UI visible; current runtime remains stable until restart | `bundle://proof/SB04-maintenance-restart-db-activation/browser/db-switch-stale-artifact-recovery-desktop.png`, `bundle://proof/SB04-maintenance-restart-db-activation/browser/db-switch-cross-tab-desktop.png`, `bundle://proof/SB04-maintenance-restart-db-activation/browser/db-switch-stale-artifact-responsive.png` | Passed |

## Remaining Risks

No SB04 implementation risk remains. Operational docs should tell users that profile activation takes effect after process restart.
