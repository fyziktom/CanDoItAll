# SB034 Proof Manifest

## Summary

- Subbundle: `SB034 - Broad smoke matrix`
- Result: `Completed`
- Production source changed: `No - validation-only final smoke matrix`
- Owned requirements: prove the bundle still builds, full unit tests pass, focused process integration coverage passes, and forbidden Core/driver/UI/stub/collapsed-row scans pass.
- Semantic invariant contract: `bundle://proof/SB034/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `5bbdc99f5414fe76a22b34645ae323607959435412157b5c1672a7f9a94d4335` `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/reviews/01-execution-report.md`
- `6b6e081b8277aa58e5d10ca73a94e6cb1c0a26af5f547291ec85c547b31f196e` `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/README.md`
- `ba04bc8e0d8ddb433e1ad7b519b2042f0c1eddb90fe39507b4e41d7a5cd8dc40` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `4198628fb6cc1d135dbe0799210a51a9fcfa7518a3f1740eb61167ad702a4b66` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`
- `ca7891b140cbdba79358295343f3a9dce5a525ee39f64e83d4035eb732efe736` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `ef2cdb38adbf5fd739ad806ec3a282dcca89b9f7c46109a9c5abb4bd8470a609` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `3ea485fa467783184da21c67f7bf4d2818f4941405717b4848944d9a63a14868` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerInputs.cs`
- `481ccfc9742b9fa57bd8d664dc717e56fe37bb4f02b84e9a8a78cb820fd3af13` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `a77ed0a2f5314c1eed678b91159a5af0242fb47f3ce31645784ab62cf9f2624b` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`

## Command Transcripts

- Solution build: `bundle://proof/SB034/transcripts/build.txt`
- Full unit tests: `bundle://proof/SB034/transcripts/full-unit-tests.txt`
- Focused integration tests: `bundle://proof/SB034/transcripts/focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB034/transcripts/source-assertions-and-scans.txt`

## Source-Level Assertions

- `dotnet build CanDoItAll.slnx --configuration Debug --no-restore` passed with 0 errors.
- Full `CanDoItAll.Tests.Unit` project passed with 1,029 tests.
- Focused `CanDoItAll.Tests.Integration` process matrix passed with 13 tests across route, subprocess, projection/artifact, finalizer, and execution behavior.
- Execution report has separate passed rows for `SB001` through `SB034` and no collapsed `SB001-SB034` or `SB001-SB036` row.
- Production source has no Process Core project and no process-driver runtime tokens.
- No UI/mobile/media changed paths outside bundle docs and no stub markers exist in changed production dispatch files.

## Semantic Adequacy Gate

- Shallow-pass trap: a build-only final smoke could miss unit/integration regressions, collapsed proof rows, forbidden Core/driver drift, or UI/media edits.
- Adversarial negative proof: source assertions fail on missing transcript success, collapsed rows, Core project creation, production process-driver tokens, UI/mobile/media drift, or stub markers.
- Semantic positive proof: build, full unit tests, focused integration tests, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB034/transcripts/source-assertions-and-scans.txt`

## Reopen Triggers

- Reopen `SB034` if build/unit/focused integration proof fails, report rows collapse, Process Core or production driver APIs appear, UI/media drift appears, or stub scans fail.
