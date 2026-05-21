# Maintainability refactor and options boundaries

## Status

- Status: `Ready`

## Objective

Refactor oversized services and centralize options so future Codex passes can modify behavior safely.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs

## Deliverables

- Split curator conversation responsibilities into session lifecycle, capture extraction, target resolution, trusted apply, professor response, and audit components.
- Split cluster planner into key extraction, candidate discovery, graph cohesion, quality scoring, and persistence components.
- Split dream consolidation into claim unit loading, grouping, synthesis, validation, provenance, and apply orchestration.
- Split recall brief composition into fragment extraction, task planner, statement composer, and lineage builder.
- Centralize options through DI/config and remove production-path direct `new CognitiveMemoryQualityAlgorithmOptions()` fallbacks where services are constructed by DI.
- Add architecture tests or source assertions for service responsibility boundaries.

## Dependency Impact

- Update downstream subbundles, tests, traceability, and proof artifacts if this subbundle changes contracts or service boundaries.
- Re-run prepared-stage validation if this README, requirements, or phase gates are edited.
- Preserve compatibility with existing persistence unless this subbundle explicitly requires schema changes.

## Validation Depth

- Add failing-first proof before production behavior changes.
- Add focused passing tests for the behavior and affected regression tests.
- Include source assertions that prove production behavior, not only tests.
- Include anti-stub audit and red-team negative cases.
- Use portable `repo://` and `bundle://` references only in proof artifacts.

## Implementation Steps

- Refactor behind existing interfaces first to preserve behavior.
- Add characterization tests before splitting large classes.
- Move deterministic helpers into focused internal services with unit tests.
- Update DI registration and module registration tests after every split.

## Do Not Do

- Do not perform a broad rename-only refactor.
- Do not change persistence schemas in SB09 unless needed by earlier behavior subbundles.
- Do not leave old large methods as pass-through dumping grounds with all logic still inside.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB09/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB09/semantic-invariants.md`.
- Completed: `bundle://proof/SB09/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB09/transcripts/passing.txt`.
- Completed: `bundle://proof/SB09/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB09/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Maintainability refactor and options boundaries. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.
