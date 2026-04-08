# Canonical Model Review Report

- Date: 2026-04-04
- Scope: `canonical-model-refactor` after the recent CRM/HR and Workbench canonical-invariant changes, with emphasis on project structure, project-party integration, and policy/runtime separation.
- Reviewer: Codex
- Branch / diff: `canonical-model-refactor`
- Evidence sources:
  - SharpTools solution and project inspection for `CanDoItAll.Modules.Projects`, `CanDoItAll.Modules.Workbench`, `CanDoItAll.Modules.CrmHr`, and `CanDoItAll.Modules.Workspace`
  - `dotnet build .\CanDoItAll.slnx`
  - `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`
  - Targeted review of project structure party-integration flows and structure lifecycle mutations

## 1. Executive summary

Module ownership is mostly understandable: `Projects` owns project identity and hierarchy, `Workbench` owns the project-structure graph, `CrmHr` owns party and workforce truth, and `Workspace` owns agent/runtime policy. The main blocker is not broad layering collapse; it is a specific split source of truth around node-local party relations.

Today, meeting participants, participant-directory links, and work-item assignees are stored in two places at once: Workbench node metadata and CRM/HR `ProjectPartyAssignment` rows. The UI reads metadata, then dual-writes metadata plus assignments on save. Structure lifecycle operations such as node deletion and subtree transfer only mutate Workbench records, so the second store can become stale without any invariant failing. That is the seam most likely to create expensive refactoring pressure in the next feature wave.

## 2. Scope and evidence gathered

### Files / projects / namespaces inspected

- `src/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeMutations.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrFoundationModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationModels.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectPartyAssignmentIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`

### Commands run

- `python .\codex\skills\architecture-reviews\canonical-model-review\scripts\new_review.py canonical-model-review --scope "canonical-model-refactor branch after CRM/HR + workbench canonical invariant changes" --template .\codex\skills\architecture-reviews\canonical-model-review\assets\review-report-template.md`
- `dotnet build .\CanDoItAll.slnx`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`

Build and targeted integration validation passed. The current problem is architectural coherence, not an already broken build.

## 3. Candidate canonical concepts

| Concept | Primary kind | Owner project / namespace | Identity | Persisted where | Mutated by | Notes |
|---|---|---|---|---|---|---|
| `Project` | canonical entity | `CanDoItAll.Modules.Projects` | `Project.Id` | `Projects` tables via `ProjectModels.cs` | `ProjectsService` | Owns portfolio identity, phase, status, objective, and hierarchy attachment context. |
| `ProjectHierarchyLink` | relation / edge | `CanDoItAll.Modules.Projects` | `ProjectHierarchyLink.Id` | `Projects` tables via `ProjectModels.cs` | `ProjectsService` | Canonical project-to-project parent/child relation. |
| `ProjectObjectRecord` | canonical entity / universal node primitive | `CanDoItAll.Modules.Workbench` | `ProjectObjectRecord.Id` and `NodeKey` | `Workbench_ProjectObjects` | `ProjectWorkbenchService` and agent APIs | Canonical owner of project-structure nodes, but currently overloaded. |
| `ProjectObjectLinkRecord` | relation / edge | `CanDoItAll.Modules.Workbench` | link record id | `Workbench_ProjectObjectLinks` | `ProjectWorkbenchService` | Canonical owner of graph edges inside a project structure. |
| `ProjectStructureNode` | projection / read model | `CanDoItAll.Modules.Workbench` | `Id` mirrors `NodeKey` | built from `ProjectObjectRecord` + derived badges/markers/project-role data | `MapStructureNode` | Read-side surface for UI, checklist, validation, and agent APIs. |
| `Party` | canonical entity | `CanDoItAll.Modules.CrmHr` | `Party.Id` | CRM/HR foundation tables | `PartyDirectoryService` and CRM/HR services | Canonical owner of people/org/unit identity. |
| `ProjectPartyAssignment` | relation / edge with mixed scope | `CanDoItAll.Modules.CrmHr` | `ProjectPartyAssignment.Id` | CRM/HR business tables | `ProjectPartyIntegrationService` | Holds project-level party assignments well, but also duplicates node-local relations that Workbench metadata stores. |
| `ProjectStructureAgentProfileRecord` | policy / authorization object | `CanDoItAll.Modules.Workspace` | `ProjectStructureAgentProfileRecord.Id` | Workspace policy tables | `ProjectStructureAgentAdministrationService` | Good example of runtime/policy truth staying outside the core domain model. |

## 4. Single-source-of-truth table

| Concern | Canonical owner | Derived views | Risk of duplicate truth | Notes |
|---|---|---|---|---|
| Project identity and hierarchy | `Projects.Project` and `Projects.ProjectHierarchyLink` | `ProjectSummary`, hierarchy dialogs, parent/subproject projection nodes | Low | This boundary is mostly clear. |
| Project structure graph | `Workbench.ProjectObjectRecord` and `Workbench.ProjectObjectLinkRecord` | `ProjectStructureNode`, checklist/dependency surfaces, gantt preview | Medium | Projection discipline is decent, but the underlying node primitive is broad. |
| Party and workforce identity | `CrmHr.Party`, `WorkforceProfile`, `Opportunity` | account/workforce/workspace models | Low | CRM/HR ownership is clear. |
| Project-level portfolio party context | `CrmHr.ProjectPartyAssignment` rows with empty `NodeKey` | `ProjectPortfolioPartyContext`, `ProjectSummary.RelatedParties` | Low | Project summary correctly treats this as derived data. |
| Node-local participant / meeting / work-item party links | Split between Workbench metadata (`Participant.LinkedPartyId`, `Meeting.RelatedParties`, `WorkItem.AssigneePartyId`) and `CrmHr.ProjectPartyAssignment` | Workbench party editor, assignment detail lists | High | This is the main source-of-truth split. |
| Agent authorization and runtime policy | `Workspace.ProjectStructureAgentProfileRecord` and related policy records | setup guides, authorization decisions | Low | Policy/runtime separation is comparatively healthy. |

## 5. Findings

### Critical

#### Node-local party truth is stored twice and mutated through dual-write orchestration

- Claim: participant-directory links, meeting participants, and work-item assignees are not owned by one canonical model.
- Evidence:
  - Workbench metadata stores the relation in `ProjectMeetingMetadata.RelatedParties`, `ProjectParticipantMetadata.LinkedPartyId`, and `ProjectWorkItemMetadata.AssigneePartyId` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:211-335`.
  - The editor loads selected values from metadata, not from assignments, in `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:129-178`.
  - Save flows write metadata first and then write `ProjectPartyAssignment` rows through `ReplaceNodeAssignmentsAsync` in `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:240-447`.
