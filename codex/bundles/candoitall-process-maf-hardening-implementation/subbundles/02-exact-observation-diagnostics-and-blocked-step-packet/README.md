# SB02 - Exact Observation Diagnostics And Blocked Step Packet

## Status

- `Completed`
- Critical foundation: yes

## Objective

Fix blocked operator diagnostics and rework packet quality so a blocked step can be diagnosed by exact process run id and step instance id, with runtime receipt fallback when AgentFramework observation is unavailable.

## Covered Inputs

- F01, F02, F10.
- R01, R02, R03.
- GPTPro B01.

## Prerequisites

- SB01 inventory and current test map complete.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorActionDiagnostics.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`

## Deliverables

- Step-level selector support in process observation query.
- Observation reader uses exact `ExecutionRunQuery.ProcessStepId` for operator action diagnostics.
- Run-level fallback remains only for live dashboard enrichment.
- `ProcessBlockedStepPacket` or equivalent typed packet.
- Packet builder that consumes observation, receipt, assignment, artifact descriptors when available, and step state.
- Operator/rework text uses packet and does not recommend blind retry when diagnostics are missing.

## Dependency Impact

- SB03, SB07, and SB09 consume this diagnostic packet. Weak proof here invalidates later preflight and bridge diagnostics.

## Validation Depth

- Critical foundation.
- Requires failing-first and passing tests.

## Implementation Steps

1. Add a strongly typed step selector to `ProcessExecutionObservationQuery` or an equivalent selector record.
2. Update all call sites with backward-compatible defaults.
3. Change operator action projection path to request exact run/step observations.
4. Keep existing run-level query for dashboard lists where exact step ids are not requested.
5. Add focused blocked packet records and builder.
6. Build packet categories for missing AF observation, missing output artifact, missing input artifact, child active, child no-go, required tool missing/denied, finalization downgrade, and unknown diagnostic.
7. Update rework instruction generation to include packet details and stop blind retry wording.
8. Add direct unit tests for query selection and packet categories.

## Scope Exceptions

- Do not persist AgentFramework structured summaries in this phase; SB03 owns persistence.
- Do not add artifact descriptor rendering beyond minimal packet placeholders; SB06 owns descriptor richness.

## Do Not Do

- Do not put all packet logic inside `ProcessRuntimeProjectionQueryService`.
- Do not treat missing diagnostics as approval for blind retry.
- Do not use string-only blocker categories when typed enum/record is feasible.

## Acceptance Checklist

- [ ] Exact step observation can find a blocked run hidden by run-level `TakePerRun`.
- [ ] Missing AgentFramework observation uses runtime receipt diagnostics.
- [ ] Operator action names run id, step id, step key, outcome, diagnostic category, and next action.
- [ ] Rework prompt includes blocked packet and forbids blind retry without concrete diagnostic.
- [ ] Existing dashboard behavior remains compatible.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- Failing-first transcript for observation truncation.
- Passing transcript for exact step query.
- Passing transcript for packet fallback tests.
- Source assertions that packet logic lives outside `ProcessRuntimeProjectionQueryService`.
- Changed-file hashes.
- Anti-stub audit.
- Production Behavior Artifact Matrix for `ProcessBlockedStepPacket` and new diagnostic categories.

## Browser Validation Logging

- `N/A` for backend/projection contract tests unless Blazor rendering changes.
- If UI rendering changes, add maximized browser proof for blocked operator action text.

## Progression Gate

- SB03 and SB07 may start only after exact observation lookup and blocked packet fallback tests pass.

## C# Architecture Impact

Extracts projection/rework diagnostic responsibility from a large projection service.

## Boundary Ownership

Packet builder belongs in application/projection boundary; AgentFramework reader remains module integration.

## Dependency Direction

Projection contracts must not depend on module concrete types. Module reader may depend on AgentFramework execution query.

## Pattern Decision

Use Builder for packet construction. Reject partial-class expansion.

## Testability Contract

Unit tests instantiate packet builder directly with fake receipt/observation data.

## Partial Class Policy

No new projection partial as final boundary. Any partial edit must delegate to focused builder.

## Architecture Proof Required

- Source assertion of extracted builder.
- Direct tests for builder.
- CodeAnalytics or source evidence that dependencies remain acyclic.

## Suggested Agent Prompt

```text
Execute SB02 only. Implement exact observation selectors and blocked-step packet diagnostics. Add failing-first and passing tests. Do not touch subprocess bridge, template contracts, or artifact materialization except for minimal data needed by packet placeholders.
```
