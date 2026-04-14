# Review checklist

Use this checklist at every gate and final closure.

## Proof trust
- Are the claimed tests visible in `.trx` artifacts?
- Do the artifacts match the prose claims exactly?
- Were migration scripts regenerated after schema changes?

## Canonicality
- Do core types have exactly one dependency representation?
- Is old-format compatibility only at boundaries?
- Are runtime/UI paths free of single-dependency shortcuts?

## Database integrity
- Are representative child/runtime references protected by FKs?
- Does the DB reject duplicate unconditional dependencies?
- Are delete behaviors intentional and documented?

## Lifecycle
- Is one draft per definition enforced?
- Is one published version per definition enforced?
- Is `ActivePublishedVersionId` safe?
- Is version allocation conflict-safe?

## Side effects
- Are activity/search side effects durable or retryable?
- Can a command still report failure after commit solely because dispatch threw?

## Structure
- Did cleanup reduce responsibility concentration rather than just moving code?
- Were invariants preserved after structural changes?
