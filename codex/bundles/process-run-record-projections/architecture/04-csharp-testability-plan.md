# C# Testability Plan

## Contract Tests

- Serialization round-trip and schema-version compatibility for every JSON value object.
- Disposition parsing and explicit rejection of manager-loop escalation as a terminal seed.
- Query validation, bounded page size, cursor/order stability, and filter combinations.

## Application Tests

- Completed, failed, and cancelled end-to-end assembly; reserved escalated assembler contract coverage.
- Attempts/repetitions, agent/workflow/subprocess IDs, duration, usage, cost, tools, and completeness.
- Idempotent replay and later-disposition replacement.
- Missing evidence produces flags, not invented values.
- Hard facts persist when narrative generation fails.

## Narrative Tests

- Claim/lease prevents concurrent ownership.
- Structured output success transitions Pending -> Generating -> Completed.
- Invalid structured output/provider failure transitions to Failed with masked diagnostics and retry metadata.
- No raw prompt, tool argument, secret, or provider response is logged/stored as an error.

## Persistence Tests

- EF model has unique run key and expected indexes.
- No foreign keys/navigation relationships.
- JSON fields deserialize through centralized options/source-generated context where appropriate.
- List filters/order/limit execute before payload materialization.
- Analytics aggregates from stored records.

## No-Deep-Hydration Tests

Fakes for runtime state, assignment, and execution-detail readers throw if called. Normal run-record list, summary, graph, analytics, manager/CRM adapter, and terminal project-node tests must still pass.

## API Tests

- List validation/filter/paging and cancellation.
- Summary found/not found.
- Analytics filters and stable typed response.
- Existing deep detail remains explicit and compatible.

## Closure

- Build affected projects.
- Run focused unit and integration filters.
- Build the solution.
- Review migration.
- Run bundle validators and architecture review gate.
