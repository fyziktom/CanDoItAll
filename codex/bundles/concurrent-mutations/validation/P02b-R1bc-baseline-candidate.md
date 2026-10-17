# Receipt-store baseline and narrow R1c repairs

Status: focused checkpoint closed with failures: 80 executed, 78 passed, two failed,
zero skipped. All four owning Release projects built after fixture import repairs;
final static validation and all 25 discoveries passed. All 22 performance rows remain
unexecuted because the focused regression gate failed. This candidate contains no
production JSON optimization, historical
receipt reader or C3 publisher. It does not replace the completed
[400-case result](P02b-R1c-activity-repair-candidate.md): 386 passed, 14 failed, no skips.

The native repair asserts the exact attachment-staging success notification in each
affected fixture, then excludes only that object from its no-new-success assertion.
All recovery, draft, access-denial and uncertain-cleanup assertions remain. The API
repair adds `Description` metadata to four enum schemas, their four exposed properties,
and the original proposal binding and its four properties. Every numeric enum value
is described. No XML documentation or coverage exception was introduced.

The [reviewed measurement design](../plan/P02b-R1bc-store-cost-remediation.md) selects
22 LongRunning rows: 12 serial, eight independent 2/4-workspace cases and two shared
workspace controls with four distinct runs. Each row needs a fresh testhost, one cold
observation, two warmups and seven measured cohorts. Expected build-backed discovery
is 22 aggregate and exactly one for each row's method/display filter. The aggregate
selection is never executed. Native/API regressions select another 57 + 23 cases.

The original seven-input harness transfer has SHA256
`399F184DE49D6B0D7B489D10364E439F276667B3E61EE92F41EACFAB7D642291`.
Its first Integration build exposed an ambiguous provider type import. The fixture
now imports only the required administration interface using an alias, matching the
real native session. The subsequent build exposed a missing Infrastructure namespace
for the existing physical path-policy factory; adding that import completed the
Integration build. Original failed build logs remain retained. The native repair
was also amended before execution to cover attachment staging inside its later
activity-failure loop. Both transfer identities are retained separately.

Evidence root:
`.artifacts/concurrent-mutations/validation/r1bc-harness-baseline-validation`.
The parent records the entire source input manifest, candidate source/binary identity,
exact discovery labels, per-row source/loaded-assembly hashes, process identity,
900-second owned process deadline, intended fixture roots and terminal cleanup result.
Rows stop on their first failed sample; incomplete rows are never counted as seven
measured observations. The shared PostgreSQL server is retained for the focused API
lane; performance rows use their own recorded file fixtures.

The physical-memory prerequisite was initially unavailable during the 400-case run.
After closure, observed free memory recovered to about 24.5 GiB; each measurement
must still pass its own unchanged headroom checks. This is characterization, not
capacity qualification. Sampled memory maxima can miss transients and logical payload
activities are not physical I/O; diagnostic corroboration remains mandatory. No
capacity, performance improvement, broad Stable or end-to-end closure is claimed.

The frozen source was
`fe86890fc30a58e6c6454fe68b3f7e5f6c3114ee89cd8452358a04678d1ab1cb`;
the selection manifest SHA256 was
`4F9F5DA808F9B544FFB470D57631628597CCC4C1C276A2B03E2F3970733771DF`.
The complete 8,599-file source input manifest was independently rehashed without
mismatches, SHA256
`896287825F426DC4C2A74C7E2F95C217768606C63A7E93929999CD63377C9443`.
The closed summary SHA256 is
`A5ED7E2D5E79BEC212CA53A8CE168D61C93D68950174ED47DD8D4DF1C5374EA5`.
All runtime labels, definitions, unique executions, source and binary identities
matched. Native passed 56/57; API documentation passed 22/23. The original runner
stopped after native failure; parent completed only the already-discovered API lane
on the same frozen candidate. No performance fixture or measurement was started.

The corrected native fixture reached a genuine presentation mismatch: activity or
lease uncertainty blocks dispatch in the host, but Send still renders enabled. A
separate send-blocked presentation value must disable only the action and preserve
draft editing. The remaining API family reports two execution-state properties
missing their numeric enum values. Those repairs require another candidate.

The final full scan covered 8,530 files and 36,528 findings without truncation,
SHA256 `4B7365EA37D30CB0D7060D92A6EAD7F797AC167E1607C9509EF368FE3B4AADFA`.
No baseline refresh was needed: final no-write enforcement passed with 15,467
unchanged allowances. Static self-tests passed 6 + 4 + 7, rendering passed 1,198
inputs, documentation passed 426 maintained files, and whitespace checks passed.
