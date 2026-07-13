# Assumptions And Risks

## Assumptions

- The simple Calculator app run should be a valid autonomous path through the default `software-delivery` templates.
- Existing process-template JSON is the source of truth for seeded definitions in the development runtime.
- Existing agent/tool policy code can express the needed separation without adding a new permission model.

## Critical Path Risks

- SB01 must be accurate; otherwise SB02/SB03 may harden the wrong path.
- SB02 must not collapse the process into a monolith; the user explicitly wants smaller subprocesses that can be tested independently.
- SB03 must produce actionable readiness failures; generic HR rejection would still leave managers unable to repair templates or agents.

## Validation Risks

- Real-run proof may still fail for provider availability, test-environment state, or project data unrelated to the process contract repair.
- Template tests can prove static contracts but cannot prove dispatcher behavior by themselves.
- Browser/UI proof can be unavailable until the fresh process reaches the Calculator app surface.

## Reopen Triggers

- Any fresh run repeats the same feature-child escalation loop.
- An architect-assigned step receives product mutation capability.
- A code implementation/repair step lacks product mutation capability.
- HR/readiness marks a semantically under-capable assignment as ready.
- 5032 starts with stale template hashes after rebuild/restart.
