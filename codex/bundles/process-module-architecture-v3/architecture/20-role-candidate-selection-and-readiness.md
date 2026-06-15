# Role Candidate Selection And Readiness

## Design Intent

Process launch candidate selection must be explainable and executable. A high suitability score is not enough. The system must separately judge whether the candidate has the required tools, rights, permissions, approvals, bindings, workflow access, project access, and provider/profile readiness to perform the role.

The HR agent may recommend candidates and provide scoring input, but final readiness is produced by deterministic application/domain services using typed requirements and evidence.

## Separation Of Concerns

| Concern | Responsibility | Notes |
| --- | --- | --- |
| Candidate discovery | Finds possible people, AI agents, workflows, existing assignments, and provisioning proposals. | HR agent and directories may contribute candidates. |
| Suitability scoring | Ranks how well the candidate appears to match role purpose, skills, availability, team scope, and executor preference. | Score is advisory and explainable; it cannot override required readiness blockers. |
| Readiness assessment | Determines whether the candidate can actually execute the role now. | Deterministic and typed; based on required tools, rights, access, approvals, bindings, and capabilities. |
| Provisioning planning | Converts missing but provisionable items into durable provisioning tasks. | Each task is linked to a readiness finding. |
| Approval planning | Converts rights/override-sensitive findings into approval tasks. | Approval is explicit and auditable. |
| Launch gate | Decides whether the launch plan may move to approval or execution. | Required blockers must be resolved or explicitly overridden by policy. |
| UI projection | Shows score, readiness, missing items, proposed fixes, and blocker/warning state. | UI must not infer readiness from score text. |

## Role Execution Requirement Model

Each role receives an aggregated execution requirement envelope before candidate evaluation:

| Requirement family | Examples | Source |
| --- | --- | --- |
| Skills/capabilities | Domain skills, tool capabilities, workflow capabilities, driver-required capabilities. | Role definitions, templates, driver capability descriptors, HR/agent directories. |
| Tools | Browser tools, Git wrapper operations, project/workspace tools, process tools, file/artifact tools, workflow tools. | Step operation contracts, execution strategies, drivers, templates. |
| Rights/permissions | Read project structure, mutate product target, write artifacts, run validation, launch runtime, capture proof, approve tool calls, send direct messages. | Step operation contracts, manager policy, artifact policy, access policy. |
| Access scopes | Project, repository, workspace, filesystem root, artifact destination, external service, workflow definition, provider profile. | Launch context, project scope, target scope, artifact sensitivity, adapters. |
| Approvals | Human approval, manager approval, security approval, provisioning approval, explicit role approval. | Role flags, process criticality, autonomy level, policy. |
| Availability | Person availability, agent provider health, workflow active status, model/profile bound status, team membership. | CRM-HR, AgentFramework, workflow catalog, provider registry. |
| Compliance | Sensitivity clearance, trust requirement, retention policy, domain governance, unauthorized mutation audit. | Artifact expectations, governance policy, Git wrapper, security policy. |

The envelope is represented as `RoleExecutionRequirementSet` in the target architecture. It is built from definition roles, assigned steps, operation contracts, artifact expectations, process criticality, selected operating mode, driver capability descriptors, and project/run context.

## Candidate Readiness Assessment Model

Each candidate receives a persisted assessment snapshot for the launch plan:

```text
CandidateReadinessAssessment
  CandidateId
  LaunchPlanRoleId
  AssessedAtUtc
  AssessedBy
  RequirementSetHash
  EvidenceSnapshotHash
  SuitabilityScore
  SuitabilityScoreBreakdown[]
  ReadinessStatus
  BlockingFindingCount
  WarningFindingCount
  ProvisioningTaskCount
  ApprovalTaskCount
  Findings[]
```

`ReadinessStatus` values:

- `Ready`
- `ReadyWithWarnings`
- `ProvisioningRequired`
- `ApprovalRequired`
- `MissingRequiredTool`
- `MissingRequiredRight`
- `MissingRequiredCapability`
- `Unavailable`
- `IncompatibleExecutor`
- `Blocked`
- `Gap`

The selected candidate for a required role may only be considered executable when readiness is `Ready` or `ReadyWithWarnings`, or when a policy-approved override exists for a non-safety-critical blocker. Missing required tools and missing required rights are blocking by default.

## Readiness Finding Model

Every missing item is a typed finding:

```text
CandidateReadinessFinding
  FindingId
  CandidateId
  RequirementId
  Code
  Severity
  RequirementKind
  RequiredKey
  RequiredDisplayName
  CurrentState
  UserSafeSummary
  RestrictedDetailRef
  CanAutoProvision
  RequiresApproval
  SuggestedResolutionAction
  ResolutionOwnerKind
  ResolutionOwnerRef
  BlocksLaunchApproval
  BlocksLaunchExecution
```

Finding codes:

- `MissingRequiredTool`
- `MissingRequiredRight`
- `MissingRequiredCapability`
- `MissingProviderProfile`
- `MissingWorkflowExecutionBinding`
- `MissingProjectAccess`
- `MissingArtifactAccess`
- `MissingRepositoryAccess`
- `MissingDirectMessagingPermission`
- `MissingApprovalAuthority`
- `CapabilityRetired`
- `CapabilityUnverified`
- `ToolProviderUnavailable`
- `WorkflowInactive`
- `ProviderUnavailable`
- `InsufficientAvailability`
- `ExecutorKindMismatch`
- `OutsideSelectedTeam`
- `SensitivityPolicyMismatch`
- `ManualReviewRequired`

Finding severities:

- `Blocking`
- `Warning`
- `Info`

Resolution actions:

