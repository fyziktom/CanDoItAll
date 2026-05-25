# Assumptions And Risks

## Assumptions

- `processes-hardening` is the intended branch because `process-hardening` was not found.
- PostgreSQL remains canonical.
- The runtime must stay generic and support software, business, legal, finance, manufacturing, research, and other process types.
- Existing UI changes should be minimal unless typed step operation contract fields are added.

## Critical Path Risks

- Fixing tool policy without handling script side effects leaves the original scope-drift problem open.
- Fixing storage validation without a storage abstraction can create path coupling.
- Adding strict lint defaults too aggressively can block valid existing definitions.
- Adding typed operation contracts without migration/backfill can break imported definitions.

## Validation Risks

- Source-assertion-only proof is insufficient for runtime behavior.
- Failing-first tests must cover realistic process scenarios, not just string helpers.
- Browser/UI proof is required only if process editor UI behavior is materially changed.

## Reopen Triggers

- Reopen SB01 if downstream steps remain blocked after upstream artifact materialization.
- Reopen SB02 if lineage remains only in bounded `ExternalReferenceKey`.
- Reopen SB03 if non-mutating steps can mutate product files through scripts.
- Reopen SB04 if stale text mentions can create writable external target aliases.
- Reopen SB05 if malformed JSON in managed storage can pass validation.
- Reopen SB06 if workflow/subprocess output mapping remains kind/title-only.
- Reopen SB07 if missing own required artifacts can route to branch disposition.
- Reopen SB08 if typed operation contracts remain magic text only.
- Reopen SB09 if no-progress behavior resets across dispatcher restarts.
- Reopen SB10 if critical lint warnings remain advisory for high-risk definitions.
