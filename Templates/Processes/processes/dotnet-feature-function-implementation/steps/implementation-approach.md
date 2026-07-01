# Decide implementation approach

Read the current-run feature intake artifact first. Use it as sufficient evidence to produce the implementation approach when product-source inspection is not available to this read-only architecture step. Acceptance criteria supplied in launch variables or inside the intake artifact are sufficient; do not invent or require `artifacts/process-runs/<current-process-run-id>/steps/feature-acceptance-criteria.md`.

Write the managed plan artifact to `artifacts/process-runs/<current-process-run-id>/steps/implementation-approach.md` before completing. Include that artifact path and the intake artifact path in `evidenceRefs`.

Identify canonical state ownership, UI/application/domain/infrastructure boundaries, the intended file set, and whether the feature needs an architecture decision record. Name any assumptions separately from blockers.

When visual target ImageAsset ids or media paths are in scope, inspect or analyze the target image before selecting the UI file set. The implementation approach must name how the visible layout will map to the source image, not just to text requirements.

For repair runs, plan against the inherited repair target instead of broadening scope. Name the failing proof, the product surfaces likely responsible, and the focused verification that must pass before handoff.

Return `Blocked` only when required upstream evidence cannot be read, canonical ownership is genuinely undecidable, or the requested scope must be split before implementation can start. Do not block only because there is no prior assistant prose, no product-file inspection, or no previous tool result beyond the current-run intake artifact.
