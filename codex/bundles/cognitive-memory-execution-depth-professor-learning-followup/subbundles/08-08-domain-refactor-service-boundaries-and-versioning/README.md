# 08 Domain Refactor Service Boundaries And Versioning

## Status

- `Completed`

## Objective

- Refactor the repaired logic into maintainable services, contracts, migrations, and versioned algorithms so the codebase remains navigable for future Codex agents.

## Success Criteria

- Large services are split into focused collaborators with clear interfaces.
- New contracts/entities/migrations are versioned and backward compatible.
- DI registration and tests cover the new collaborators.
- Algorithm versions distinguish old shallow behavior from new composite/dream/professor behavior.
- No feature behavior regresses after refactor.

## Covered Inputs

- Current clustering, dreaming, curator, and recall services are large and hide multiple responsibilities.
- Large codebase increases Codex drift risk; service boundaries should reduce future simplification mistakes.
- New proof-depth skills require easier auditability.

## Prerequisites

- SB04-SB07 completed.
- All behavior tests passing before refactor begins.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs

## Deliverables

- Focused services such as cluster key provider/scorer, cluster graph builder, dream claim synthesizer, dream claim validator, aggregate confidence calibrator, curator anchor parser, professor assimilation orchestrator, recall brief composer, and provenance mapper.
- Updated DI registration.
- Additive migrations or configuration updates if new records are introduced.
- Updated tests proving behavior unchanged after refactor.

## Dependency Impact

- Improves maintainability and prevents future Codex agents from getting lost in large multipurpose classes.
- Blocks final closure because final proof must run against the refactored maintainable implementation.

## Validation Depth

- Architecture-quality refactor with full regression proof.
- No behavior weakening allowed.

## Implementation Steps

1. Inventory responsibilities inside each large service.
2. Extract collaborators one at a time with tests green between steps.
3. Ensure public contracts remain stable or migration path is documented.
4. Update DI and any serialization contexts.
5. Run unit/component tests and build.
6. Update architecture docs and execution report with the new boundaries.

## Scope Exceptions

- Deep UI redesign is out of scope unless required by changed service/API status display.
- Performance optimization beyond bounded clustering is out of scope unless tests expose a regression.

## Do Not Do

- Do not refactor by moving code into equally large helper classes without behavior clarity.
- Do not change behavior to make extraction easier.
- Do not leave old and new algorithms both active without explicit versioning/compatibility rules.

## Acceptance Checklist

- All cognitive-memory tests pass after refactor.
- Build passes.
- DI and serialization context compile.
- Architecture doc lists new collaborators and responsibilities.
- Execution report includes before/after responsibility map.

## Proof Required

- Full targeted cognitive-memory unit suite.
- Component tests if affected.
- `dotnet build` or solution build.
- Execution report refactor map.

## Browser Validation Logging

- If UI bindings changed: `/cognitive-memory` smoke on changed tabs.
- Large desktop screenshot for changed tabs.
- N/A if no UI-visible change.

## Progression Gate

- SB09 may start only after refactor is complete and all behavior tests still pass.
- If refactor introduces hidden shallow fallbacks, this gate fails.

## Suggested Agent Prompt

```text
Refactor the repaired cognitive-memory implementation into focused collaborators without weakening behavior. Keep tests green and update architecture proof.
```

## Execution Proof

- Responsibility inventory completed for clustering, dreaming, validation, aggregate apply, curator/professor lifecycle, recall synthesis, and reference resolution.
- Extracted `CognitiveMemoryAggregateConfidenceCalibrator` behind `ICognitiveMemoryAggregateConfidenceCalibrator`; aggregate apply now delegates confidence score, bucket, and stability policy to the collaborator.
- Registered the new collaborator in `CognitiveMemoryModuleServiceCollectionExtensions`.
- Versioned repaired algorithms as `quality-clustering-v2`, `quality-dream-v2-claim-synthesis`, `quality-aggregate-apply-v2-calibrated`, and `curator-conversation-v2-professor-anchor`.
- No migration was required; SB08 adds a stateless service boundary and uses existing persisted algorithm-version columns.
- Architecture responsibility map updated in `architecture/01-target-solution.md`.
- Validation passed:
  - `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemoryQualityFoundationTests|FullyQualifiedName~CognitiveMemoryAdvancedServicesTests" --logger "console;verbosity=minimal"`: `54/54` passed.
  - `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal"`: `176/176` passed.
  - `dotnet build CanDoItAll.slnx --no-restore`: passed with `0` warnings and `0` errors.
- Anti-stub audit passed: no `TODO`/`NotImplemented` in changed cognitive-memory quality/advanced production paths, no default reference display, no default bullet-wrapped recall statement, and no title-only recall grouping expression.
