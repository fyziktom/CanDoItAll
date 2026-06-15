# User Story Coverage Model

## Intent

The user-story map is a control plane for the rewrite. It prevents the architecture from becoming internally clean while accidentally dropping current user-facing behavior. The rewrite may change implementation details, data models, and runtime semantics, but each current story must remain traceable to a target model, command/query contract, projection, and validation proof.

## Coverage Planes

| Plane | Responsibility | Story coverage use |
| --- | --- | --- |
| Generic Core | Domain-neutral definitions, roles, steps, branches, artifact slots, policies, and typed IDs. | Owns structural stories such as definitions, roles, steps, branches, artifacts, and subprocess references without domain vocabulary. |
| Template/Git Source | JSON source-of-truth, component references, overrides, migrations, Git history, conflicts. | Owns template catalog, selective import, global/local customization, migration, diff/conflict stories. |
| Builder/Plan Compiler | Converts definitions/templates/context into immutable instance plans with selected drivers and strategies. | Owns launch-readiness, subprocess composition, branch route tables, artifact plans, and strategy binding stories. |
| Runtime/Scheduler/Dispatcher | Applies state transitions, claims work, invokes assigned strategies, persists events. | Owns run lifecycle, step execution, leases, dead letters, cancellation, idempotency, and runtime control stories. |
| Manager/Recovery | Converts faults and diagnostics into incidents, recovery/resupply requests, escalations, and parent/child messages. | Owns operator control, recovery, rework, approvals, escalations, manager chat, and subprocess communication stories. |
| Drivers/Strategies | Domain-specific execution, recovery, branch decision, manager behavior, and artifact policies behind generic interfaces. | Owns domain-specific behavior while keeping the core clean. |
| Persistence/Event/Outbox/Ledger | Durable state, events, artifact ledger, outbox, projection storage, offsets, dead letters. | Owns observability, artifact availability, history, retry, and crash/recovery stories. |
| Projections/UI Query Services | UI-friendly read models, snapshot freshness, history filters, canvas/read-only projections. | Owns workspace, live dashboard, run history, analytics, graphs, and browser-facing stories. |
| Application Commands | Authorized actions from UI/tools/agents into the runtime and manager. | Owns save/publish/import/export, launch, operator action, assignment, messaging, approval, rework, and tool stories. |

## Story Coverage Contract

For each user story, the implementation must record:

- `StoryId`: stable `US-###` identifier from `analysis/06-current-implementation-user-story-map.md`.
- `CurrentEvidence`: source/test/UI evidence that proves the story exists today.
- `TargetOwner`: subbundle and project that owns the future implementation.
- `ModelCoverage`: domain model, projection, command, event, policy, or strategy type that represents the story.
- `ValidationCoverage`: unit, integration, component, Playwright, migration, or scan proof.
- `DeltaDecision`: preserve, improve, replace, or remove-with-approval.
- `Risk`: missing projection fields, hidden runtime coupling, domain vocabulary leak, security exposure, or browser regression.

## Generic Boundary Rule

Stories that come from software-development templates, Blazor workflows, runtime command writeback, screenshot capture, or Git auditing must not cause those words to appear in generic core/runtime contracts. They must be represented through:

- driver capability descriptors,
- opaque domain tags,
- strategy factories,
- template fragments,
- adapter envelopes,
- restricted diagnostics,
- projection metadata.

The generic layers may know that a step has an execution strategy, a branch input, an artifact dependency, a manager policy, or an external target operation. They may not know that the domain is Blazor, Office, marketing, .NET, screenshots, or any other specific domain.

## UI Story Preservation Rule

Browser-facing stories must be validated at the subbundle that implements them, not deferred to the final E2E subbundle. Required proof for browser-facing stories:

- Playwright MCP route, viewport, actions, assertions, screenshot path, and accessibility snapshot where useful.
- Component test or bUnit-style proof for projection-only rendering and command dispatch.
- Dependency scan proving UI does not reference runtime internals or EF runtime entities.
- Freshness/lag display validation for live/history and projection-backed surfaces.
- Console/network issue review for critical routes.

