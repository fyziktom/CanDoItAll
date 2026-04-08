# Canonical Model Review Report

- Date: 2026-04-04
- Scope: `CanDoItAll.Modules.Projects`, `CanDoItAll.Modules.Workbench`, `CanDoItAll.Modules.CrmHr`, and `CanDoItAll.Modules.Workspace`
- Reviewer: Codex
- Branch / diff: `canonical-model-refactor`
- Evidence sources:
  - SharpTools type maps, symbol definitions, reference tracing, and complexity analysis
  - `dotnet build CanDoItAll.slnx`
  - `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`

## 1. Executive summary

The top-level canonical ownership is mostly legible: `Project` and `ProjectHierarchyLink` remain the primary truth for portfolio structure in `CanDoItAll.Modules.Projects`, `Party` remains the primary truth for people and organizations in `CanDoItAll.Modules.CrmHr`, and `Workspace` mostly holds operational configuration rather than project content. The unstable seam is the cross-module join between Workbench nodes and CRM/HR assignments.

The most serious issue is that node-scoped party assignments are only validated at write time. `ProjectPartyAssignment` stores `ProjectId` plus a plain `NodeKey`, but `DeleteObjectAsync` and `MoveDescendantsToProjectAsync` in Workbench never repair or remove those assignment rows. That means the system can silently retain assignments to deleted nodes or leave assignments under the wrong project after subtree transfer.

The second major issue is architectural drag inside Workbench. `ProjectObjectRecord`, `ProjectObjectMetadataEnvelope`, and `ProjectWorkbenchService` are carrying structural truth, attachment/storage concerns, UI routing state, marker presentation, prompt/test/runtime concepts, and subtype-specific metadata through one universal node model. That will make the next feature wave materially riskier even if current tests stay green.

## 2. Scope and evidence gathered

### Files / projects / namespaces inspected

- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationService.cs`

### Commands run

- `dotnet build CanDoItAll.slnx`
  - Passed
  - Warnings only: unrelated `NU1510` package-trimming warnings in `src/CanDoItAll.Mcp.DotNetWatch/CanDoItAll.Mcp.DotNetWatch.csproj`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`
  - Passed: 22
  - Failed: 0

## 3. Candidate canonical concepts

| Concept | Primary kind | Owner project / namespace | Identity | Persisted where | Mutated by | Notes |
|---|---|---|---|---|---|---|
| Project | Canonical entity | `CanDoItAll.Modules.Projects` | `Project.Id` | `Projects_Project` | `ProjectsService`, `ProjectStructureAgentService.SaveProjectAsync` | Clear top-level portfolio owner. |
| Project hierarchy link | Relation / edge | `CanDoItAll.Modules.Projects` | `ProjectHierarchyLink.Id` / `(ParentProjectId, ChildProjectId)` | `Projects_ProjectHierarchyLinks` | `ProjectsService` | Separate from workbench subtree hierarchy, which is good. |
| Project structure node | Canonical entity, but overloaded | `CanDoItAll.Modules.Workbench` | `ProjectObjectRecord.Id` plus `NodeKey` | `Workbench_ProjectObjects` | `ProjectWorkbenchService` | Structural truth is mixed with route, media, storage, marker, and subtype JSON concerns. |
| Project structure link | Relation / edge | `CanDoItAll.Modules.Workbench` | `ProjectObjectLinkRecord.Id` | `Workbench_ProjectObjectLinks` | `ProjectWorkbenchService` | Enforces explicit graph edges inside a project. |
| Party | Canonical entity | `CanDoItAll.Modules.CrmHr` | `Party.Id` | `CrmHr_Parties` | `PartyDirectoryService`, CRM/HR services | Clean owner for people and organizations. |
| Project-party assignment | Relation / edge | `CanDoItAll.Modules.CrmHr` | `ProjectPartyAssignment.Id` | `CrmHr_ProjectPartyAssignments` | `ProjectPartyIntegrationService` | Uses `ProjectId` plus plain `NodeKey` to point back into Workbench. |
| Provider/storage/agent policy config | Policy / runtime state | `CanDoItAll.Modules.Workspace` | Profile / settings record IDs | Workspace tables | `WorkspaceService`, `ProjectStructureAgentAdministrationService` | Operational truth, not project-content truth. |
| Project structure node projection | Projection / derived view | `CanDoItAll.Modules.Workbench` | `ProjectStructureNode.Id` mirrors `NodeKey` | Not persisted directly | `ProjectWorkbenchService.MapStructureNode` | Read model includes derived badges, marker presentation, project-role overlays, and storage metadata. |

## 4. Single-source-of-truth table

