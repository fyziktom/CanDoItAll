# Canonical Model Review Report

- Date: 2026-04-04
- Scope: `post-fix node-scoped assignment canonicalization and lifecycle reconciliation`
- Reviewer: Codex
- Branch / diff: `canonical-model-refactor`
- Evidence sources:
  - repo-local review skillset and agent guidance under `codex/skills/architecture-reviews/*` and `.codex/agents/*.toml`
  - `python codex/skills/architecture-reviews/canonical-model-review/scripts/solution_inventory.py --root . --output architecture/reviews/_inventory-post-fix.json`
  - targeted inspection of `ProjectPartyIntegrationContracts`, `ProjectPartyIntegrationService`, `ProjectStructurePage.PartyIntegration`, `ProjectWorkbenchService`, `ProjectNodeScopeBridge`, and `ProjectWorkbenchMetadata`
  - `dotnet build .\CanDoItAll.slnx`
  - `dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProjectStructurePartyPickerTests"`
  - `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`
  - `dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter "FullyQualifiedName~ProjectPartyAssignmentFlowTests"`
  - `dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter "FullyQualifiedName~CrmHrCrossModuleFlowTests"`

## 1. Executive summary

The critical split source of truth identified in the earlier review is repaired in the current code path. Node-scoped participant, meeting, and work-item party links now read from `CrmHr.ProjectPartyAssignment`, write through explicit bridge operations, and reconcile during Workbench subtree delete and project transfer flows. In the repaired flow, Workbench metadata is still persisted, but it behaves as a derived projection rather than the active canonical owner.

That materially improves change safety for the next feature wave. The remaining architectural risk is no longer silent cross-module drift in the repaired path. It is now concentrated in stabilization work: lifecycle reconciliation is still not atomic across the Workbench and CRM/HR boundary, the universal Workbench node model is still broad, and the cross-module node reference is still a raw `NodeKey` string. Those are real design debts, but they are no longer blocking the current canonical ownership story.

## 2. Scope and evidence gathered

### Files / projects / namespaces inspected

- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePartyPickerTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectPartyAssignmentIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Playwright/ProjectPartyAssignmentFlowTests.cs`
- `tests/CanDoItAll.Tests.Playwright/CrmHrCrossModuleFlowTests.cs`

### Commands run

- `python codex/skills/architecture-reviews/canonical-model-review/scripts/new_review.py canonical-model-review --scope "post-fix node-scoped assignment canonicalization and lifecycle reconciliation" --template codex/skills/architecture-reviews/canonical-model-review/assets/review-report-template.md`
- `python codex/skills/architecture-reviews/canonical-model-review/scripts/solution_inventory.py --root . --output architecture/reviews/_inventory-post-fix.json`
- `dotnet build .\CanDoItAll.slnx`
- `dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProjectStructurePartyPickerTests"`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`
- `dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter "FullyQualifiedName~ProjectPartyAssignmentFlowTests"`
- `dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter "FullyQualifiedName~CrmHrCrossModuleFlowTests"`

Playwright MCP itself remains blocked on this machine because the MCP runtime fails with `EPERM: operation not permitted, mkdir 'C:\Windows\System32\.playwright-mcp'`. Browser proof therefore relied on the passing Playwright browser tests and the refreshed screenshots under `evidence/crm-hr/b10` and `evidence/crm-hr/b11`.

## 3. Candidate canonical concepts