The final subbundle repeats critical journeys as regression proof, but it must not be the first proof that a browser-facing story works.

## Story-To-Model Mapping

| Story family | Stories | Required target models/contracts |
| --- | --- | --- |
| Workspace shell and catalog | US-001 to US-004 | `ProcessWorkspaceProjection`, `ProcessDefinitionCatalogProjection`, `DefinitionSummaryProjection`, `TemplateFeedCommand`, `ProjectionRefreshToken`. |
| Definition authoring | US-005 to US-008 | `ProcessDefinitionDraft`, `DefinitionGovernancePolicy`, `DefinitionContractSummary`, `DefinitionLintResult`, publication commands. |
| Role authoring | US-009 to US-010, US-016 | `ProcessRoleDefinition`, `ExecutorPreference`, `RoleTemplateReference`, `RoleOverride`, `StepRoleAssignment`. |
| Step and branch authoring | US-011 to US-015, US-018 to US-019 | `ProcessStepDefinition`, `StepOperationContract`, `BranchDefinition`, `BranchOutcome`, `RouteTarget`, `LoopBudget`, `DefinitionCanvasProjection`. |
| Artifact authoring and evidence | US-017, US-040, US-053 | `ArtifactSlot`, `ArtifactExpectation`, `ArtifactInstance`, `ArtifactLedgerEntry`, `ArtifactValidationStatus`, `ArtifactRecoveryRequest`. |
| Template/Git/Exchange | US-021 to US-025, US-051 | `TemplateCatalogProjection`, `TemplateComponentReference`, `TemplateOverrideRecord`, `TemplateConflictRecord`, `GitChangeProjection`, `ProcessExchangeEnvelope`. |
| Launch and staffing | US-026 to US-029, US-041 | `LaunchPlan`, `RoleDemand`, `CandidateMatch`, `ProvisioningRequest`, `ApprovalRecord`, `AssignmentResolution`. |
| Runtime and run history | US-030 to US-035, US-054 | `RunHistoryProjection`, `RunDetailProjection`, `StepRunProjection`, `RuntimeCanvasProjection`, `DispatcherClaim`, `OutboxMessage`, `DeadLetterRecord`. |
| Operator and manager | US-036 to US-039, US-043, US-048 | `ManagerIncident`, `EscalationRecord`, `RecoveryAdvice`, `ManagerDirective`, `ReworkRequest`, `ApprovalDecision`, `ManagerMessageProjection`. |
| Messaging and collaboration | US-042, US-052 | `RoleMessage`, `CollaborationThreadLink`, `ParentChildManagerMessage`, subprocess run hierarchy projection. |
| Analytics and live monitoring | US-044 to US-047 | `LiveProcessSnapshot`, `LiveRunCardProjection`, `LiveActivityProjection`, `AnalyticsProjection`, `GraphProjection`, `ProjectionQueryWindow`. |
| Project and tool integration | US-002, US-049 to US-051, US-055 | Project-scoped process context, process agent tool facade, access summary, policy audit, unauthorized mutation report. |

## Replacement Decision Rules

The implementation may replace a current story only when all of the following are true:

- The current behavior is coupled to the old runtime/dispatcher or insecure data access.
- The replacement has an explicit user-facing equivalent or a documented improvement.
- The replacement is assigned to a subbundle and validation proof exists.
- The story map records the delta.

The implementation may remove a story only with explicit user approval recorded in the relevant subbundle execution report and final story coverage matrix.

## Final Coverage Gate

The final rewrite cannot close until:

- every US-001 through US-055 story has an implementation owner,
- every browser-facing story has Playwright proof,
- every non-browser story has source and automated test proof,
- every replacement/improvement decision is documented,
- every approved removal is recorded,
- no generic core/runtime project contains domain-specific vocabulary from current templates or drivers,
- no UI component queries runtime internals or EF runtime entities,
- no live/history query returns out-of-window historical events unless explicitly requested.
