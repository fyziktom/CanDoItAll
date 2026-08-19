# Validation strategy — avoid repeated 7,000-test runs

## Core principle

Validation cost is proportional to the changed contract, not to repository size. Full-suite execution is a scheduled confidence checkpoint, not a reflex after every edit.

## Tier 0 — edit loop

After each implementation edit:

- compile only the directly affected project when compilation feedback is needed;
- run one or more exact fully qualified test names/classes;
- run static/source-contract checks for scripts, workflows, Compose, manifests, or source generators;
- do not restore/build the solution repeatedly.

## Tier 1 — subbundle closure

Before closing a subbundle:

1. build the affected production and test projects once;
2. run the subbundle's FQN/category test set with `--no-build --no-restore`;
3. run negative/failing-first tests for the corrected bug;
4. update the invalidation ledger.

No full suite is allowed at individual M00–M07 closure unless a named NO-GO requires it.

## Tier 2 — grouped checkpoint

### C1 after M01–M03

- one clean package-mode Release solution build on the primary Windows host;
- affected persistence/process/FileTools tests;
- complete runtime portability Unit + Integration + Browser gate;
- optional full stable Windows suite **once** because M01–M03 alter shared persistence and process ownership.

If the full suite fails, fix only the failing project/test and defer the aggregate rerun to M08 unless the fix changes a different shared contract.

### C2 after M04–M06

- build once on Windows and once on Linux/package mode;
- complete runtime portability gate on both hosts;
- Docker focused tests and local MCP fake-server tests;
- **no full stable suite**.

## Tier 3 — final local candidate M08

Run exactly once on the frozen candidate:

- full stable Windows suite;
- full stable Linux suite;
- complete runtime portability gate on both;
- clean package-mode publish/start/restart;
- Docker local-stack smoke;
- migration compatibility fixtures;
- static portability and artifact redaction scans.

Do not rerun the full suites after documentation, checksums, indexes, or evidence-only changes. Re-run only if production source, test infrastructure, generated code, dependency anchors, build properties, or runtime configuration changes.

## Build-stamp requirement

`Test-RuntimePortability.ps1 -SkipBuild` must refuse execution unless a durable build stamp matches:

- repository commit/working-tree source fingerprint;
- build configuration;
- package versus explicit local-source mode;
- SDK version;
- selected assembly paths and SHA-256 hashes;
- Components/FileTools dependency anchors where applicable.

## Test catalog requirement

Replace bare method-name filtering with a versioned catalog containing fully qualified names or stable traits. The runner must:

- reject duplicate entries;
- prove every catalog entry was discovered;
- reject unexpected zero-test results;
- update expected counts only through an explicit reviewed catalog change;
- report per-scope and aggregate counts.

## Invalidation ledger

Every subbundle records changed files/contracts and the minimal affected test scopes. A later subbundle may reuse prior evidence only when none of its invalidation keys changed.
