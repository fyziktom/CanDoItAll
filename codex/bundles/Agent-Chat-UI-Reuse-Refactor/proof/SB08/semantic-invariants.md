# SB08 semantic invariants

## UIR-SB08-01 — Every live Agent consumer uses one presentation owner

- Raw note: "Migrate every existing agent consumer through the neutral presentation boundary, remove superseded duplication, prove dependency direction, and close architecture review without activating Simple Chats."
- Expected behavior: every discovered Agent, contextual and Process consumer renders neutral conversation presentation directly or through a purposeful Agent facade.
- Disallowed shallow implementation: create neutral DTOs while leaving each consumer's original card/thread/workspace/settings markup as the real production path.
- Source proof: `bundle://proof/SB08/consumer-migration.md` and the facade-to-neutral tag scan in `bundle://proof/SB08/source-guards.md`.
- Semantic positive proof: 81/81 cross-consumer tests and the CP2/CP3 real Agent UI proofs exercise production consumers.
- Adversarial negative proof: invalid opaque Agent handle keys throw, missing selections fail closed, and contextual late completions do not replace a newer selection.
- Anti-stub audit: zero matches in the new neutral/adapter surface.
- Downstream check: `CanDoItAll.Modules.Processes` builds successfully through `ChatWorkspacePanel`.

## UIR-SB08-02 — Dependency direction remains inward to neutral presentation

- Expected behavior: product modules may depend on Agent facades, Agent facades may depend on neutral presentation, and neutral presentation depends on no product/backend project.
- Disallowed shallow implementation: solve reuse by moving Agent types into Common, introducing a reverse neutral-to-Agent reference, or injecting a coordinator/service provider into neutral UI.
- Source proof: `bundle://proof/SB08/architecture-review.md` and scoped snapshot `snap-20260816142006-84a4f698`.
- Semantic positive proof: all four scoped projects build and CodeAnalytics reports the intended direct/reverse references.
- Adversarial negative proof: repository/source guards explicitly reject Agent/LlmChats/backend/persistence/service-location references under the neutral project.
- Downstream check: project inventory proves Processes and AgentFramework modules consume the facade without a neutral reverse dependency.

## UIR-SB08-03 — Phase 1 does not activate Simple Chat UI

- Expected behavior: current Agent UI remains the only production consumer; future Simple Chat seams remain inert presentation contracts/slots.
- Disallowed shallow implementation: add a Simple Chat catalog tab, source filter, context button, route, HTTP client or SSE client while claiming it is only preparation.
- Source proof: zero-match phase scans and the repository boundary validator.
- Semantic positive proof: CP3 UI shows Agent-only catalog/active tabs and current Agent behavior.
- Adversarial negative proof: phase-exclusion validator rejects forbidden Simple Chat activation patterns.
- Downstream check: SB09 terminal state remains `awaiting-user-agent-chat-regression`, not a Simple Chat rollout state.

## Production behavior artifact matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
|---|---|---|---|---|
| participant presentation | Agent participant mapper | neutral card/list/picker | rebuilt from current Agent state on render/parameter change | fail-closed selection tests |
| conversation presentation | Agent conversation/thread mappers | neutral rail/header/transcript/composer | rebuilt from loaded session and pending-send state | contextual stale-completion tests |
| active-chat presentation | Agent active-chat mapper | neutral active list | rebuilt from coordinator active handles | invalid opaque-key mapper test |

No production-only signal is manually seeded to prove this architecture closure.
