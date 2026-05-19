# Assumptions And Risks

## Assumptions

- Existing public contracts should stay source-compatible unless tests prove a safe evolution.
- Focused partial/service splits are acceptable for P0 when they reduce active file risk without changing behavior.
- New projection and automation operations can use existing durable records and return explicit summaries without adding migrations.
- Browser proof is mandatory when rendered Blazor behavior changes; the continuation tab split triggered that proof.

## Critical Path Risks

- Subbundle 01 is critical because behavior changes later should land in decomposed files, not expand the oversized files further.
- Subbundle 02 is critical because docs cannot move projection/automation out of P0 without tests proving operational paths.
- Subbundle 03 is critical because process-critical agent memory behavior must not silently degrade.
- Subbundle 04 is critical because docs must match the final implemented state.

## Validation Risks

- Full solution tests may be expensive; targeted Cognitive Memory tests are required at minimum.
- Projection rebuild behavior depends on optional projection adapter/provider state, so tests must cover success and failure through fakes and adapter-backed writes.
- Browser validation needs a running app when UI markup changes.
- Mechanical file splits can pass tests but still leave some maintainability debt; docs must distinguish closed P0 scope from beta hardening file-size work.

## Reopen Triggers

- Reopen subbundle 01 if build errors show a split broke accessibility, namespace, or Blazor partial behavior.
- Reopen subbundle 02 if projection rebuild or automation tests cannot prove explicit outcomes.
- Reopen subbundle 03 if process-critical memory still skips when required context is unavailable.
- Reopen subbundle 04 if docs claim a P0 item is complete without source/test proof.