| Concern | Canonical owner | Derived views | Risk of duplicate truth | Notes |
|---|---|---|---|---|
| Portfolio project identity and lifecycle | `Projects.Project` | Project cards, workspace pickers, MCP project choices | Low | Still reasonably clean. |
| Portfolio parent/child relationships | `Projects.ProjectHierarchyLink` | Subproject dialogs, workbench related-project overlays | Medium | Separate from workbench subtree structure, which is correct but easy to blur in future work. |
| In-project graph structure | `Workbench.ProjectObjectRecord` + `ProjectObjectLinkRecord` | `ProjectStructureNode`, calendar/Gantt/dependency overlays | High | Structural truth is mixed with presentation/storage/runtime fields on the same record. |
| Project-to-party and node-to-party role assignments | `CrmHr.ProjectPartyAssignment` | Portfolio contexts, party project assignment lists, workbench party pickers | High | Canonical assignment rows duplicate node scope through `NodeKey` instead of durable typed identity. |
| Party directory and contact truth | `CrmHr.Party` plus related CRM/HR records | Project party options, account/workforce views | Low | Stronger owner than the project-node boundary. |
| Agent/provider/storage configuration | Workspace profile/settings records | Workbench file handling, MCP setup guide, provider health checks | Medium | Operational truth is separate, but policy ownership is not clearly aligned with `Modules.Security`. |

## 5. Findings

### Critical

- Claim: Node-scoped party assignments are validated on write but are not lifecycle-managed when Workbench nodes are deleted or moved across projects.
  - Evidence: `ProjectPartyAssignment` persists `ProjectId` and `NodeKey` as the cross-module reference in `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs` (symbol: `ProjectPartyAssignment`). `SaveAssignmentAsync` validates node scope once and then stores the normalized `NodeKey` in `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` (symbol: `ProjectPartyIntegrationService.SaveAssignmentAsync`). `DeleteObjectAsync` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` deletes only `ProjectObjectRecord` and `ProjectObjectLinkRecord`. `MoveDescendantsToProjectAsync` in the same file rewrites `ProjectObjectRecord.ProjectId`, `ParentNodeKey`, and routes, but never touches `CrmHr_ProjectPartyAssignments`.
  - Why it matters: deleting or transferring a subtree can silently leave assignments pointing at deleted nodes or at nodes that now live under another project. That is direct cross-module truth divergence.
  - Recommended stabilization action: introduce a durable project-node reference contract owned by Workbench or Projects, and make delete/move flows cascade or repair CRM/HR assignment rows. Add integration tests that exercise delete and subtree transfer with node-scoped assignments.
  - Recommended timing: `now`

### High

- Claim: The current `NodeKey` bridge contract is globally ambiguous even though Workbench only guarantees uniqueness per project.
  - Evidence: `ProjectObjectRecordConfiguration.Configure` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` enforces uniqueness only on `{ ProjectId, NodeKey }`. `ProjectNodeScopeBridge.ResolveAsync` in `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs` queries by `NodeKey` alone and returns `FirstOrDefault`. The shared integration contract exposes `ProjectPartyAssignmentUpsertRequest.NodeKey` as a plain string in `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`.
  - Why it matters: current creation paths mostly generate `custom:{Guid}` keys, so collisions are unlikely today, but the contract itself does not guarantee that. A future import path, deterministic key scheme, or system-managed node generator can make ownership resolution non-deterministic across projects.
  - Recommended stabilization action: replace string-only node references with a durable typed contract, preferably including the Workbench node GUID or a strongly typed `(ProjectId, NodeId)` reference. Scope bridge lookups by the owning project instead of global `NodeKey` search.
  - Recommended timing: `now`

- Claim: Workbench is using one universal node record and one orchestration service to carry too many unrelated semantics.
  - Evidence: `ProjectObjectType` in `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs` spans 27 concepts from meetings/files/media to prompt flows, validation runs, tests, notes, and secret references. `ProjectObjectRecord` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` stores structural fields together with route, external artifact linkage, media descriptors, storage JSON, markers, progress, and subtype JSON. `ProjectObjectMetadataEnvelope` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs` concentrates many subtype-specific payloads. Complexity analysis shows `ProjectWorkbenchService` with 101 members, 133 dependencies, `SyncGraphAsync` cognitive complexity 1020, and `MoveDescendantsToProjectAsync` cognitive complexity 322; `ProjectObjectMetadataSerializer` has high coupling and very high subtype-detection complexity.
  - Why it matters: every new feature wave is incentivized to extend the universal node instead of modeling a narrower bounded concept. That raises regression risk and makes invariant reasoning progressively harder.
  - Recommended stabilization action: separate graph ownership from attachment/storage/presentation/runtime concerns. Keep `ProjectObjectRecord` closer to structural truth and move subtype-heavy operational behavior into narrower services or extension records.
  - Recommended timing: `next_wave`

