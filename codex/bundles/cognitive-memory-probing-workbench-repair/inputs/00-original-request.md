# Original Request

## User Request

> agree. use [$candoitall-bundle-workflow](C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md) to create followup bundle to improve plan of probing and then execute it and implement it. you must validate it. use those sample projects we loaded about AI Tap/Faucet and Glass factory.

## Preceding Agreement

The preceding analysis found that the original `cognitive-memory-architecture-v2` plan describes the right probing goal, but the live implementation is too thin:

- probing is not a real chat/workbench experience;
- feedback creates generic evidence but does not create an applicable repair candidate;
- review approval for probe feedback does not repair memory records;
- regression tests only check broad expected text and lack source/scope constraints;
- the UI only lists probe sessions instead of letting a user talk with memory, inspect trace/source evidence, and click to approve/correct.

## Hard Constraints

- Use the existing CanDoItAll bundle workflow.
- Preserve the original architecture-v2 bundle as source truth; create a follow-up bundle rather than editing that plan in-place.
- Validate with the realistic AI Tap/Faucet and Curacao Glass factory projects loaded by `realistic-project-memory-validation`.
- Do not let probe chat directly mutate canonical truth.
- Keep corrections review-gated and apply repairs only through explicit approved operations.
