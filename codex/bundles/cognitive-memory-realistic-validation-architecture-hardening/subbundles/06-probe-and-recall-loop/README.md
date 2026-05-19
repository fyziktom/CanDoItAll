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

## Covered Inputs

- Probe recall lost vector projection options and returned `vector:projection-options-missing` even after projection setup.

## Prerequisites

- Probe ask contracts can carry projection collection/profile/embedding options without changing unrelated recall behavior.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs`

## Deliverables

- Probe ask and feedback flows that preserve projection options, expose recall trace stages, and create repair candidates when feedback identifies missing facts.

## Dependency Impact

- Qdrant operability and long-run validation depend on probe recall proving whether vector search participated.

## Validation Depth

- Unit tests must assert projection option propagation and feedback-driven repair candidate behavior.

## Implementation Steps

- Extend typed probe ask options, pass them to recall, capture trace stages, and connect feedback to review/consolidation repair paths.

## Do Not Do

- Do not report probe recall as successful when vector search was skipped because options were missing.

## Acceptance Checklist

- Probe ask accepts projection options.
- Recall trace distinguishes vector search from lexical-only fallback.

## Proof Required

- Focused advanced-service tests for projection option propagation and probe feedback behavior.

## Browser Validation Logging

- Record large-screen probe tab proof when projection controls or recall traces are surfaced.

## Progression Gate

- Proceed only when probe recall can prove vector participation or explicitly explain why it did not run.

## Suggested Agent Prompt

- Close the Cognitive Memory probe and recall loop by carrying projection options through probe asks and exposing traceable feedback repair candidates.
