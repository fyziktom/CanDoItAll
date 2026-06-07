# SB004 Proof Manifest

## Summary

- Subbundle: `SB004 - Split pure route DTOs from dispatcher source payloads`
- Result: `Completed`
- Production source changed: `Yes`
- Browser validation: `N/A - runtime/service refactor only`
- Semantic invariant contract: `bundle://proof/SB004/semantic-invariants.md`

## Changed File Hashes

- `4198628fb6cc1d135dbe0799210a51a9fcfa7518a3f1740eb61167ad702a4b66` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`
- `ca7891b140cbdba79358295343f3a9dce5a525ee39f64e83d4035eb732efe736` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `1300afce48eab647ab9b24402db9f9b9d0ac447368e6371b03e57502505b003d` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Build: `bundle://proof/SB004/transcripts/route-dto-split-build.txt`
- Architecture tests: `bundle://proof/SB004/transcripts/route-boundary-architecture-tests.txt`
- Focused integration tests: `bundle://proof/SB004/transcripts/route-dto-split-focused-integration-tests.txt`
- Source assertions: `bundle://proof/SB004/transcripts/route-dto-source-assertions.txt`

## Source Assertions

- `ProcessRouteCandidate`, `ProcessRouteDispatchClaim`, and `ProcessRouteExecutionOutcome` no longer expose source interfaces or `Source` constructor parameters.
- `ProcessDispatchRouteModelAdapters` owns dispatcher source payload sidecars through `ConditionalWeakTable` mappings and throws explicitly when a non-adapter-created DTO is converted back to dispatcher payload.
- Route services and route handlers contain no `ProcessDispatchRouteModelAdapters.ToDispatcher*` or `FromDispatcher*` calls.

## Semantic Adequacy Gate

- Shallow-pass trap: deleting `Source` from DTOs without preserving dispatcher payload recovery would compile only if finalizer, recovery, direct-agent, or guard paths were weakened or bypassed.
- Adversarial negative proof: focused finalizer adapter and route reload tests exercise adapter-created DTOs through dispatcher-only edges after the source split.
- Semantic positive proof: build, architecture tests, focused route/finalizer integration tests, and source assertions all passed.
- Anti-stub audit: `bundle://proof/SB004/transcripts/route-dto-source-assertions.txt`

## Reopen Triggers

- Reopen `SB004` if route DTOs regain source interfaces, route services/handlers call dispatcher adapters directly, adapter-created route DTOs fail finalizer/recovery/direct-agent conversion, or source scans find Core/driver/UI drift.