- `GrantRight`
- `AttachTool`
- `BindProviderProfile`
- `ActivateWorkflow`
- `ProvisionAgent`
- `AddProjectAssignment`
- `RequestApproval`
- `SelectDifferentCandidate`
- `CreateWorkflowCandidate`
- `ManualReview`
- `OverrideWithDecisionRecord`

## Score Model

The score is retained but becomes explainable and subordinate to readiness.

`CandidateSuitabilityScoreBreakdown` includes:

- source base score,
- skill/capability fit,
- role-title/context fit,
- executor-kind fit,
- team/project fit,
- availability fit,
- workflow fit,
- tool/right readiness contribution,
- provisioning penalty,
- policy risk penalty,
- HR agent recommendation contribution,
- deterministic evaluator adjustments.

Rules:

- A high score cannot hide blocking readiness findings.
- HR agent score contribution is advisory.
- Deterministic readiness findings may add penalties, but the blocking state is represented by `ReadinessStatus` and findings, not by score alone.
- The score explanation must be user-safe; sensitive rights/tool details go behind restricted evidence links.

## HR Agent Boundary

The HR agent may:

- search candidate directories,
- propose candidates,
- rank candidates,
- summarize why a candidate may fit,
- identify likely missing provisioning from available directory data.

The HR agent may not:

- grant rights,
- mark a missing required tool as satisfied,
- mark a missing required right as satisfied,
- suppress readiness blockers,
- override policy,
- convert a gap into ready state without deterministic evidence.

The application service re-evaluates every HR-proposed candidate through `IRoleCandidateReadinessEvaluator` before storing or selecting it.

## Evaluator Interfaces

Target contracts:

```text
IRoleExecutionRequirementCompiler
  CompileAsync(definition, launchContext, role, assignedSteps, cancellationToken)

IRoleCandidateDiscoveryService
  DiscoverAsync(requirementSet, launchContext, cancellationToken)

IRoleCandidateSuitabilityScorer
  Score(candidate, requirementSet, evidenceSnapshot)

IRoleCandidateReadinessEvaluator
  EvaluateAsync(candidate, requirementSet, evidenceSnapshot, cancellationToken)

IRoleCandidateProvisioningPlanner
  PlanAsync(candidate, readinessAssessment, cancellationToken)

IRoleCandidateApprovalPlanner
  PlanAsync(candidate, readinessAssessment, cancellationToken)
```

Generic Process contracts define requirement and finding shapes. Domain drivers provide domain-specific requirement descriptors and evaluators behind generic interfaces.

## Launch Gate Rules

| Gate | Required condition |
| --- | --- |
| Candidate may be displayed | Candidate discovery succeeded or a gap candidate was produced. |
| Candidate may be selected | Candidate exists and assessment exists. Blocking findings may still allow selection only when UI clearly shows that selection is not executable yet. |
| Launch may move to approval | Required roles have selected candidates and no `BlocksLaunchApproval` findings remain unless policy allows approval with outstanding provisioning. |
| Launch may provision | Each provisionable finding has a durable provisioning task. |
| Launch may execute | Required roles have selected candidates with `Ready`/`ReadyWithWarnings` assessment or approved override; all blocking execution findings resolved. |
| Runtime may start role assignment | Assignment snapshot includes candidate id, requirement hash, readiness assessment hash, granted rights/tool evidence refs, and unresolved warning list. |

## Provisioning And Reassessment

Provisioning is itemized. A single selected candidate may create multiple provisioning tasks:

- attach missing tool,
- grant missing right,
- bind provider profile,
- activate workflow,
- add project assignment,
- create AI resource,
- request human approval,
- update direct messaging permission.

When provisioning completes, the system re-runs readiness assessment against a fresh evidence snapshot. It does not assume provisioning succeeded because a task status changed.

## UI Projection Requirements

The launch candidate matrix must show:

- score and score explanation,
- readiness status badge,
- blocking finding count,
- warning finding count,
- missing tools,
- missing rights,
- missing capabilities,
- provisioning tasks,
- approval tasks,
- selected candidate readiness,
- whether approval/execution is blocked,
- suggested resolution actions,
- restricted-detail links for sensitive findings.

Candidate rows must be expandable or otherwise inspectable without overloading the list. The selected role card must summarize the selected candidate readiness and name the first blocking item.

## Security And Redaction

Missing-right/tool findings may reveal sensitive access structure. UI projections must contain user-safe summaries and authorization flags. Raw policy evidence and restricted details are exposed only through restricted evidence links.

Logs must include actionable state while masking sensitive identifiers where needed.

## Persistence And Audit

Persist:

- role execution requirement snapshot,
- candidate assessment snapshot,
- readiness findings,
- provisioning tasks linked to findings,
- approval tasks linked to findings,
- candidate selection decision,
- override decision records,
- evidence snapshot hashes.

The selected candidate assignment copied into runtime must include readiness assessment hash and unresolved warning list so later runtime failures can be traced back to launch readiness.

## Tests Required

- High-scoring HR candidate with missing required tool is visible but not executable.
- Candidate missing required right blocks launch execution and shows a user-safe missing-right finding.
- Missing optional capability produces warning, not blocker.
- Provisioning task resolves missing tool only after reassessment proves the tool exists.
- HR agent recommendation cannot suppress deterministic missing-right finding.
- Manual override is blocked for safety-critical rights unless policy explicitly allows it.
- Selected candidate assignment includes readiness assessment hash.
- UI candidate matrix shows score and readiness separately.
- Sensitive missing-right details are redacted for unauthorized users.
- Batch candidate evaluation does not perform per-candidate directory/tool-provider calls when shared evidence can be loaded once.
