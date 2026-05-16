# QA Prompt For Self-Regulation Architecture Patch

You are a senior architecture QA reviewer. Review the patched `cognitive-memory-architecture-v2` bundle after Cognitive Self-Regulation was added.

Verify:

1. Self-Regulation is not described as consciousness, emotions, or autonomous ego.
2. Self-model is structured, scoped, evidence-backed data, not prompt persona.
3. Self-Regulation Assessment exists and includes self-model, competence, failure patterns, triggers, score trace, state, warnings, and required operations.
4. Attention Router consumes Self-Regulation Assessment.
5. Metamemory Answer Gate consumes Self-Regulation Assessment and Answer Posture.
6. Answer Gate cannot become looser than Self-Regulation without a new trace.
7. Humility triggers are defined and traceable.
8. Confidence reinforcement is evidence-based and reviewable.
9. Calibration Health aggregates include overconfidence, underconfidence, expected calibration error or equivalent, and profile versioning.
10. Professor Review is challenge/review input only and cannot directly mutate truth.
11. Score Geometry includes self-regulation score spaces and rejects scalar-only behavior.
12. Requirements, acceptance criteria, validation plan, subbundles, diagrams, contracts, and traceability are updated.
13. Existing contract consistency was audited across enum values, record references, score-space additions, and cross-file dependencies.
14. No path allows generated summaries, self-model, professor review, salience, prediction error, or probing feedback to directly become canonical truth.
