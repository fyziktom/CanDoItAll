# Current Role Candidate Selection Gap

## Purpose

This analysis records the current Process launch candidate behavior and the gap that must be corrected in the rewrite.

## Current Source Evidence

Current candidate selection and scoring are implemented mainly in:

- `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.CandidateDiscovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Staffing.cs`
- `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Planning.cs`
- `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Approval.cs`
- `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Provisioning.cs`
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsLaunchSection.razor`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessLaunchPlanningIntegrationTests.cs`

Related capability/right evidence exists in AgentFramework tests:

- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`

## Current Behavior

Current launch planning already supports:

- role candidate sources: project assignments, AI directory resources, workforce/HR search, workflow catalog, new AI agent proposal, and gap candidates;
- candidate kind tracking through `ProcessLaunchCandidateKind`;
- `Score`, `IsRecommended`, `RequiresProvisioning`, `RecommendationSummary`, and `AvailabilitySummary`;
- HR-manager scoring via `ScoreCandidateForHrManager` and supplemental candidate generation;
- project assignment preference and agent-team fit adjustments;
- provisioning requests for selected candidates that require provisioning;
- launch approval blocking when required roles do not select a resolvable candidate;
- run start assignment from selected launch candidates.

## Gap

The current model treats readiness mostly as text summaries and a broad `RequiresProvisioning` flag. It does not provide a typed readiness assessment that lists the exact missing prerequisites for a role candidate.

The missing information includes:

- required tools,
- missing tool-provider bindings,
- missing rights or permissions,
- missing project/workspace/file access,
- missing approval rights,
- missing direct-message permission,
- missing workflow execution rights,
- missing provider profile or model binding,
- missing domain-driver capability,
- retired or unverified capability,
- candidate availability risk,
- security/sensitivity restrictions caused by artifact or target scope,
- who can resolve each missing item,
- whether the missing item can be auto-provisioned,
- whether the missing item blocks launch approval or only blocks execution.

## Architectural Risk

A score-only system can select a candidate that looks suitable but cannot actually perform the role. This creates late runtime failures, unclear provisioning requests, and poor user feedback. The new Process architecture must split candidate suitability from execution readiness.

## Required Rewrite Direction

The rewrite must produce a deterministic `CandidateReadinessAssessment` for every candidate considered for a role. HR scoring may propose or rank candidates, but it cannot mark a candidate executable without readiness evidence.

Candidate selection must be based on:

- suitability score,
- readiness status,
- blocking findings,
- warning findings,
- provisioning plan,
- approval plan,
- source/provenance of the recommendation,
- audit decision for overrides.

The UI must expose this as a candidate matrix that shows both the score and the readiness gaps.
