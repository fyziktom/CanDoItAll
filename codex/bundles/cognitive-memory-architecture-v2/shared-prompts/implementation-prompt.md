# Implementation Prompt

You are implementing one subbundle from `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture`.

Before editing code:

- Read the root README, `plan/01-phase-plan.md`, the active subbundle README, `analysis/03-prerequisite-refactor-decision.md`, and `traceability/01-requirement-traceability.md`.
- Confirm `00-prerequisite-boundary-gate` validates the target branch before starting any Cognitive Memory implementation. The current supplied code already contains the expected boundaries, but implementation must not assume that another branch does.
- Keep changes limited to the active subbundle and its declared dependency impact.
- Preserve source-of-truth boundaries: raw sources remain authoritative, durable memory remains authoritative over projections, and Qdrant/search/context packs remain rebuildable projections.
- Use strongly typed modes, ids, policy contexts, and result objects.
- Do not introduce fallback mechanisms that hide provider, source, or projection failures.
- Do not collapse Epistemic Drive into a simple scalar priority score.
- Human approval is required before external study or high-impact memory updates.
- All learning-derived canonical records and procedures require source refs.
- Preserve multi-dimensional evidence for knowledge gaps, learning proposals, and learning outcomes.

Expected output:

- List files changed.
- Explain the smallest correct design choice.
- Provide build/test/browser proof required by the subbundle.
- Update `reviews/01-execution-report.md` for the active subbundle.
