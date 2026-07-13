# Semantic Invariants - SB02

## INV-SB02-01

- Invariant ID: `INV-SB02-01`
- Source raw note: F01/F02/F10 require exact process run and step diagnostics for blocked or failed operator action.
- Expected behavior: operator actions can use exact AgentFramework observations or runtime receipt diagnostics and never collapse to blind retry.
- Disallowed shallow implementation: increasing a recent-observation page size or keeping generic retry text.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs`, `repo://src/Processes/CanDoItAll.Processes.Application/ProcessBlockedStepPacket.cs`, `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`.
- Production assertions: blocked packet construction is isolated and projection tests verify branch repair and expired-claim operator text.
- Red-team negative case: no exact observation plus no runtime receipt produces a concrete missing-diagnostics packet, not a blind retry.
- Downstream dependency check: SB03/SB07/SB09 consume the blocked packet and exact diagnostic path.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessBlockedStepPacket` | packet builder source and process projection tests | operator action/rework projection | blocked step lifecycle | missing AgentFramework observation still produces concrete packet |
