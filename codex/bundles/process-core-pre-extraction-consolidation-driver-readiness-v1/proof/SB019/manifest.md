# SB019 Proof Manifest

## Summary

- Subbundle: `SB019 - Direct-agent execution input/output DTO hardening`
- Result: `Completed`
- Production source changed: `No - existing branch implementation already satisfied the DTO boundary`
- Owned requirements: direct-agent runtime boundary uses route-owned input/output DTOs; dispatcher payload recovery is confined to one adapter edge.
- Semantic invariant contract: `bundle://proof/SB019/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `ec3ae537d40ce5b798e0a194377c2358c07c5d3f3db5ac9ef630367c836bb958` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionModels.cs`
- `14edc825a9b8e78429ec49f60c551c53a1e1ebddc575552069daca17d1407b91` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`
- `7931a4d3284190a01b65ef3434a28755dbeb9b43dba5767390ff41719b642072` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionAdapter.cs`
- `3fdbf5714ed7dc824d01f4c1c7df67395e1ba5e98edfad2704c3dbfccaf64fb9` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`
- `7f8f3ef2b89128bebaa1b1263154054385531fa15299f7abf8fab32aac44fc7c` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `57e053ff4e04449e5370efd706da2a63de6884326584a346be019902f2c43bf7` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `a77ed0a2f5314c1eed678b91159a5af0242fb47f3ce31645784ab62cf9f2624b` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Build: `bundle://proof/SB019/transcripts/direct-agent-execution-dto-build.txt`
- Architecture test: `bundle://proof/SB019/transcripts/direct-agent-execution-dto-architecture-test.txt`
- Focused integration tests: `bundle://proof/SB019/transcripts/direct-agent-execution-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB019/transcripts/direct-agent-execution-dto-source-assertions.txt`

## Source-Level Assertions

- Direct-agent execution input carries route candidate, trigger, and renew-lease delegate.
- Runtime and route services consume route-owned execution input without dispatcher aliases or route adapter calls.
- `ProcessDispatchDirectAgentExecutionAdapter` is the one edge converting route candidate and execution outcome to/from dispatcher payloads.

## Semantic Adequacy Gate

- Shallow-pass trap: adding an input DTO would not be enough if runtime still accepted dispatcher candidates or route adapters leaked outside the adapter.
- Adversarial negative proof: the architecture guard fails if direct-agent runtime regains dispatcher aliases, adapter calls, or full dispatcher payloads.
- Semantic positive proof: build, architecture guard, route pipeline/direct-agent/finalizer focused tests, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB019/transcripts/direct-agent-execution-dto-source-assertions.txt`

## Reopen Triggers

- Reopen `SB019` if direct-agent runtime regains dispatcher aliases, route adapters leave the adapter edge, route execution outcomes stop flowing through route DTOs, or forbidden Core/driver/UI/stub scans fail.
