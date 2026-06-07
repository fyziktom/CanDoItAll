# SB008 Proof Manifest

## Summary

- Subbundle: `SB008 - Constrain finalizer adapter to application edge`
- Result: `Completed`
- Production source changed: `Yes`
- Browser validation: `N/A - runtime/service refactor only`
- Semantic invariant contract: `bundle://proof/SB008/semantic-invariants.md`

## Changed File Hashes

- `ef2cdb38adbf5fd739ad806ec3a282dcca89b9f7c46109a9c5abb4bd8470a609` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `481ccfc9742b9fa57bd8d664dc717e56fe37bb4f02b84e9a8a78cb820fd3af13` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `a77ed0a2f5314c1eed678b91159a5af0242fb47f3ce31645784ab62cf9f2624b` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Command Transcripts

- Build: `bundle://proof/SB008/transcripts/finalizer-adapter-confinement-build.txt`
- Architecture tests: `bundle://proof/SB008/transcripts/finalizer-adapter-confinement-tests.txt`
- Focused integration tests: `bundle://proof/SB008/transcripts/finalizer-adapter-focused-integration-tests.txt`
- Source assertions: `bundle://proof/SB008/transcripts/finalizer-adapter-source-assertions.txt`

## Source Assertions

- `ProcessDispatchFinalizerAdapter` exposes input-based methods only; duplicate public dispatcher-alias overloads are removed.
- Legacy application-edge callers now create `ProcessDispatch*FinalizerInput` records before calling the adapter.
- `ProcessDispatchFinalizerApplicationService` remains free of dispatcher aliases and route adapter calls.

## Semantic Adequacy Gate

- Shallow-pass trap: making alias overloads private or leaving legacy direct callers in place would keep the dispatcher boundary ambiguous.
- Adversarial negative proof: the architecture test fails if public dispatcher-alias overloads return or finalizer application service regains dispatcher alias knowledge.
- Semantic positive proof: build, architecture tests, and focused finalizer integration tests passed.
- Anti-stub audit: `bundle://proof/SB008/transcripts/finalizer-adapter-source-assertions.txt`

## Reopen Triggers

- Reopen `SB008` if public dispatcher-alias finalizer overloads return, finalizer application service calls route adapters, legacy partials bypass finalizer input records, or no-Core/no-driver/no-UI scans fail.
