# Self-Regulation Implementation Prompt

You are implementing Cognitive Self-Regulation for the `cognitive-memory-architecture-v2` bundle. Execute only the active subbundle and keep the workbook, execution report, and proof paths synchronized.

Read first:

1. `architecture/27-cognitive-self-regulation-layer.md`
2. `architecture/28-self-model-and-epistemic-identity.md`
3. `architecture/29-calibration-health-and-probing-training.md`
4. `architecture/30-professor-review-and-escalation.md`
5. `contracts/csharp/CognitiveMemory.SelfRegulationContracts.cs`
6. `plan/01-phase-plan.md`
7. The active subbundle README.

Core constraints:

- Do not model Self-Regulation as consciousness, emotion, or prompt persona.
- Use structured, scoped, evidence-backed records.
- Do not let self-model, professor review, salience, prediction error, generated summary, or probing feedback directly mutate canonical truth.
- Use score geometry for self-regulation assessment, answer posture, calibration health, and professor-review routing.
- Keep display confidence as derived rendering data only.
- Preserve access policy, redaction policy, mutation authority, review policy, and source truth hierarchy.

Before closure, prove:

- contract/model tests cover the active records and service boundaries,
- scalar-only behavior is rejected,
- negative tests cover policy bypass and direct truth mutation,
- trace records preserve evidence refs, algorithm/profile version, actor/model profile, and timestamp,
- browser-visible surfaces record Playwright route, viewport, actions, assertions, screenshot paths, and result when UI is affected.
