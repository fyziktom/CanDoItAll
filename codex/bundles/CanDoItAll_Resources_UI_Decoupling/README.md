# Memory review and complete Resources UI slice

Entry point: [prompt.md](prompt.md).

Execute one combined assignment: first the bounded Memory result-origin correction,
then the entire Resources Registry/Browse surface and its real sandbox. Do not stop at
S0 and do not start another module after Resources.

Review branch: `components-decoupling`. Reviewed implementation and observed HEAD:
`5f7f8329e3e3e30bd1451fc72757e04847579754`. The review date is 2026-09-28 in
America/Curacao; the Memory commit's UTC timestamp is 2026-09-29. This is provenance,
not a checkout instruction. The preceding Scheduler fix is `51648a77...`; bundle archival
commits are not product changes. [EV01, EV02, EV03]

## Reading order

1. Current repository instructions, then [prompt](prompt.md) and [shared foundation](shared/README.md).
2. [Memory review](MEMORY_REVIEW.md) and [Resources design/source notes](RESOURCES_REVIEW_NOTES.md).
3. [Validation matrix](VALIDATION_MATRIX.md), [development loop](DEV_LOOP.md), and [proof status](PROOF_STATUS.md).
4. [Source register](SOURCES.md) and [package validation](PACKAGE_VALIDATION.md).

## Decision

Scheduler's two previous corrections are present. Memory has a real Contracts/UI/
Presentation boundary and seven-tab sandbox. Preserve it. One source-demonstrated gap
remains: a result already published under an old same-ID provider revision is not reclassified
when a subsequent snapshot discovers replacement or disappearance. S0 addresses that
without changing provider capabilities or discarding useful historical evidence.

Resources is the next bounded product workspace, not necessarily the smallest by raw file
count. It has two related tabs and mature metadata/file owners, unlike a whole Workspace,
Projects or Workbench rewrite. The complete slice includes file browsing, promotion and
reopening, not just Registry. It deliberately leaves Processes and Workbench extraction
for later. Current owner authority is a reason to preserve and wrap it, not to recreate it
inside the UI. [RS01, RS02, RS03, RS04, RS05]

## What this archive contains

All 22 shared-v3 files are inherited byte-for-byte. Their old audit/module map remains
historical. This archive adds the new executable prompt, bounded review/design notes,
test matrix, source provenance and package checks; it does not recursively include old
module bundles or product source snapshots.

The reviewer read connected GitHub source and implementation receipts. Product builds,
product tests, UI/browser reproduction and watch benchmarks were not run in the review
environment; `dotnet` was not present on PATH. See [proof status](PROOF_STATUS.md).
The new assignment requires its own current product evidence.

## Local package checks

```powershell
python tools/validate_package.py
python tools/test_package.py
python shared/tools/validate_bundle.py
python shared/tools/test_tooling.py
```

These utilities validate the handoff only. They do not fetch GitHub, compile C#, operate on
a repository checkout, or certify product behavior. Their tests use temporary copies.
