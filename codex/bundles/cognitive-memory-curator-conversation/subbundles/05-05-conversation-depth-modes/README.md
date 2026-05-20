# 05 Conversation Depth Modes

## Status

- State: `Completed`
- Critical foundation: `Follow-up feature`

## Objective

Add short, medium, and long curator conversation depth modes that control both curator reply detail and the amount of recall/aggregation input used when conversation turns produce memory improvements.

## Covered Inputs

- `R-010`
- `SRC-006`
- Raw note: response length should control how much knowledge is used for aggregation.

## Prerequisites

- Subbundle 02 closure gate passed.
- Subbundle 03 closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryCuratorConversationService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.Curator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryCuratorTab.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CognitiveMemoryPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryAdvancedPersistenceModelTests.cs`

## Deliverables

- Strongly typed `CognitiveMemoryCuratorConversationDepth` contract.
- Session, turn, and captured-improvement depth persistence.
- Depth-specific recall budgets and prompt guidance in the curator service.
- Depth metadata on recall traces and captured improvement provenance.
- Response length selector in the Curator tab.
- Unit, component, integration, migration, EF pending-model, build, and browser proof.

## Dependency Impact

- Subbundle 04 final closure depends on this follow-up because the original bundle was reopened by `SRC-006`.
- Depth modes reuse the existing curator service contract and do not change normal probe/review approval behavior.

## Validation Depth

- Focused unit coverage for prompt, recall budget, trace metadata, and capture metadata.
- Component coverage for the selector and selected-mode transcript display.
- EF model and migration checks for persisted depth.
- Browser proof in desktop and narrow viewports.

## Implementation Steps

1. Add a strongly typed conversation-depth enum to curator contracts.
2. Persist depth on sessions, turns, and trusted captures.
3. Replace UI-owned recall budgets with service-owned depth profiles.
4. Add depth-specific reply guidance to both direct LLM and agent prompts.
5. Add a response length selector to the Curator tab.
6. Add focused tests and migrations.
7. Re-run EF pending-model checks, build, browser proof, and completed bundle validator.

## Scope Exceptions

- Real microphone/audio-provider proof remains covered by subbundle 03 and is still environment-dependent.

## Do Not Do

- Do not make response length a stringly typed UI-only setting.
- Do not let the UI decide persistence or aggregation policy.
- Do not weaken normal manual review behavior outside trusted curator mode.

## Acceptance Checklist

- Depth selector exposes `Short`, `Medium`, and `Long`.
- Active session depth is part of session matching.
- Curator recall budgets increase from short to long.
- Prompt instructions differ by depth.
- Turn and capture records store depth.
- EF migrations exist for SQLite and PostgreSQL.
- Browser proof shows the selector without layout overlap.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter CognitiveMemoryAdvancedServicesTests --no-restore`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter CognitiveMemory --no-restore`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter CognitiveMemoryAdvancedPersistenceModelTests --no-restore`
- `dotnet ef migrations has-pending-model-changes --project src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj --startup-project src\CanDoItAll.Web\CanDoItAll.Web.csproj --context AppDbContext`
- `dotnet ef migrations has-pending-model-changes --project src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project src\CanDoItAll.Web\CanDoItAll.Web.csproj --context AppDbContext`
- `dotnet build CanDoItAll.slnx --no-restore`
- Browser route `/cognitive-memory?projectId=aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`, desktop and narrow viewport screenshots.

## Browser Validation Logging

- Record route `/cognitive-memory`.
- Record the Curator tab click, response length selector assertion, `Long` selection, viewport sizes, screenshot paths, and result.

## Progression Gate

- Pass only when depth behavior is proven at the service, persistence, UI, EF model, and browser levels.

## Suggested Agent Prompt

Implement subbundle 05 only. Add strongly typed curator conversation depth modes, move recall breadth policy into the curator service, update the UI selector, run focused tests and browser proof, and update the bundle closure evidence.