- Why it matters: there is no single authoritative owner for node-local party links. Any new feature that edits either side alone will create drift, and every new node-local party feature will add more sync code rather than strengthening the model.
- Recommended stabilization action: choose a single canonical owner for node-local party relations. Either make Workbench metadata canonical and derive CRM/HR node assignments from it, or make CRM/HR assignments canonical and derive node metadata from them.
- Recommended timing: `now`

### High

#### Structure lifecycle mutations do not reconcile node-scoped assignments

- Claim: deleting or transferring structure nodes can leave stale `ProjectPartyAssignment` rows behind.
- Evidence:
  - `DeleteObjectAsync` removes only `ProjectObjectRecord` and `ProjectObjectLinkRecord` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:661-717`.
  - `MoveDescendantsToProjectAsync` rewrites Workbench node `ProjectId`, parent links, routes, and graph links only in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:945-1063`.
  - `ListAssignmentsDetailedAsync` reads raw assignment rows by project without revalidating node existence in `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4376-4420`.
  - The subtree-transfer UI path calls `MoveDescendantsToProjectAsync` directly from `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeMutations.cs:182-255`.
- Why it matters: a moved node can keep its metadata and structure in the target project while its CRM/HR assignment rows still point to the original project. A deleted node can disappear from Workbench while assignments remain queryable and silently stale.
- Recommended stabilization action: whichever store is not selected as canonical for node-local party relations must be reconciled during delete, subtree transfer, and any future reclassification/migration flows.
- Recommended timing: `now`

#### The universal Workbench node has become a catch-all semantic container

- Claim: `ProjectObjectRecord` is absorbing too many unrelated concerns.
- Evidence:
  - `ProjectObjectType` spans 27 concepts from `Meeting` and `Transcript` to `Repository`, `Infrastructure`, `ValidationRun`, and `SecretReference` in `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs:3-32`.
  - `ProjectObjectRecord` combines route/media/storage/external artifact/progress/marker/layout/timing concerns in one table row in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-60`.
  - `ProjectObjectMetadataEnvelope` adds a JSON union for meeting, recording, transcript, participant, work item, repository, file, script, environment, infrastructure, link, and marker families in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:165-190`.
  - `ProjectObjectMetadataSerializer.Validate` enforces one family payload, but only after the model has already accepted the universal container design in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:625-660`.
- Why it matters: the next feature wave will almost certainly add more object types or metadata families, which will increase branching and cross-cutting conditionals unless the extension strategy becomes explicit.
- Recommended stabilization action: write down whether `ProjectObjectRecord` is intentionally the universal primitive with explicit per-type capability rules, or whether the model is expected to split into typed families over time.
- Recommended timing: `next_wave`

### Medium

#### `NodeKey` remains a string bridge instead of a strong cross-module reference

- Claim: the cross-module contract for canonical structure nodes is still a raw string.
- Evidence:
  - `ProjectPartyAssignment.NodeKey` stores a plain string in `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs:413-428`.
  - `ProjectObjectRecord` only enforces uniqueness on `{ ProjectId, NodeKey }` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:64-87`.
  - `ProjectNodeScopeBridge.ResolveAsync` queries by `NodeKey` alone and returns the first match in `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs:10-49`.