### Medium

- Claim: Project-party assignment roles are duplicated across the shared Projects contract and CRM/HR persistence model, and both mapping functions fail open.
  - Evidence: `ProjectPartyAssignmentRole` lives in `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`, while `ProjectPartyAssignmentKind` lives in `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`. `MapRole(ProjectPartyAssignmentKind)` and `MapRole(ProjectPartyAssignmentRole)` in `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` both use `_ => TechnicalContact` as the default arm.
  - Why it matters: if the next feature wave adds a role on one side and misses the other, the system will silently coerce the new role to `TechnicalContact` instead of failing fast. That is exactly how split truth creeps in across module boundaries.
  - Recommended stabilization action: replace the default arms with explicit `TechnicalContact` cases and throw for unknown values. After that, decide whether the duplicated enums are still justified or whether one shared enum is safer.
  - Recommended timing: `now`

- Claim: Project-structure agent authorization policy currently lives in Workspace operational administration rather than a clear security boundary.
  - Evidence: `ProjectStructureAgentAdministrationService` in `src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationService.cs` owns token storage, capability masks, approval thresholds, per-project overrides, and `AuthorizeAsync`, while a dedicated `CanDoItAll.Modules.Security` module also exists.
  - Why it matters: this is not a direct data-corruption bug today, but it blurs whether project-structure agent policy is operational configuration or security policy. That ambiguity will matter if broader agent capabilities or cross-surface policies are added later.
  - Recommended stabilization action: write down the ownership decision in an ADR. Either keep it in Workspace as operational MCP administration, or move the policy core into Security and leave Workspace as the editor.
  - Recommended timing: `next_wave`

### Low

- Claim: None.

### Open questions

- Are imported or system-managed Workbench node keys always GUID-based, or can product features introduce deterministic or user-derived `NodeKey` values?
- Should node-scoped project-party assignments remain persisted canonical relations, or should they become projections over a stronger shared project-structure identity?
- Is project-structure agent authorization intended to remain a workspace-local concern, or is it part of the broader security model?

## 6. Stability risks for the next feature wave

- Another feature that adds node-scoped collaboration, staffing, CRM, or automation semantics will almost certainly touch `ProjectObjectRecord`, `ProjectWorkbenchService`, and `ProjectObjectMetadataEnvelope`, increasing the cost of safe change.
- A feature that deletes or moves subtrees while also assigning people or agents to nodes risks producing silent cross-module inconsistency immediately.
- Any new project-party role or new node identity scheme will be fragile until the `NodeKey` contract and enum duplication are tightened.

## 7. Stabilization plan

### Now

- Make node-scoped assignment lifecycle safe for Workbench delete and subtree transfer operations.
- Replace the `NodeKey` string boundary with a durable typed project-node reference, or at minimum scope every lookup by project and fail if multiple rows share the same `NodeKey`.
- Remove the silent fallback arms from the role mapping methods and add explicit tests for unknown values.
- Add integration tests for:
  - delete node with node-scoped assignments
  - subtree transfer with node-scoped assignments
  - duplicate or ambiguous node reference handling

### Next wave

- Split Workbench structural truth from storage/media/route/presentation/runtime concerns.
- Break `ProjectWorkbenchService` into smaller services around graph mutation, graph projection, media/storage handling, and cross-project synchronization.
- Decide and document where project-structure agent policy belongs relative to Workspace and Security.

### Later

- Revisit the `ProjectObjectType` taxonomy and define extension rules for new concept families instead of growing the universal enum indefinitely.
- Reduce subtype inference and marker/presentation branching in `ProjectObjectMetadataSerializer`.
- Consider whether some node subtypes deserve first-class tables or bounded models instead of remaining inside the generic metadata envelope.

## 8. Scorecard

- source_of_truth_integrity: 2
- boundary_clarity: 3
- invariant_enforcement: 3
- projection_discipline: 2
- integration_isolation: 2
- runtime_state_separation: 2
- ai_policy_separation: 3
- testable_architecture: 3
- change_safety: 2
- overall_stability: 2

## 9. Assumptions

- The review focused on architecture and code-level evidence, not on production data inspection.
- Current user-authored node creation paths use GUID-backed `NodeKey` values, which reduces collision likelihood but does not change the contract weakness.
- Passing integration tests mean the current branch is green, not that delete/move lifecycle invariants across modules are complete.

## 10. Suggested ADRs

- ADR: Durable cross-module project-node reference contract and lifecycle rules.
- ADR: Boundary between Workbench structural truth and attachment/runtime/presentation/storage concerns.
- ADR: Ownership of project-structure agent policy between Workspace and Security.
