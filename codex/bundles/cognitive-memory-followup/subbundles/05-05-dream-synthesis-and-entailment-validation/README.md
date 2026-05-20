# SB05 - Dream synthesis and entailment validation

## Status

- Status: `Completed`

## Objective

Finish dream mode so aggregates integrate source claims and validation rejects unsupported, copied, or mixed-topic claims.

## Covered Inputs

- Current dream synthesis picks representative claim text.
- Current validation relies heavily on token overlap.
- Modes are not structurally distinct enough.

## Prerequisites

- SB03 dream tests fail first.
- SB04 clustering quality available.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateConfidenceCalibrator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Add `ICognitiveMemoryDreamClaimSynthesizer` for deterministic integration of complementary claims.
- Add statement-to-claim segmentation and claim-level source maps.
- Add entailment/coverage validator stronger than simple token overlap, with deterministic lexical fallback and optional provider interface.
- Add synthesis-vs-copy quality gate.
- Add distinct output schemas for ProcedureMining, FailureLearning, ProjectNightly, and coverage/drive modes.

## Dependency Impact

- Feeds SB07 assimilation and SB08 precise lineage.
- Dream validation must not promote shallow/copy aggregates into stable memory.

## Validation Depth

- Complementary claim test must prove integrated statement includes details from multiple sources.
- Unsupported token-overlap claim must be rejected/reviewed.
- Representative-copy output must be review-only unless quote-preservation mode is explicit.
- Mode-specific tests must assert structure, not title strings.

## Implementation Steps

- Extract synthesizer and entailment validator collaborators.
- Produce aggregate claim records with precise source map ownership per synthesized statement.
- Add duplicate/near-duplicate detection for paraphrases.
- Calibrate apply confidence using validation quality and synthesis quality.
- Persist algorithm version update.

## Do Not Do

- Do not only remove diagnostic boilerplate.
- Do not set `StrongAccept` from source count alone.
- Do not call a representative source sentence a synthesized claim.

## Acceptance Checklist

- Dream aggregate text is not identical to any single source when synthesis is required.
- Each aggregate statement maps to source claims/evidence.
- Unsupported token-overlap claim is rejected or review-only.
- ProcedureMining emits ordered steps/constraints.
- FailureLearning emits trigger/symptom/consequence/mitigation/evidence fields or equivalent structure.

## Proof Required

- `proof/SB05/manifest.md` with transcripts.
- Targeted dream/validator/apply tests: `proof/SB05/transcripts/passing-targeted-dream-tests.txt` and `proof/SB05/transcripts/passing-dream-apply-regression-tests.txt`.
- Source-level assertion for synthesizer and entailment validator: `proof/SB05/transcripts/source-assertions.txt`.
- Anti-copy quality check evidence: `proof/SB05/transcripts/anti-copy-quality-check.txt`.

## Browser Validation Logging

- N/A unless dream results UI changes.

## Progression Gate

- SB07 and SB08 cannot close until statement-to-claim source maps are precise.
- If aggregate text is representative-copy output, SB05 remains incomplete.

## Suggested Agent Prompt

Implement SB05. Turn dreaming from representative extraction into validated claim synthesis with precise source maps.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
