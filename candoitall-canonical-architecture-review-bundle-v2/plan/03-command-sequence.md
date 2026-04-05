
# Command sequence

Run these in a real Codex/.NET environment.

## 1. Inventory and static map

```bash
python .agents/skills/canonical-model-review/scripts/solution_inventory.py --root . --output architecture/reviews/_inventory.json
python .agents/skills/architecture-drift-audit/scripts/solution_inventory.py --root . --output architecture/reviews/_inventory_drift.json
rg "LinkedPartyId|AssigneePartyId|RelatedParties|ResponsiblePartyId|OwnerPartyId|MaintainerPartyId|NodeKey" src tests
rg "ProjectObjectType|ObjectSubtype|ParticipantKind|WorkItemKind" src
rg "SyncGraphAsync|ReclassifyObjectAsync|ReparentObjectAsync|BuildPrerequisites" src
```

## 2. Phase 0 guardrails

```bash
dotnet build CanDoItAll.sln
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests|FullyQualifiedName~CrmHrCrossModuleIntegrationTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~CrossModuleResponsiblePartyPageTests"
```

## 3. Phase 1 semantics / ownership

```bash
dotnet build CanDoItAll.sln
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"
```

## 4. Phase 2 graph + lifecycle

```bash
dotnet build CanDoItAll.sln
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectWorkbenchSubtreeRecompositionIntegrationTests|FullyQualifiedName~ProjectStructureAgentIntegrationTests"
```

## 5. Phase 3 decomposition / cross-module alignment

```bash
dotnet build CanDoItAll.sln
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj
```

## 6. Phase 4 service decomposition / concurrency

```bash
dotnet build CanDoItAll.sln
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~CrmHrCrossModuleFlowTests|FullyQualifiedName~ProjectPartyAssignmentFlowTests|FullyQualifiedName~DatabaseSwitchWorkbenchPlaywrightTests"
```

## 7. Skill back-check after each phase

Use the skillset deliberately:

- `$canonical-model-review` after Phases 2 and 3
- `$feature-block-architecture-review` after each major code chunk
- `$architecture-drift-audit` after every phase

## 8. SharpTools focus points

Use SharpTools MCP to inspect:

- references to `ProjectObjectRecord`
- references to `ProjectPartyAssignment`
- references to `ProjectPartyAssignmentUpsertRequest.NodeKey`
- callers of `ReclassifyObjectAsync`
- callers of `ReparentObjectAsync`
- all code paths that write `ResponsiblePartyId`, `OwnerPartyId`, `MaintainerPartyId`