| Concept | Primary kind | Owner project / namespace | Identity | Persisted where | Mutated by | Notes |
|---|---|---|---|---|---|---|
| `Project` | canonical entity | `CanDoItAll.Modules.Projects` | `Project.Id` | `Projects` tables | `ProjectsService` | Owns project identity and hierarchy attachment. |
| `ProjectObjectRecord` | canonical entity / universal node primitive | `CanDoItAll.Modules.Workbench` | `ProjectObjectRecord.Id`, `NodeKey` | `Workbench_ProjectObjects` | `ProjectWorkbenchService` | Canonical owner of structure nodes and their parent-child placement. |
| `ProjectObjectLinkRecord` | relation / edge | `CanDoItAll.Modules.Workbench` | link row identity | `Workbench_ProjectObjectLinks` | `ProjectWorkbenchService` | Canonical owner of structure links. |
| `Party` | canonical entity | `CanDoItAll.Modules.CrmHr` | `Party.Id` | CRM/HR foundation tables | `PartyDirectoryService` | Canonical party identity. |
| `ProjectPartyAssignment` | canonical relation / edge | `CanDoItAll.Modules.CrmHr` | `ProjectPartyAssignment.Id` | CRM/HR business tables | `ProjectPartyIntegrationService` | Canonical owner of project-level and repaired node-scoped party links. |
| `ProjectPartyAssignmentDetail` | projection / read model | `CanDoItAll.Modules.Projects` contract, materialized in `CrmHr` | row identity mirrors assignment id | computed from assignments joined to parties | `ProjectPartyIntegrationService.ListAssignmentsDetailedAsync` | Read-side shape for UI and tests. |
| `ProjectMeetingMetadata`, `ProjectParticipantMetadata`, `ProjectWorkItemMetadata` party fields | projection / duplicated descriptive state | `CanDoItAll.Modules.Workbench` | none beyond parent node | `MetadataJson` inside Workbench nodes | `ProjectStructurePage` save flow | Still stored, but now downstream of canonical assignments in the repaired UI path. |
| `ProjectStructureNode` | projection / UI surface | `CanDoItAll.Modules.Workbench` | `Id` mirrors `NodeKey` | computed from Workbench records | `MapStructureNode` | Structure UI read model, not canonical truth. |

## 4. Single-source-of-truth table

| Concern | Canonical owner | Derived views | Risk of duplicate truth | Notes |
|---|---|---|---|---|
| Project identity and hierarchy | `Projects.Project` and `Projects.ProjectHierarchyLink` | `ProjectSummary`, hierarchy projections | Low | No change from the baseline review. |
| Structure graph and subtree placement | `Workbench.ProjectObjectRecord` and `Workbench.ProjectObjectLinkRecord` | `ProjectStructureNode`, checklist/dependency surfaces | Medium | Canonical owner is clear, but the node primitive is still broad. |
| Party identity | `CrmHr.Party` | option lists, directory views | Low | Boundary remains clear. |
| Project-level party context | `CrmHr.ProjectPartyAssignment` rows with empty `NodeKey` | `ProjectPortfolioPartyContext`, `ProjectSummary` related-party summaries | Low | Healthy derived-view pattern. |
| Node-scoped participant / meeting / work-item links | `CrmHr.ProjectPartyAssignment` rows keyed by `ProjectId + NodeKey + Role` | Workbench metadata fields and editor state | Medium | Canonical owner is now clear in the active path, but duplicate projection fields still exist. |
| Node delete / subtree transfer reconciliation | `ProjectWorkbenchService` plus bridge lifecycle methods | none | Low | The repaired lifecycle now updates canonical assignments after graph mutations. |

## 5. Findings

### Critical

- No current critical finding remains in the repaired canonical path.

### High

#### Lifecycle reconciliation is still non-atomic across the Workbench and CRM/HR boundary

- Claim: the repaired lifecycle now reconciles canonical assignments, but it still does so in a second persistence step after Workbench commits structural mutations.
- Evidence:
  - Workbench delete and subtree-transfer flows save structural changes first and only then call the bridge in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:662-702` and `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:950-993`.
  - The bridge-side cleanup and move implementations run in CRM/HR persistence separately in `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4688-4749`.
- Why it matters: the original split-truth bug is repaired for the normal path, but a mid-flight failure between the two saves can still leave structure state and assignment state temporarily divergent.
- Recommended stabilization action: when this seam grows again, move delete and transfer reconciliation behind one explicit application-level unit of work or compensating-operation strategy.
- Recommended timing: `next_wave`

#### The universal Workbench node remains a broad, overloaded primitive

- Claim: the current model still relies on one node record plus one metadata union to represent many unrelated semantics.
- Evidence:
  - `ProjectWorkbenchService` still centers all structure mutations on `ProjectObjectRecord` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:314-320`.
  - `ProjectMeetingMetadata`, `ProjectParticipantMetadata`, and `ProjectWorkItemMetadata` sit inside the larger metadata envelope in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:211-335`.
  - The earlier review’s broader `ProjectObjectType` and metadata-union concerns remain structurally true; this repair did not reduce that surface.
- Why it matters: the next feature wave can now build on a safer assignment boundary, but every additional node flavor still pushes more semantics into the same universal container.
- Recommended stabilization action: document the intended extension strategy for `ProjectObjectRecord` and the metadata envelope before the next large feature wave.
- Recommended timing: `next_wave`

### Medium

#### Workbench metadata still stores canonical-looking party identifiers and names

- Claim: the repaired path demotes these fields to projection status, but the model still exposes writable duplicate truth.
- Evidence:
  - `ProjectMeetingMetadata.RelatedParties` / `RelatedPartyNames`, `ProjectParticipantMetadata.LinkedPartyId` / `LinkedPartyName`, and `ProjectWorkItemMetadata.AssigneePartyId` / `AssigneePartyName` still exist in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:228-334`.
  - The editor now loads from assignments in `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:145-170`, and the new component test proves stale metadata is repaired from canonical assignments in `tests/CanDoItAll.Tests.Components/ProjectStructurePartyPickerTests.cs:201-304`.
