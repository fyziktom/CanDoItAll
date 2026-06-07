# SB009 Proof Manifest

## Summary

- Subbundle: `SB009 - Gate C finalizer parity`
- Result: `Completed`
- Production source changed: `No - gate proof only after SB007/SB008`
- Owned requirements: null-finalizer no-apply, apply-on-result, transition shape, workflow/recovery/direct/subprocess finalizer parity, no Process Core, no production process-driver API, no UI/mobile drift.
- Semantic invariant contract: `bundle://proof/SB009/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `ef2cdb38adbf5fd739ad806ec3a282dcca89b9f7c46109a9c5abb4bd8470a609` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `54f275ca80214dfe4febb4f8c3d6f6913f0cfc089bcae355a6f09f64d62f4553` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `3ea485fa467783184da21c67f7bf4d2818f4941405717b4848944d9a63a14868` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerInputs.cs`
- `481ccfc9742b9fa57bd8d664dc717e56fe37bb4f02b84e9a8a78cb820fd3af13` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `a77ed0a2f5314c1eed678b91159a5af0242fb47f3ce31645784ab62cf9f2624b` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Critical build: `bundle://proof/SB009/transcripts/critical-build.txt`
- Focused architecture tests: `bundle://proof/SB009/transcripts/focused-architecture-tests.txt`
- Finalizer parity focused integration tests: `bundle://proof/SB009/transcripts/finalizer-parity-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB009/transcripts/source-assertions-and-scans.txt`

## Source-Level Assertions

- `ProcessDispatchFinalizerAdapter.FinalizeAndApplyAsync` returns without applying when finalizer output is null.
- `ProcessDispatchFinalizerAdapter.FinalizeAndApplyAsync` applies the finalized transition when finalizer output is non-null.
- Public finalizer entry points consume route/application finalizer input records.
- `ProcessDispatchFinalizerApplicationService` remains free of dispatcher aliases and route adapter calls.
- No Process Core, production process-driver API, UI/media drift, or implementation stub markers were found.

## Semantic Adequacy Gate

- Shallow-pass trap: a compile-only check could miss null-finalizer apply behavior, dropped recovery ids, wrong executor kind, or subprocess/workflow transition context drift.
- Adversarial negative proof: the SB009 focused integration test fails if null finalizer output applies a transition, if non-null output does not apply all four finalizer paths, or if workflow/recovery/direct/subprocess contexts lose their expected ids and flags.
- Semantic positive proof: build, architecture tests, finalizer parity integration tests, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB009/transcripts/source-assertions-and-scans.txt`

## Reopen Triggers

- Reopen `SB009` if null finalizer output applies a transition, any finalizer path stops applying a non-null result, workflow/recovery/direct/subprocess context fields change, dispatcher aliases leak into the application service, or forbidden Core/driver/UI/stub scans fail.
