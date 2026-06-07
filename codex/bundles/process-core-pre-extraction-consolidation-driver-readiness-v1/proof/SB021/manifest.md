# SB021 Proof Manifest

## Summary

- Subbundle: `SB021 - Gate G execution parity`
- Result: `Completed`
- Production source changed: `No - critical gate proof only after SB019/SB020`
- Owned requirements: retry, provider repair, no-progress, competing execution, finalizer detail compatibility, no Process Core, no production process-driver API, no UI/mobile drift.
- Semantic invariant contract: `bundle://proof/SB021/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `ec3ae537d40ce5b798e0a194377c2358c07c5d3f3db5ac9ef630367c836bb958` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionModels.cs`
- `7931a4d3284190a01b65ef3434a28755dbeb9b43dba5767390ff41719b642072` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionAdapter.cs`
- `14edc825a9b8e78429ec49f60c551c53a1e1ebddc575552069daca17d1407b91` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`
- `4198628fb6cc1d135dbe0799210a51a9fcfa7518a3f1740eb61167ad702a4b66` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`
- `78d238ba26fce22b7afd198594688dd50beedc1a54cbe77ee513dc113cfd3633` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs`
- `7f8f3ef2b89128bebaa1b1263154054385531fa15299f7abf8fab32aac44fc7c` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `ef2cdb38adbf5fd739ad806ec3a282dcca89b9f7c46109a9c5abb4bd8470a609` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `f460cc17fa0ce5eb146a34fa2ac074834ac8ce190bd59621119fbc6f193aa150` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `a884b46bd9516af51c5853f4be57227878f7016fc9a60a328100731cfb17011d` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProviderRecovery.cs`
- `3d45923c62a0f513d6b1ba79fd71b53eed26371f5599d730212ae2762757d99c` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionAttemptLoopFacade.cs`
- `3b4fe164dee28e18bf41115a54de21194d932c7f4533b66a6d1620acca9fbec6` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRepairCoordinator.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Critical build: `bundle://proof/SB021/transcripts/critical-build.txt`
- Focused architecture test: `bundle://proof/SB021/transcripts/execution-parity-architecture-test.txt`
- Execution parity focused integration tests: `bundle://proof/SB021/transcripts/execution-parity-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`

## Source-Level Assertions

- Direct-agent execution remains route-input based with dispatcher conversion isolated to the execution adapter.
- Retry, no-progress, provider repair, recovery decision, rework packet, and directive behavior remain in execution/provider services.
- Competing execution uses the route run snapshot and existing selection rules.
- Finalizer detail compatibility remains adapter-owned for direct-agent completion.
- No Process Core, production process-driver API, UI/media drift, or implementation stubs were introduced.

## Semantic Adequacy Gate

- Shallow-pass trap: execution DTO proof could pass while retry/provider/no-progress/finalizer paths silently drift or competing execution starts depending on dispatcher detail again.
- Adversarial negative proof: focused tests fail if retry/provider/no-progress, fallback ordering, competing execution selection, or finalizer context parity changes; architecture guard fails if forbidden adapter/detail/Core/driver leaks return.
- Semantic positive proof: build, SB021 architecture guard, execution parity integration tests, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`

## Reopen Triggers

- Reopen `SB021` if retry, provider repair, no-progress retry, competing execution, direct-agent finalizer context, execution snapshot, adapter confinement, or forbidden Core/driver/UI/stub scans fail.
