# Real embedding/ranker-backed cluster discovery

## Status

- Status: `Ready`

## Objective

Implement actual embedding/ranker-backed approximate clustering and rename lexical fallback honestly.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Rename the current lexical/alias approximate provider so it no longer claims to be embedding-backed.
- Add a true embedding candidate provider that injects and calls `ICognitiveMemoryEmbeddingProvider` or a semantic ranker provider.
- Make candidate discovery async if embeddings require async calls.
- Add vector similarity threshold, pair budget, privacy/access guards, caching, and deterministic unavailable-provider fallback.
- Add tests with a fake embedding provider where paraphrases cluster without shared exact keys or aliases, and unrelated text does not cluster.

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

- Split lexical signal extraction from vector/ranker scoring.
- Update DI to register real embedding provider when available and lexical fallback when unavailable.
- Record metrics distinguishing exact, lexical approximate, and embedding/ranker approximate pairs.
- Update cluster quality scoring so embedding similarity is not treated as source independence by itself.

## Do Not Do

- Do not keep `EmbeddingBacked` in a class name if it does not use embeddings.
- Do not rely on shared aliases for the positive embedding test.
- Do not merge restricted cross-project records through embedding similarity without access-policy checks.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB06/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB06/semantic-invariants.md`.
- Completed: `bundle://proof/SB06/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB06/transcripts/passing.txt`.
- Completed: `bundle://proof/SB06/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB06/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Real embedding/ranker-backed cluster discovery. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.
