# Agent Recruiting Evidence

## Status

- `Completed`

## Objective

- Close N007 with canonical typed agent interview, attempt, review, and readiness evidence.

## Success Criteria

- A client can reconstruct approval/rejection from immutable challenge/rubric/run links.
- Repeated attempts compare over time and expose incomplete evidence.
- Automated evaluation and human authorization are distinct.
- Readiness never activates an agent and is false without qualifying human approval.

## Covered Inputs

- N007 / R007.

## Prerequisites

- SB03 structured-output evidence and SB05 stable workflow-run idempotency closed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Persistence`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\CrmHrApi.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.CrmHr\Recruiting\CrmHrRecruitingServices.cs`
- `C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Abstractions`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Contracts`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration`

## Deliverables

- Canonical agent-domain interview/evidence models and persistence service.
- POST interview/attempt/review and GET detail/readiness routes.
- Exact-one target discriminator and immutable hashes.
- automated evaluator and human reviewer provenance.
- optional CRM-HR projection/reference without duplicated canonical evidence.

## Dependency Impact

- Critical cross-module boundary; SB07 and all SharedInfo recruiting guidance depend on it.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation.

## Implementation Steps

1. Inventory existing CRM-HR interview, run identity, and prompt version fields.
2. Define primitive typed links in the agent-domain contract.
3. Implement durable service with visibility and evidence completeness checks.
4. Implement pure readiness projection with human gate.
5. Map dedicated endpoint family and typed responses.
6. Test repeat attempts, missing/cross-scope evidence, automated-only, rejection, and
   human-approved readiness.

## Scope Exceptions

- The API reports readiness but does not activate or deploy the agent.

## Do Not Do

- Do not copy mutable run output as untraceable prose.
- Do not make CRM-HR the canonical owner merely because it has existing interviews.
- Do not create cross-project runtime cycles.

## Acceptance Checklist

- [x] exactly one typed target per attempt
- [x] challenge/rubric/input/output versions/hashes retained
- [x] evaluator provider/model/agent retained
- [x] human review distinct
- [x] incomplete evidence explicit
- [x] readiness requires human authorization and never activates agent
- [x] workspace visibility enforced

## Proof Required

- Direct target/readiness policy tests.
- API integration positive and adversarial tests.
- Dependency/cycle proof and affected builds.

## Browser Validation Logging

- N/A.

## C# Architecture Impact

### Boundary Ownership

- Agent domain owns canonical evidence; CRM-HR is optional projection.

### Dependency Direction

- Typed primitive links avoid references to workflow/process/prompt implementations.

### Pattern Decision

- State projection for readiness and explicit discriminated target contract.

### Testability Contract

- Readiness projector and target validator are pure/directly testable.

### Partial Class Policy

- The service, pure validation helper, readiness projector, target resolver, models, store,
  and endpoint mapper are focused top-level types. No new partial class owns recruiting
  policy.

### Architecture Proof Required

- Before/after dependency/cycle result and direct service tests.

## Progression Gate

- All readiness/visibility negatives and positive reconstruction proof pass; architecture
  review unlocks SB07.

## Reopen Triggers

- Readiness can be set directly, target is untyped/ambiguous, or evidence cannot be
  reconstructed after reload.

## Implementation Evidence

- Canonical agent-domain contracts:
  `AgentRecruitingEvidenceModels.cs`.
- Core policy:
  `AgentRecruitingEvidenceService.cs`,
  `AgentRecruitingEvidenceValidation.cs`,
  `AgentRecruitingReadinessProjector.cs`, and
  `AgentConfigurationVersion.cs`.
- Durable append-only persistence:
  `FileSandboxWorkspaceStore.RecruitingEvidence.cs` under the current workspace/profile
  storage layout and cross-process lock.
- Runtime adapters and transport:
  `WorkspaceAgentRecruitingTargetResolver.cs` and `AgentRecruitingApi.cs`.
- Public operations:
  `POST /api/agent-recruiting/interviews`,
  `POST /api/agent-recruiting/interviews/{interviewId}/attempts`,
  `POST /api/agent-recruiting/interviews/{interviewId}/reviews`,
  `GET /api/agent-recruiting/interviews/{interviewId}`, and
  `GET /api/agent-recruiting/candidates/{agentId}/readiness`.
- CRM-HR recruiting models and services were not changed; they remain an optional
  projection/consumer rather than the canonical evidence owner.

## Validation Evidence

- AgentFramework Core build: 0 warnings, 0 errors.
- Web build: 0 errors; only the recorded 125 baseline NU1903 warnings.
- Direct policy/projector slice: 13/13 passed.
- Full-host API/OpenAPI slice: 5/5 passed. It covers all five operations, all three target
  kinds, JWT reviewer binding and spoof rejection, missing/mismatched/stale evidence,
  invalid hashes and rubric versions, nonqualifying approvals, workspace isolation,
  inherited authorization, typed response schemas, concurrent append-only persistence,
  and `ActivatesAgent=false`.
- Concrete target-resolver slice: 24/24 passed. It covers all 7 agent-execution states,
  all 7 workflow-run states, all 6 process-run states, missing targets for every
  discriminator, agent identity projection, and isolated backing stores.
- Final scoped CodeAnalytics snapshot:
  `snap-20260726043515-7a05e048` (5 projects, 379 documents, no blocking errors).
  The new service and validator have no findings/open questions, and the only module/type
  cycles retain the exact pre-existing node suffixes
  `efe376421f64`/`f602d7c77eb2` and
  `5374bd3c4751`/`a9e2e15d6c60`.

## Closure Decision

- N007 is solved. Canonical readiness is reconstructed from immutable execution evidence
  plus a qualifying human authorization; it cannot activate or deploy the candidate.
- SB06 is closed and SB07 is unlocked.
