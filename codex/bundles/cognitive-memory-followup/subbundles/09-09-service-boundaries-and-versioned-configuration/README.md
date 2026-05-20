# SB09 - Service boundaries and versioned configuration

## Status

- Status: `Completed`

## Objective

Refactor large monolithic services into testable collaborators and version algorithm configuration to reduce future Codex drift.

## Covered Inputs

- Current codebase remains large and easy for agents to simplify incorrectly.
- Only aggregate confidence calibration was meaningfully extracted in the last pass.
- Long services still obscure domain responsibilities.

## Prerequisites

- SB04-SB08 behavior changes completed and passing.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs

## Deliverables

- Extract clustering, dreaming, professor, and recall collaborators introduced by SB04-SB08.
- Register collaborators in DI.
- Add versioned algorithm configuration/options for thresholds, modes, proof rules, and lifecycle criteria.
- Add direct unit tests for collaborators independent of EF where possible.
- Update architecture docs/responsibility map.

## Dependency Impact

- Improves maintainability before SB10 closure.
- Prevents future agents from changing one large method and missing invariants.

## Validation Depth

- Each collaborator has direct tests.
- DI registration test covers new interfaces.
- No behavior regression in SB04-SB08 tests.
- Algorithm versions are asserted where persisted records are created.

## Implementation Steps

- Refactor behavior-preserving code after feature tests pass.
- Extract interfaces/classes with narrow responsibilities.
- Move thresholds/options into versioned config classes.
- Register all collaborators in module service collection.
- Update docs and test helpers.

## Do Not Do

- Do not refactor by only moving one small helper while leaving main services monolithic.
- Do not change behavior without rerunning SB04-SB08 tests.
- Do not hide configuration in magic constants without version labels.

## Acceptance Checklist

- Cluster planner, dream service, curator/professor lifecycle, and recall synthesis have extracted collaborators.
- Direct collaborator tests pass.
- DI/module registration test passes.
- Broad cognitive-memory unit tests pass.
- Architecture map documents ownership.

## Proof Required

- `proof/SB09/manifest.md`.
- Targeted collaborator tests and broad cognitive-memory test transcript.
- Source-level assertion for DI registration and version config.
- Responsibility map diff.

## Browser Validation Logging

- N/A unless public UI bindings change.

## Progression Gate

- SB10 can start because refactor tests and broad cognitive-memory tests pass.
- Services now have extracted collaborator boundaries, DI registration, versioned options, direct collaborator tests, and a responsibility map.

## Completion Evidence

- Proof manifest: `../../proof/SB09/manifest.md`
- Passing collaborator transcript: `../../proof/SB09/transcripts/passing-targeted-collaborator-tests.txt`
- Passing broad regression transcript: `../../proof/SB09/transcripts/passing-broad-cognitive-memory-tests.txt`
- Source assertion transcript: `../../proof/SB09/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `../../proof/SB09/transcripts/anti-stub-audit.txt`
- Responsibility map: `../../architecture/03-cognitive-memory-responsibility-map.md`

## Suggested Agent Prompt

Implement SB09. Refactor the cognitive-memory quality/professor/recall services into testable collaborators and versioned configuration.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
