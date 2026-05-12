# Structured Input

## Business Goal

- Enable workflow authors to express simple route decisions directly in workflows and the workflow canvas.
- Keep route execution inside Microsoft Agent Framework workflows for this phase.
- Avoid building a custom orchestration rule engine now.

## Functional Scope

- Direct route.
- IF/ELSE route using a deterministic predicate over the previous node output payload.
- Switch case and default routes.
- Fan-out or multi-selection routes where one source can select multiple downstream targets.
- UI route builder in the workflow canvas edge inspector.
- Persistence/API validation and round-trip.

## Non-Functional Scope

- Strongly typed C# contracts.
- Safe evaluation only; no arbitrary code execution.
- Backward compatibility with existing workflow definitions.
- Clear test and browser proof requirements.
- ARTL handoff seam for future replacement.

## Out Of Scope

- Implementing ARTL parser or DSL editor.
- Full JSONPath language support.
- Production DurableTask/DTS routing proof unless already supported by the current host.
