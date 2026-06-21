# SB31 Project-Structure Launch Staffing Readiness And Runtime Sequence Repair

## Status

- Completed on 2026-06-17

## Objective

Repair the project-structure process launch path so a reviewed launch can start reliably and so HR/agent matching cannot bind a role to an agent that lacks the role family, workspace rights, or required process-operation tool readiness.

## Source Inputs

- User report on 2026-06-17: project-structure process launch selected Delivery Manager for most roles, manual role changes were needed, and start failed with PostgreSQL `23505` on `PK_process_runtime_events` for `GlobalSequence=1`.
- Attached console log: `bundle://inputs/project-structure-launch-runtime-sequence-and-staffing-log.txt`.
- Existing architecture rule: `bundle://architecture/20-role-candidate-selection-and-readiness.md`.
- Existing readiness validation rule: `bundle://validation/06-role-candidate-readiness-validation.md`.
- Existing SB21 and SB27 roadmap packages, which were marked blocked but partially covered by the combined SB20-SB28 runtime repair.

## Covered Inputs

- User-attached duplicate key log and stack trace.
- Existing project-structure launch UI/API behavior.
- Existing process launch resolver and step operation contract models.
- Existing .NET architecture and development subprocess templates.

## Prerequisites

- SB20-SB28 runtime completion repair is present.
- Process runtime persistence and launch-plan APIs are active.
- Agent workspace access metadata profiles are available.
- TetrisGame project structure exists in the dev database.

## Exact Source References

- `repo://src/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/CanDoItAll.Processes.Application/ProcessLaunchContracts.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- `repo://Templates/Processes/processes/dotnet-architecture-design-review/definition.json`
- `repo://Templates/Processes/processes/dotnet-architecture-design-review/steps/classify-dotnet-application.md`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessLaunchExecutorResolverTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`

## Dependency Impact

- Process Application contracts now expose step operation metadata and executor overrides to the resolver.
- Process module resolver depends on the shared AgentFramework readiness evaluator.
- Workbench project-structure launch UI uses the same readiness evaluator for candidate presentation.
- PostgreSQL migrations include an operation-only identity-sequence repair.

## Validation Depth

- Unit tests cover automatic role/tool matching and invalid manual override rejection.
- Component tests cover assignment dialog rendering/selection states.
- Integration tests cover project-structure launch context, HR provider filtering, scaffold-contract propagation, and subprocess prompt generation.
- Live dev-DB smoke covers the reported start path and TetrisGame launch readiness.

## Implementation Steps

- Add shared role/tool readiness evaluator.
- Pass executor overrides and operation contracts through launch-plan creation and resolver execution.
- Remove manual override bypass from launch assignment creation.
- Update Workbench launch candidate scoring to use readiness.
- Add PostgreSQL `GlobalSequence` identity-sequence repair migration.
- Expand .NET launch-variable contributor to architecture/development subprocesses.
- Update greenfield architecture classification rules and tests.
- Record proof and bundle validation.

## Do Not Do

- Do not silently fall back to a weakly matched agent when readiness fails.
- Do not let manual UI overrides bypass resolver readiness.
- Do not make architecture classification mutate product files.
- Do not treat an empty grounded greenfield output root as existing repository corruption.

## Browser Validation Logging

- Browser validation was not required for visual regressions. Runtime validation used direct HTTP API smoke against `http://localhost:5032` and saved the JSON launch response.

## Suggested Agent Prompt

Implement SB31 by repairing project-structure Process launch staffing and runtime event sequence behavior. Keep changes scoped to launch contracts, resolver readiness, project-structure candidate presentation, PostgreSQL sequence repair, and greenfield .NET subprocess classification. Validate with focused unit/component/integration tests plus a bounded TetrisGame launch smoke.

## Scope

- Add database migration repair for stale PostgreSQL identity sequence state on `process_runtime_events.GlobalSequence`.
- Route project-structure manual executor overrides through the same deterministic launch resolver as automatic recommendations.
- Derive required workspace readiness from process step allowed operations and target scope.
- Rank project-structure assignment candidates using role family plus readiness instead of a base score that can make Delivery Manager appear suitable for .NET developer roles.
- Add focused tests for manual override rejection and accepted suitable role/tool matches.

## Out Of Scope

- Full SB21 approval/provisioning UI.
- New HR/CRM data model.
- Broad redesign of Process launch plan projections.
- Browser proof unless the focused backend/component tests expose a UI regression requiring visual inspection.

## Acceptance Checklist

- [x] Existing runtime-event rows cannot leave the PostgreSQL identity sequence behind the maximum `GlobalSequence` after migration.
- [x] Automatic process launch matching prefers role-family-correct agents with enabled structured-output providers.
- [x] Manual project-structure executor overrides are rejected when the selected agent lacks the required role family or workspace operation rights.
- [x] Project-structure assignment candidates mark unready agents as unresolved/provisioning candidates instead of silently selectable resources.
- [x] Greenfield project-structure .NET targets carry scaffold contracts into architecture/development subprocess prompts instead of blocking only because the output root is empty.
- [x] Focused unit/component/integration validation passes.

## Proof Required

- `proof/SB31-project-structure-launch-staffing-readiness-and-runtime-sequence-repair/manifest.md`
- `proof/SB31-project-structure-launch-staffing-readiness-and-runtime-sequence-repair/semantic-invariants.md`
- `proof/SB31-project-structure-launch-staffing-readiness-and-runtime-sequence-repair/changed-file-hashes.txt`
- Focused test transcripts under `proof/SB31-project-structure-launch-staffing-readiness-and-runtime-sequence-repair/transcripts/`.
- Live dev-DB Tetris launch smoke: `proof/SB31-project-structure-launch-staffing-readiness-and-runtime-sequence-repair/runtime-smoke-tetris-start-response.json`.

## Progression Gate

- Future full SB21 implementation can build approval/provisioning workflows on top of these deterministic readiness findings, but must not weaken the launch blocker semantics repaired here.
