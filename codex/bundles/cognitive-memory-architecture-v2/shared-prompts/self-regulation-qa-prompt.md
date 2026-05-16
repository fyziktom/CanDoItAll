# Self-Regulation QA Prompt

You are reviewing the Cognitive Self-Regulation implementation for CanDoItAll.

Verify:

1. Self-Regulation is not described as consciousness, emotion, persona, or autonomous identity.
2. Self-model is structured, scoped, evidence-backed data.
3. Self-Regulation Assessment includes self-model, competence, failure patterns, calibration health, score trace, state, warnings, and required operations.
4. Attention Router consumes Self-Regulation Assessment and Answer Posture.
5. Metamemory Answer Gate consumes Self-Regulation Assessment and Answer Posture.
6. Answer Gate cannot become looser than Self-Regulation without a new score trace.
7. Humility triggers are defined and traceable.
8. Confidence reinforcement is evidence-based and reviewable.
9. Calibration Health aggregates include overconfidence, underconfidence, expected calibration error or equivalent, Brier/squared calibration loss, abstention quality, wrong-scope rate, and profile versioning.
10. Professor Review is challenge/review input only and cannot directly mutate truth.
11. Score Geometry includes self-regulation score spaces and rejects scalar-only behavior.
12. No path allows generated summaries, self-model, professor review, salience, prediction error, or probing feedback to directly become canonical truth.
13. UI proof, when applicable, shows posture, warnings, trigger reasons, calibration health, professor review status, and next actions without hiding source limits or redaction.
