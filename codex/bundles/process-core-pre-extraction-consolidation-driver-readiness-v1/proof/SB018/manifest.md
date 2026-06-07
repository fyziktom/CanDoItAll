# SB018 Proof Manifest

## Summary

- Subbundle: `SB018 - Gate F subprocess parity`
- Result: `Completed`
- Production source changed: `No - critical gate proof only after SB016/SB017`
- Owned requirements: capability gap, observing state, terminal mirror, completed projection, parent finalizer, lineage, no Process Core, no production process-driver API, no UI/mobile drift.
- Semantic invariant contract: `bundle://proof/SB018/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `f77ced35fbf3cca10089b2efbcb8808170d94c09cbff0fee653d70a2d820888f` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeModels.cs`
- `3332a1f082a6995e70b197e1020f454556f4c666cecd767be90a9b789a9dbd34` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `3110e8be55e092148137698bf78490d62243b2b34bd84c18a808d79a6e212524` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`
- `d0a2af212a7ca31eeee1e97a57cae9a24e2e0dc5af19a689b8135fcb4f3513ac` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessCapabilityGapInspector.cs`
- `4645fc12c88adbf7714e5c555e2dd66be799f7cbc0ab9a238e0567bb354d7785` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPersistenceService.cs`
- `c8521e9e3dd4d116485649d1d658fc8c189e5c779e25f8c1162a27507a742ceb` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`
- `3edf8dc48748a8cbcc62957ef77747b239b823a0e464af417ec10a2bfbdaff91` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`
- `48763d7a0f64f8e0739e9d53c561dc6f47de176a322652aafdc9c2de59d6dacd` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Critical build: `bundle://proof/SB018/transcripts/critical-build.txt`
- Focused architecture tests: `bundle://proof/SB018/transcripts/focused-architecture-tests.txt`
- Subprocess parity focused integration tests: `bundle://proof/SB018/transcripts/subprocess-parity-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`

## Source-Level Assertions

- Runtime preserves start, capability-gap block, observing, terminal mirror, completed projection, and parent finalizer paths.
- Projection persistence owns completed child artifact projection and save changes.
- Subprocess finalizer and artifact validation preserve subprocess lineage semantics.
- Runtime/projection code avoids dispatcher alias leaks, Process Core, production process-driver APIs, UI/media drift, and implementation stubs.

## Semantic Adequacy Gate

- Shallow-pass trap: subprocess runtime could appear decoupled while losing capability-gap blocking, active child observing, terminal mirror, completed projection, parent finalizer, or child lineage validation.
- Adversarial negative proof: focused subprocess tests fail if lifecycle transition shape, capability-gap wording, projection delegation, subprocess candidate defaults, finalizer subprocess context, or child lineage validation changes.
- Semantic positive proof: build, full process-boundary architecture tests, subprocess parity integration tests, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`

## Reopen Triggers

- Reopen `SB018` if capability-gap blocking, observing behavior, terminal mirroring, completed projection persistence, parent finalizer context, subprocess lineage validation, or forbidden Core/driver/UI/stub scans fail.
