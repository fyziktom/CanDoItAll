# SB005 Proof Manifest

## Summary

- Subbundle: `SB005 - Route handler/service adapter confinement`
- Result: `Completed`
- Production source changed: `No additional production changes after SB004`
- Test source changed: `Yes`
- Browser validation: `N/A - runtime/service refactor only`
- Semantic invariant contract: `bundle://proof/SB005/semantic-invariants.md`

## Changed File Hashes

- `4c1c2c441527f2adbb15c9ebc8fd5d5b5c4c05484d92fe4ee6d8d98065014912` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Command Transcripts

- Architecture guard test: `bundle://proof/SB005/transcripts/route-adapter-confinement-architecture-test.txt`
- Source scans: `bundle://proof/SB005/transcripts/route-adapter-confinement-source-scans.txt`

## Source Assertions

- `ProcessDispatchRouteHandlers.cs` and `ProcessDispatchRouteServices.cs` contain no `ProcessDispatchRouteModelAdapters.ToDispatcher*` or `FromDispatcher*` calls.
- Route-facing files contain no route source payload interfaces or `.Source` route payload access.
- Adapter calls are confined to `ProcessDispatchRouteModelAdapters.cs`, route execution/hydration, and application-edge recovery/direct-agent/finalizer/guard services.

## Semantic Adequacy Gate

- Shallow-pass trap: route DTOs could be pure while route services still leak adapter conversions and dispatcher payload knowledge.
- Adversarial negative proof: `Process_core_pre_extraction_consolidation_SB005_INV_001_confines_route_adapters_to_application_edges` fails if route services/handlers regain adapter calls.
- Semantic positive proof: targeted architecture guard and source scans passed.
- Anti-stub audit: `bundle://proof/SB005/transcripts/route-adapter-confinement-source-scans.txt`

## Reopen Triggers

- Reopen `SB005` if route services or handlers call route model adapters, route-facing files regain source payload access, or adapter conversion spreads beyond named application-edge files.
