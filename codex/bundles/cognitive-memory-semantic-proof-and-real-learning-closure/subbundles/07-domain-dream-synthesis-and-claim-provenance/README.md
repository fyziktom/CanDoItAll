# Domain dream synthesis and exact claim provenance

## Status

- Status: `Ready`

## Objective

Replace source-map meta dream text with domain-useful internalized claims and fix aggregate provenance to use exact claim evidence.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Neuro/CognitiveMemoryNeuroFoundationEntities.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Load `CognitiveMemoryClaimEvidenceLinkRecord` in quality support and expose claim-to-evidence mappings.
- Create dream claim units with source maps limited to the exact claim evidence links or exact source claim support.
- Replace `source claims are described` style text with canonical domain statements built from subject/predicate/object/condition/caveat slots.
- Keep diagnostics such as support counts, source map count, and confidence outside user-facing canonical memory text.
- Add validator checks for negation, numeric/time/condition mismatch, and contradiction at claim level.
- Add tests proving unrelated evidence anchors are excluded from a claim source map.

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

- Extend support loader and internal support records to include claim evidence links.
- Change `CreateClaimUnits` to associate each source claim with its exact evidence support.
- Build a domain claim synthesizer that can output action/constraint/fact/failure claims without source-map meta text.
- Add red-team tests for misleading broad provenance and meta-text aggregates.

## Do Not Do

- Do not produce aggregate text containing `source claims`, `mapped source claims`, `supported by N`, or similar diagnostic phrases.
- Do not attach every record source map to every claim from that record.
- Do not use claim count as a substitute for entailment support.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB07/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB07/semantic-invariants.md`.
- Completed: `bundle://proof/SB07/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB07/transcripts/passing.txt`.
- Completed: `bundle://proof/SB07/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB07/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Domain dream synthesis and exact claim provenance. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.
