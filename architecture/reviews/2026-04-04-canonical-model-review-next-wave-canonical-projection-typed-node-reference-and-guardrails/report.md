# Canonical Model Review Report

- Date: 2026-04-04
- Scope: `next-wave canonical projection, typed node reference, and guardrails`
- Reviewer: Codex
- Branch / diff: `current workspace changes after bundle v4 next-wave execution`
- Evidence sources:
  - repo-local review skillset and agent guidance under `codex/skills/architecture-reviews/canonical-model-review/*` and `.codex/agents/*.toml`
  - `python .\codex\skills\architecture-reviews\canonical-model-review\scripts\solution_inventory.py --root . --output .\architecture\reviews\_inventory-next-wave.json`
  - targeted inspection of `ProjectPartyIntegrationContracts`, `CrmHrServices`, `ProjectWorkbenchModels`, `ProjectWorkbenchMetadata`, `ProjectStructurePage.PartyIntegration`, `ProjectStructureNodeDescriptor`, and `ProjectNodeScopeBridge`
  - `dotnet build .\CanDoItAll.slnx`
  - `dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProjectStructurePartyPickerTests"`
  - `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`
  - `dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter "FullyQualifiedName~ProjectPartyAssignmentFlowTests|FullyQualifiedName~CrmHrCrossModuleFlowTests"`
  - direct Playwright MCP probe via `browser_navigate` to `about:blank`, which still fails with `EPERM: operation not permitted, mkdir 'C:\Windows\System32\.playwright-mcp'`

## 1. Executive summary

This next-wave materially reduces the remaining split-source-of-truth risk around project and node party ownership. The active boundary now states canonical intent more clearly: node-scoped assignment operations use `ProjectNodeReference`, Workbench lifecycle mutations compensate when downstream assignment reconciliation fails, and the Workbench metadata contract now stores projection-only display summaries instead of canonical-looking party ids and rich linked-party payloads.

That is enough to move the architecture from `overall_stability: 3` to `overall_stability: 4`. The repaired path is no longer one feature away from silently recreating the original dual-write bug. The main remaining risks are narrower and explicit: the Workbench to CRM/HR seam is still a two-step persistence flow rather than one atomic unit, and the universal Workbench node remains a broad primitive that can absorb too much business meaning if future features are not disciplined.

## 2. Scope and evidence gathered

### Files / projects / namespaces inspected

- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureNodeDescriptor.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs`
- `architecture/adrs/ADR-0001-canonical-project-party-assignment-ownership.md`
- `architecture/adrs/ADR-0002-workbench-party-metadata-is-projection-only.md`
- `architecture/adrs/ADR-0003-use-typed-project-node-references-across-module-boundaries.md`
- `architecture/adrs/ADR-0004-workbench-node-extension-guardrails.md`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePartyPickerTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectPartyAssignmentIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Playwright/ProjectPartyAssignmentFlowTests.cs`
- `tests/CanDoItAll.Tests.Playwright/CrmHrCrossModuleFlowTests.cs`

### Commands run

