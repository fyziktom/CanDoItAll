# 06-probe-and-recall-loop

## Status

- `Ready`

## Objective

Close the loop between probes, recall, feedback, repair candidates, and consolidation.

## Required Edits

- Pass projection collection/profile/embedding options through probe ask requests.
- Make probe feedback repair candidates visible in review and consolidation flows.
- Add regression tests for incorrect/missing-source probe feedback.

## Closure Proof

- Probe recall trace includes Qdrant vector search when projection options are supplied.
- Probe feedback creates a review item and regression case with traceable correction text.
