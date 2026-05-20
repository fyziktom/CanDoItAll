# Adversarial Test Corpus Template

## Clustering Cases

- Paraphrase without exact topic/entity overlap.
- Bridge overmerge A-B-C where A and C should not aggregate together.
- High-fanout meaningful key with rare secondary evidence.
- Contradiction-only relation edge.
- Professor anchor topic alias expansion.

## Dream Cases

- Complementary sources requiring integrated synthesis.
- Unsupported claim with high token overlap.
- Valid paraphrased support with low exact token overlap.
- ProcedureMining must emit ordered constraints/steps.
- FailureLearning must emit trigger, symptom, consequence, mitigation, and evidence.

## Professor Cases

- Natural teaching without remember/correct keywords.
- Multi-turn explanation condensed into structured professor claims.
- Ambiguous target requires target selection before mutation.
- Direct anchor cannot assimilate itself or descendants-only support.
- Fading demotes direct quote memory after mastery.

## Recall Cases

- Many selected memories compressed into one task brief.
- Conflicting selected memories separated with caveat.
- Statement-level reference resolver returns only the requested claim lineage.
- Restricted/redacted lineage is hidden according to policy.