- `python .\codex\skills\architecture-reviews\canonical-model-review\scripts\solution_inventory.py --root . --output .\architecture\reviews\_inventory-next-wave.json`
- `dotnet build .\CanDoItAll.slnx`
- `dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProjectStructurePartyPickerTests"`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`
- `dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter "FullyQualifiedName~ProjectPartyAssignmentFlowTests|FullyQualifiedName~CrmHrCrossModuleFlowTests"`

The browser-test evidence refreshed successfully through the Playwright test suite. Direct Playwright MCP control is still blocked by the machine-level `EPERM` failure above, so screenshots come from the passing browser tests under `evidence/crm-hr/b10` and `evidence/crm-hr/b11`.

## 3. Candidate canonical concepts

| Concept | Primary kind | Owner project / namespace | Identity | Persisted where | Mutated by | Notes |
|---|---|---|---|---|---|---|
| `Project` | canonical entity | `CanDoItAll.Modules.Projects` | `Project.Id` | project tables | `ProjectsService` | Canonical project identity. |
| `ProjectObjectRecord` | canonical entity / universal node primitive | `CanDoItAll.Modules.Workbench` | `ProjectObjectRecord.Id` | Workbench tables | `ProjectWorkbenchService` | Canonical owner of structure nodes and parent-child placement. |
| `ProjectObjectLinkRecord` | relation / edge | `CanDoItAll.Modules.Workbench` | link row identity | Workbench link tables | `ProjectWorkbenchService` | Canonical owner of explicit structure links. |
| `Party` | canonical entity | `CanDoItAll.Modules.CrmHr` | `Party.Id` | CRM/HR tables | `PartyDirectoryService` | Canonical party identity. |
| `ProjectPartyAssignment` | canonical relation / edge | `CanDoItAll.Modules.CrmHr` | `ProjectPartyAssignment.Id` | CRM/HR assignment tables | `ProjectPartyIntegrationService` | Canonical owner of project-level and node-scoped party links. |
| `ProjectNodeReference` | value object / boundary contract | `CanDoItAll.Modules.Projects` | wrapped `NodeKey` | not persisted directly | bridge callers | Thin typed boundary object for cross-module node targeting. |
| Workbench metadata party summaries | projection / display snapshot | `CanDoItAll.Modules.Workbench` | parent node identity only | `MetadataJson` on Workbench nodes | `ProjectStructurePage` save flow | Now reduced to display summaries such as `RelatedPartySummary`, `LinkedPartyDisplayName`, and `AssigneePartyDisplayName`. |
| `ProjectStructureNode` | projection / UI surface | `CanDoItAll.Modules.Workbench` | `Id` mirrors node key | computed from canonical Workbench records | projection mapping | Not canonical truth. |

## 4. Single-source-of-truth table

| Concern | Canonical owner | Derived views | Risk of duplicate truth | Notes |
|---|---|---|---|---|
| Project identity and hierarchy | `Projects` | summaries and board views | Low | No new concern in this wave. |
| Structure graph and subtree placement | `Workbench.ProjectObjectRecord` and `Workbench.ProjectObjectLinkRecord` | structure surface, descriptors, checklists | Medium | Canonical owner is clear, but the node primitive is still broad. |
| Party identity | `CrmHr.Party` | directory options and summaries | Low | Stable boundary. |
| Project-level party context | `CrmHr.ProjectPartyAssignment` rows without node scope | projects board and assignment views | Low | Healthy projection pattern. |
| Node-scoped participant / meeting / work-item party ownership | `CrmHr.ProjectPartyAssignment` rows with node scope | editor state and Workbench metadata summaries | Low | This was the main repaired concern. |
| Metadata party labels shown in the structure UI | Workbench metadata summary fields | node facts and editor messages | Medium | Projection-only now, but still writable summaries that require discipline. |
| Delete and subtree-transfer reconciliation | Workbench lifecycle plus bridge compensation | none | Medium | Safer than before, but still a multi-step boundary. |

## 5. Findings

### Critical

- No critical finding remains in the repaired next-wave path.

### High

#### Lifecycle reconciliation is still non-atomic across the Workbench and CRM/HR boundary

- Claim: the next-wave compensation protects against obvious drift, but the structural mutation and the assignment mutation still commit in separate persistence steps.
- Evidence:
  - `ProjectWorkbenchService.DeleteObjectAsync` and `MoveDescendantsToProjectAsync` still save Workbench changes before invoking bridge cleanup or transfer, then compensate on failure in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`.
  - `ProjectPartyIntegrationService.DeleteAssignmentsForNodesAsync` and `MoveAssignmentsToProjectAsync` still persist in CRM/HR separately in `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`.
- Why it matters: a failure between the two commits is now recoverable in the application path, but it is still not one atomic invariant boundary.
- Recommended stabilization action: keep the compensation path, and treat any future widening of this seam as the trigger for an explicit application-level unit-of-work or transactional boundary redesign.
- Recommended timing: `next_wave`

#### The universal Workbench node remains a broad primitive that can absorb unrelated semantics

- Claim: this wave improved boundary discipline without changing the underlying universal-node shape.
- Evidence:
  - `ProjectObjectRecord` still anchors all structure behavior in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`.
  - the metadata envelope still carries many optional families in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`.
  - the new ADR `ADR-0004-workbench-node-extension-guardrails.md` exists precisely because the node primitive is still broad enough to drift again.
