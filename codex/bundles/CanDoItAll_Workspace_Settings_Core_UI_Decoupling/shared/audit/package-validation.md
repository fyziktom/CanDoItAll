# Package-only validation

**Date:** 2026-09-28. **Status:** passed for the package checks listed below.

## Executed locally

`python tools/validate_bundle.py` passed: JSON/metadata consistency, declared entry files, source IDs and Git-object syntax, repository-source URL pin format, local Markdown file targets, UTF-8 text-only inventory, and exact SHA-256 manifest coverage/integrity.

`python tools/test_tooling.py` passed **14 tests, zero failures, zero skips**. Eight tests use newly created disposable Git repositories or status fixtures to exercise unchanged and changed HEAD, uncommitted changes, a missing reviewed file, unavailable old history, unsafe paths, rename records and an invalid repository. Six tests check acceptance of the sealed package and rejection of content tampering, a broken local link, an unknown source ID, an unsealed file and a dummy binary-extension file. No real font was created or included.

Only synthetic test repositories receive test commits/configuration. Neither utility edits the application's Git state; the drift utility has no fetch/checkout/stage/build operation. Tests and validation do not require the original input ZIP or a CanDoItAll checkout.

The final ZIP was checked with the ZIP CRC/integrity check and its extracted contents were validated against the same manifest. The external ZIP SHA-256 digest is supplied alongside the archive. No source snapshot, font, binary package or historical product test log is distributed.

## Not executed and not claimed

Application builds and tests: **NOT_RUN**.
Evaluated MSBuild and restored package graph: **NOT_RUN**.
Live production/sandbox browser interactions: **NOT_RUN**.
Actual dotnet-watch performance experiment: **NOT_RUN**.
Independent sibling implementation/build audit: **NOT_RUN**.
CanDoItAll repository portability/documentation gates: **NOT_RUN**; no checkout was modified.
Drift utility against a local CanDoItAll checkout: **NOT_RUN**; utility behavior was tested with disposable fixtures.

The source audit used connected GitHub reads, with coverage limits in the source register. Structural package checks do not re-fetch external URLs or certify their current contents. A full repository/behavior/performance conclusion cannot be inferred from these 14 utility tests.

## Reproduce

From the extracted bundle root with Python 3.10+:

```text
python tools/validate_bundle.py
python tools/test_tooling.py
```

Git is needed only for the disposable-repository tests and the optional drift utility. The test runner reports skips explicitly when Git is absent; that is not the zero-skip result recorded for this preparation. The validator excludes only its own manifest from the seal and never rewrites hashes or baselines.