- Why it matters: current node generation mostly uses `custom:{Guid}` keys, but the contract itself does not express global identity or prevent future non-unique keys from confusing cross-module validation.
- Recommended stabilization action: introduce a typed node reference contract, or at minimum make cross-module bridge resolution explicitly keyed by both `ProjectId` and node id semantics rather than a loose string lookup.
- Recommended timing: `next_wave`

#### The current tests do not cover node-party coherence across structure lifecycle changes

- Claim: the critical seam above is not protected by an end-to-end lifecycle test.
- Evidence:
  - `ProjectPartyAssignmentIntegrationTests` cover save and validation behavior only in `tests/CanDoItAll.Tests.Integration/ProjectPartyAssignmentIntegrationTests.cs:12`, `:81`, and `:135`.
  - `ProjectWorkbenchServiceIntegrationTests` cover node deletion and subtree transfer separately in `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:620` and `:1056`.
  - No reviewed test combines node-scoped assignment creation with delete or subtree transfer.
- Why it matters: the exact split-truth failure mode can regress silently while the current targeted suites remain green.
- Recommended stabilization action: add integration coverage for create node -> assign party -> delete node and create node -> assign party -> subtree transfer.
- Recommended timing: `now`

### Low

- No low-severity-only items were worth elevating separately. The current risks are concentrated in a few high-leverage seams.

### Open questions

- Should node-local party links be canonical in Workbench or in CRM/HR?
- Is `ProjectPartyAssignment` supposed to represent project-level staffing only, or both project-level and node-local ownership?
- Is `ProjectObjectRecord` intentionally the long-term universal primitive, or is the current metadata-envelope pattern only an interim bridge?

## 6. Stability risks for the next feature wave

- Any new node-local feature that links parties, responsibilities, or approvals to structure nodes will likely repeat the current dual-write pattern unless the canonical owner is fixed first.
- Additional object types in Workbench will probably expand `ProjectObjectMetadataEnvelope` and `ProjectObjectType` faster than the current invariants can keep the model explicit.
- More external modules using `NodeKey` as a cross-module string contract will raise the cost of replacing it later with a stronger reference type.

## 7. Stabilization plan

### Now

- Decide and document the canonical owner for node-local party relations.
- Reconcile the non-canonical store during `DeleteObjectAsync`, `MoveDescendantsToProjectAsync`, and related lifecycle flows.
- Add integration tests that span node-scoped assignment creation plus delete/transfer behavior.

### Next wave

- Replace raw `NodeKey` coupling with a typed reference contract or a strictly scoped composite reference.
- Make per-type Workbench capabilities explicit if `ProjectObjectRecord` remains the universal node primitive.
- Move node-local party-link rules into one domain service instead of keeping them in page-level switch logic plus CRM/HR bridge validation.

### Later

- Reduce raw `MetadataJson` usage across UI/service boundaries in favor of typed mutation DTOs where the semantics are stable.
- Revisit whether node-local CRM/HR views should be built as projections rather than independent write models.

## 8. Scorecard

- source_of_truth_integrity: 2
- boundary_clarity: 3
- invariant_enforcement: 3
- projection_discipline: 3
- integration_isolation: 2
- runtime_state_separation: 4
- ai_policy_separation: 4
- testable_architecture: 3
- change_safety: 2
- overall_stability: 2

## 9. Assumptions

- The review assumes the active code path for node-local participant/meeting/work-item party editing is the Workbench page flow inspected above.
- The review assumes `ProjectSummary.RelatedParties` is intentionally a derived portfolio view and not a canonical source of project-party truth.
- No runtime mutation path outside the reviewed files was found that reconciles node-scoped assignments during node delete or subtree transfer.

## 10. Suggested ADRs

- ADR: canonical owner of node-local party relations (`Workbench` metadata vs `CrmHr.ProjectPartyAssignment`)
- ADR: global project-node reference contract
- ADR: extension strategy for the universal Workbench node
