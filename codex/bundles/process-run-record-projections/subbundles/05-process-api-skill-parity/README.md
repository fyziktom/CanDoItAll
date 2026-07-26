# process-api-skill-parity

## Status

- `Completed`

## Objective

- Update the authoritative SharedInfo Processes API skill to describe exactly the implemented record-backed history commands and their efficient usage.

## Success Criteria

- Current Commands lists every tested route once.
- Readback explains lightweight list/summary/analytics versus explicit deep diagnostics.
- Filters, cursor, maximum page size, freshness, completeness, narrative states, errors, and examples match the API contracts.
- Implemented routes are removed from “Not Current HTTP Commands.”
- Route appendix matches `ProcessesApi.cs`.

## Covered Inputs

- R10, R12, R14; N008.

## Prerequisites

- SB04 API tests and Architecture A3 pass.
- Write permission for `C:\repositories\CanDoItAll.SharedInfo`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\ProcessRunRecordsApi.cs`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration\ProcessRunRecordApiIntegrationTests.cs`
- `C:\repositories\CanDoItAll.SharedInfo\codex\skills\candoitall-api-processes\SKILL.md`

## UI Composition Contract

- N/A: documentation only.

## Deliverables

- Updated authoritative skill sections, examples, validation, and generated route appendix.
- Readback/diff evidence mapped to compiled route contracts.

## Dependency Impact

- SB06 closes API documentation parity. Any subsequent API change reopens this subbundle.

## Validation Depth

- Proof tier: `Standard`.

## Implementation Steps

1. Read implemented route mapping and passing integration tests.
2. Update commands, request/response examples, efficient readback guidance, errors, freshness/completeness/summary state, and not-current list.
3. Regenerate or manually verify the route appendix from source.
4. Search for stale claims and compare every route/payload field to code.
5. Record Architecture Checkpoint A4.

## Scope Exceptions

- Update the authoritative SharedInfo copy only unless the repository’s documented synchronization process explicitly requires another copy.

## Do Not Do

- Do not document planned or untested routes.
- Do not encourage deep detail for ordinary history.

## Acceptance Checklist

- [x] Skill route set equals implemented route set.
- [x] Examples use typed current fields and bounded paging.
- [x] Lightweight/deep distinction is explicit.
- [x] Stale “not current” claims are removed.

## Proof Required

- `rg`/diff readback against `ProcessesApi.cs`.
- Passing API integration test reference.

## Browser Validation Logging

- N/A: documentation only.

## Actual Proof And Progression

- Entry and closure gates: `Pass`.
- Source-to-skill readback covers `/runs`, `/runs/analytics`, `/{runId}/summary`, and `/{runId}/graph` exactly once in commands and the generated appendix.
- The skill documents compact versus deep reads, cursor/page limits, facts/narrative independence, completeness, analytics denominators/data watermarks, backfill timestamps, and privacy exclusions.
- `git -C C:\repositories\CanDoItAll.SharedInfo diff --check` passes.
- The integration host and serialization tests compile; live HTTP execution is explicitly environment-blocked by unavailable Docker/PostgreSQL and is not represented as a passing live request.
- Progression decision: `Completed; the authoritative skill matches the compiled/tested deterministic contract and records the operating caveats.`

## Progression Gate

- SB06 starts only when route-to-skill parity is exact.

## Reopen Triggers

- Any API route, query, response, maximum, error, or freshness behavior changes.

## Suggested Agent Prompt

```text
Implement SB05 only after API tests pass. Update only the authoritative SharedInfo skill and prove every documented route and field against source.
```
