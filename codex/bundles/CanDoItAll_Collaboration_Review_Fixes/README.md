# Collaboration review and bounded corrections

**Execution entry point:** [prompt.md](prompt.md). This is a follow-up to an existing implementation, not another extraction assignment.

The architecture is retained. Close the production draft-lifetime defect, sandbox behavior discrepancies and browser-test completion race described in [REVIEW.md](REVIEW.md), then prove the result using [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md). Do not start another module in this run.

Review provenance: `fyziktom/CanDoItAll`, branch `components-decoupling`, commit `e55a780b36e75db39a05b7400408d8267d0f32d5`, reviewed 2026-09-28. This is not a checkout requirement. The executor works on the user's actual current checkout and first checks relevant drift.

## Complete inputs are included

The entire previous handoff is preserved under [reference/original-assignment/](reference/original-assignment/README.md), including its original review notes, validation matrix and all shared v3 files. Treat its old extraction instructions and source inventory as historical context; do not recreate the already existing projects. The current correction prompt is the bounded execution task. Repository policy remains authoritative.

Read the extracted directory, not just a standalone copy of the prompt. The implementation's evidence record says the original module-specific companion documents were unavailable during the prior run. They are present here and must now be reconciled with the implemented behavior.

[Next-module decision](NEXT_MODULE.md) records TestLab as a subsequent candidate, not as authorized scope for this run. [sources.json](sources.json) records read coverage and pinned source links.

## Evidence status

The review used connected GitHub source/metadata reads, the user-supplied handoff and official Blazor/Playwright documentation. It did not execute C# builds, test discovery, application tests, browsers, PostgreSQL or watch benchmarks. Defect traces are source-derived and must be reproduced in the executor's C# test environment before repair. The previous evidence document's reported passing tests/timings are attributed records, not independently replayed results.

Archive hashes and package/link checks are documented in [PACKAGE_VALIDATION.md](PACKAGE_VALIDATION.md); those checks are not product validation.
