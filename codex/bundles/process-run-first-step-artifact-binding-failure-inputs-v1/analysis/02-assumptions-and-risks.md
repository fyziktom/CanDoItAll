# Assumptions And Risks

## Assumptions

- `http://localhost:5032` is the intended live instance.
- Run `9bbc0667-9d12-4506-ba81-654ef924cad6` is the run the user described.
- ChatGPT Pro will own the actual bundle completion and any repair implementation.

## Critical Path Risks

- The raw process API reports an invariant diagnostic count but does not expose the diagnostic list in the captured response.
- The direct managed artifact content was not fetched by storage path; the content is preserved through agent execution output and tool receipt instead.
- The later manager-chat execution is still waiting on a pending approval, so future API state may diverge from this snapshot.

## Validation Risks

- This is not a prepared-stage-valid bundle.
- Placeholder scaffold files outside `inputs/` were only lightly updated because the user requested input preparation only.
- Raw JSON payloads can contain large embedded strings; summaries should be checked against the raw files before implementation decisions.

## Reopen Triggers

- Re-query the APIs if the pending approval is decided, the process run is rerun, or another artifact is recorded.
- Re-query the APIs if ChatGPT Pro starts after the local instance has been restarted or reseeded.
- Re-query project structure if the QA evidence node or process-run output nodes change.
