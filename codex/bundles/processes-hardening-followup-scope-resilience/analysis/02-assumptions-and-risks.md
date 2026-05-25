# Assumptions And Risks

## Assumptions

- `Processes` owns runtime governance for process steps even when a step executor is an AgentFramework workflow.
- The process core is intended to support software delivery, business, finance, HR, operations, research, legal, governance, and other process types.
- PostgreSQL is now the only runtime database provider. Do not reintroduce SQLite-specific migrations or tests.
- Some runtime behavior is intentionally strict, but strictness must be aligned with modeled process semantics rather than accidental text heuristics.

## Critical Path Risks

- If workflow-backed role candidates continue to load no expected artifacts, workflow-backed process steps will bypass artifact contracts.
- If subprocess parent completion bypasses the finalizer, subprocesses remain a separate and weaker runtime path.
- If step scope remains prompt-only, architecture/research/planning agents can still mutate downstream deliverables.
- If blocked state is used for all negative findings, processes stop where they should select a repair/no-go branch.
- If downstream steps are blocked while upstream materialization is requested, they can be stranded unless explicit unblock logic exists.
- If artifact validation remains string-heuristic, generic process artifacts will be blocked or accepted incorrectly.

## Validation Risks

- Source-assertion tests alone are not enough. Codex must add behavioral tests that exercise production emitters and runtime transitions.
- Tests must include non-software process examples to prove the generic process runtime was not overfitted to Blazor/.NET.
- Browser/UI proof is only needed for red-team process simulations that actually launch browser-visible process scenarios.

## Reopen Triggers

- Reopen SB01 if any non-mutating step can mutate a product/external target through workspace tools.
- Reopen SB02 if workflow-backed or subprocess-backed process steps can complete without loading and validating their process artifact expectations.
- Reopen SB03 if a review step with a repair/no-go branch still becomes `Blocked` merely because the product needs repair.
- Reopen SB04 if downstream steps remain blocked after the upstream source step successfully materializes the missing artifact.
- Reopen SB05 if legitimate generic artifacts containing terms like `todo`, `not available`, `decision log`, or `markdown summary` are rejected as placeholders or wrong format.
- Reopen SB06 if the same failure fingerprint can trigger repeated executor attempts without new evidence, mutation, or decision.
- Reopen SB07 if process definitions can be published with ambiguous step boundaries or missing artifact/role contracts.
