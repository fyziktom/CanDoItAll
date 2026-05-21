# SB01 Semantic Invariants

## Invariant SB01-PRODUCTION-MATRIX-01

- Invariant ID: `SB01-PRODUCTION-MATRIX-01`
- Source raw note: Codex may skip required production behavior while gates pass; production-only signals must not be closed from enum, consumer, and manually seeded test proof.
- Expected behavior: Completed-stage validation fails a critical proof that names a production signal, state, record, or event unless both `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.*` include a production behavior artifact matrix with producer, consumer, lifecycle, and negative-test citations.
- Disallowed shallow implementation: Accepting `ProfessorAnchorAcceptedUse` because an enum exists, an evaluator consumes it, and tests manually seed it, without a production emitter or lifecycle path.
- Failing-first test: `FakeProof.AcceptedUseConsumerOnly` in `bundle://proof/SB01/transcripts/failing-first.txt`.
- Passing test: `ValidatorProof.PositiveFixtureStillPasses` in `bundle://proof/SB01/transcripts/passing.txt`.
- Changed source files: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` with hash `7D082597C99E690DB4C7152368BF4A128CAC754B085AF69CE1343E56845CB077`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` proves `requires_production_behavior_matrix`, `validate_production_behavior_matrix`, and completed-manifest matrix validation are present.
- Red-team negative case: `bundle://proof/SB01/transcripts/failing-first.txt` proves the consumer-only accepted-use fixture fails after hardening.
- Downstream dependency check: `bundle://proof/SB01/transcripts/passing.txt` proves the prepared bundle and complete positive validator fixture still pass.

## Invariant SB01-DREAM-META-TEXT-02

- Invariant ID: `SB01-DREAM-META-TEXT-02`
- Source raw note: Dream synthesis must not close by storing diagnostic evidence-count template text as domain knowledge.
- Expected behavior: Completed-stage validation fails critical semantic proof that treats dream evidence-count template text as the expected, shipped, or semantic-positive memory behavior.
- Disallowed shallow implementation: Accepting non-empty text such as `Conclusion: rollout safety is supported by N source-backed observation(s)` as useful synthesized knowledge.
- Failing-first test: `FakeProof.TemplateDreamMetaText` in `bundle://proof/SB01/transcripts/failing-first.txt`.
- Passing test: `ValidatorProof.PositiveFixtureStillPasses` in `bundle://proof/SB01/transcripts/passing.txt`.
- Changed source files: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` with hash `7D082597C99E690DB4C7152368BF4A128CAC754B085AF69CE1343E56845CB077`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` proves `DREAM_META_TEXT_PATTERN` and dream meta-text semantic validation are present.
- Red-team negative case: `bundle://proof/SB01/transcripts/failing-first.txt` proves the template-only dream synthesis fixture fails after hardening.
- Downstream dependency check: SB06 must still add production behavior tests; SB01 only prevents proof gates from accepting template text as positive semantic proof.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProfessorAnchorAcceptedUse` proof requirement | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` and `bundle://proof/SB01/transcripts/source-assertions.txt` enforce producer-proof citations | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` enforces consumer-proof citations | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` enforces lifecycle-proof citations | `bundle://proof/SB01/transcripts/failing-first.txt` proves consumer-only proof fails |