- Why it matters: the original source-of-truth bug is repaired, but the next bad feature could still stuff new canonical behavior into the universal node unless review discipline holds.
- Recommended stabilization action: enforce the ADR guardrails during feature design and reject new Workbench metadata that acts like a hidden canonical store.
- Recommended timing: `next_wave`

### Medium

#### The persisted assignment model still ultimately keys node scope with a raw string

- Claim: the cross-module boundary is typed now, but the underlying persistence and several internal flows still normalize to plain node-key strings.
- Evidence:
  - the external contracts now use `ProjectNodeReference` in `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`.
  - `ProjectNodeScopeBridge.ResolveAsync` accepts `ProjectNodeReference` in `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs`.
  - `ProjectPartyIntegrationService.NormalizeNodeKeys` in `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` still converts the typed reference back to string keys for persistence operations.
- Why it matters: this is acceptable for the current scope, but deeper cross-module node ownership features will still feel the underlying string identity.
- Recommended stabilization action: keep the typed boundary, and consider a stronger persisted node-reference model only when another feature wave proves the string representation too weak.
- Recommended timing: `later`

#### Backward-compatible JSON property names still expose the old semantic labels to raw metadata consumers

- Claim: the C# contract is projection-only now, but the JSON names remain `relatedPartyNames`, `linkedPartyName`, and `assigneePartyName` for compatibility.
- Evidence:
  - `ProjectMeetingMetadata.RelatedPartySummary`, `ProjectParticipantMetadata.LinkedPartyDisplayName`, and `ProjectWorkItemMetadata.AssigneePartyDisplayName` map through `[JsonPropertyName(...)]` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`.
- Why it matters: any future raw JSON consumer could still treat those names as stronger than they are unless the ADR and code review discipline are followed.
- Recommended stabilization action: keep the compatibility mapping for now, but treat the typed property names and ADR as the supported semantic contract.
- Recommended timing: `later`

### Low

- No low-priority item matters more than the medium findings above.

### Open questions

- Will any future feature need a persisted typed node reference rather than the current wrapper-over-string contract?
- Are there any raw metadata consumers outside the reviewed C# code paths that still reason directly over the legacy JSON property names?
- How much additional behavior is the team willing to allow into the universal Workbench node before a typed-family redesign becomes cheaper than continued guardrails?

## 6. Stability risks for the next feature wave

- The repaired path is safer, but future lifecycle variants that bypass compensation will reopen cross-module drift quickly.
- The universal node remains the main architectural pressure point for future feature work.
- Raw JSON consumers are now the most likely place for accidental semantic drift, not the reviewed editor path.

## 7. Stabilization plan

### Now

- Keep node-scoped party ownership on the `IProjectPartyIntegrationBridge` path only.
- Treat the new ADRs as binding review criteria for feature design.
- Preserve the focused component, integration, and browser tests that now cover stale-metadata repair and lifecycle compensation.

### Next wave

- Revisit the Workbench to CRM/HR mutation seam only if another feature needs broader multi-entity lifecycle writes.
- Enforce the Workbench-node extension guardrails in design reviews before adding new metadata-heavy node behavior.
- Verify there are no raw JSON consumers treating the legacy property names as canonical identity fields.

### Later

- Reassess whether persisted node scope should graduate beyond raw string identity.
- Reassess whether the universal Workbench node should split into typed families or capability contracts.

## 8. Scorecard

- source_of_truth_integrity: 5
- boundary_clarity: 4
- invariant_enforcement: 4
- projection_discipline: 4
- integration_isolation: 3
- runtime_state_separation: 4
- ai_policy_separation: 4
- testable_architecture: 4
- change_safety: 4
- overall_stability: 4

## 9. Assumptions

- The reviewed editor and lifecycle paths are the primary mutation paths for node-scoped party ownership.
- The Playwright MCP failure is environmental and not caused by the application changes in this wave.
- No unreviewed external consumer depends on the removed metadata ids or rich meeting party payload.

## 10. Suggested ADRs

- ADR-0001: canonical project-party assignment ownership
- ADR-0002: Workbench party metadata is projection-only
- ADR-0003: use typed project-node references across module boundaries
- ADR-0004: Workbench node extension guardrails
