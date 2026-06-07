# Assumptions, Risks, And Reopen Triggers

## Assumptions

- The branch is `maf-processes-refactor`.
- Runtime behavior is already covered by the existing focused integration tests.
- Full unfiltered integration tests may be long-running; focused integration closure is acceptable only when the moved behavior is covered.
- No UI work is expected.

## Critical Path Risks

- Source-payload removal may accidentally break finalizer compatibility.
- Hydration split may lose direct-agent binding defaults, recovery execution ids, or cooperation metadata.
- Subprocess projection split may change child artifact selection or parent artifact lineage.
- Artifact pure-rule extraction may accidentally move filesystem/storage/database behavior into a pure candidate.
- Driver-readiness vocabulary may accidentally become a production API.

## Validation Risks

- Source scans must not be too narrow. They must scan the whole process module for forbidden Core/driver tokens.
- Tests must include both positive and negative route/finalizer/subprocess/projection cases.
- Documentation-only driver work must be validated as documentation-only.

## Reopen Triggers

Reopen the relevant subbundle if any of these happen:

- Any `CanDoItAll.Processes.Core` or `CanDoItAll.Modules.Processes.Core` project appears.
- Any production `IProcessDriverPack`, registry, manager tool, or DI registration appears.
- Route order changes.
- `ProcessRouteCandidate.Source`, `ProcessRouteDispatchClaim.Source`, or `ProcessRouteExecutionOutcome.Source` remains in route-facing pure DTOs without an explicit adapter-edge exception.
- Finalizer null-result starts applying transitions.
- Subprocess projection writes move without parity proof.
- Any UI/media/mobile proof appears for this runtime-only bundle.