- Why it matters: future code can still mistake those metadata fields for canonical write models unless the team keeps the current discipline explicit.
- Recommended stabilization action: formalize these fields as derived projection data in architecture notes and avoid new direct write paths that treat them as authoritative.
- Recommended timing: `next_wave`

#### `NodeKey` is still the cross-module identity bridge

- Claim: bridge resolution improved, but the contract remains string-based.
- Evidence:
  - The shared contract still exposes `string NodeKey` and lifecycle methods keyed by node strings in `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:153-177`.
  - `ProjectNodeScopeBridge.ResolveAsync` now prefers project-local matches and parses project-root keys, but still resolves by raw string identifiers in `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs:10-85`.
- Why it matters: this is acceptable for the repaired feature set, but it leaves long-term type safety and cross-module discoverability weaker than the rest of the model.
- Recommended stabilization action: introduce a typed project-node reference contract when the next feature wave needs broader cross-module node ownership.
- Recommended timing: `next_wave`

### Low

- No low-priority cleanup item is more important than the medium findings above.

### Open questions

- Should the duplicated party-related metadata fields eventually be reduced to display-only summaries, or are there non-UI consumers that still need the richer projection payload?
- Is the long-term intention that `ProjectPartyAssignment` owns all node-scoped responsible-party semantics, or only the CRM/HR-facing subset?
- Will the next feature wave require node references outside Workbench often enough to justify a typed node-reference value object?

## 6. Stability risks for the next feature wave

- The repaired assignment boundary is safe enough for incremental feature growth, but the universal node model can still accumulate unrelated semantics quickly.
- New contributors can still reintroduce split truth if they write directly to metadata party fields instead of going through the bridge.
- Any broader cross-module node ownership feature will feel the `NodeKey` string contract immediately.

## 7. Stabilization plan

### Now

- Keep new node-scoped party features on the `IProjectPartyIntegrationBridge` path.
- Treat the new lifecycle reconciliation hooks as mandatory for any future node move/delete variant.
- Retain the focused tests that now protect stale-metadata repair and assignment lifecycle cleanup.

### Next wave

- Write an ADR that declares `ProjectPartyAssignment` as the canonical owner for repaired node-scoped party links.
- Decide whether to narrow or annotate the metadata party fields so they are clearly projections.
- Define whether `NodeKey` graduates into a typed cross-module reference.
- Decide how far the universal Workbench node model is allowed to grow before typed families or capability contracts become necessary.

### Later

- Reduce duplication between Workbench metadata and CRM/HR-facing summaries where the extra projection payload no longer adds value.
- Revisit whether some node families should stop sharing one metadata union.

## 8. Scorecard

- source_of_truth_integrity: 4
- boundary_clarity: 3
- invariant_enforcement: 4
- projection_discipline: 3
- integration_isolation: 3
- runtime_state_separation: 4
- ai_policy_separation: 4
- testable_architecture: 4
- change_safety: 3
- overall_stability: 3

## 9. Assumptions

- The repaired Workbench party editor is the primary mutation path for participant, meeting, and work-item node-scoped party links.
- The Playwright MCP failure is environmental and not caused by the repaired code path.
- No other unreviewed module currently writes those metadata party fields directly as canonical truth.

## 10. Suggested ADRs

- ADR: `ProjectPartyAssignment` is the canonical owner for node-scoped responsible-party links in the repaired CRM/HR + Workbench boundary
- ADR: projection-only semantics for party-related Workbench metadata fields
- ADR: typed project-node reference contract for future cross-module features
- ADR: long-term extension strategy for the universal Workbench node
