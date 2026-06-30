# Decide implementation approach

Inspect existing project conventions before coding. Identify canonical state ownership, UI/application/domain/infrastructure boundaries, the intended file set, and whether the feature needs an architecture decision record.

When visual target ImageAsset ids or media paths are in scope, inspect or analyze the target image before selecting the UI file set. The implementation approach must name how the visible layout will map to the source image, not just to text requirements.

For repair runs, plan against the inherited repair target instead of broadening scope. Name the failing proof, the product surfaces likely responsible, and the focused verification that must pass before handoff.
