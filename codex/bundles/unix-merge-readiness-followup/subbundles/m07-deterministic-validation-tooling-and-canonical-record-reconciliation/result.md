# Subbundle result — M07

## Anchor

- Repository commit: `386d8beb6038035f89a9a6961ec017d8213879a5` with accepted M00-M06/C1/C2 working-tree changes
- Dependency mode: package
- Host: Windows x64; SDK `10.0.303`
- Runtime catalog version: `2026-08-12.1`

## Implemented behavior

`Test-RuntimePortability.ps1` now builds through an explicit `-BuildOnly` mode and writes a durable build stamp containing repository commit, build-input fingerprint, configuration, dependency mode, selected SDK, catalog version, and exact assembly hashes. `-SkipBuild` validates every field before invoking a test process and fails closed on a missing or stale stamp.

The versioned JSON catalog replaces bare method-name selection with explicit class scopes and a fully qualified browser test. Guarded TRX parsing requires the exact discovered and passed counts: Unit 422, Integration 45, and Browser 1. Duplicate scopes, missing FQNs, zero tests, failures, and count drift are rejected.

The runner self-test covers stale source, wrong dependency mode, wrong commit, changed assembly hash, duplicate catalog entry, missing FQN, and zero-test output. CI now performs one stamped build and consumes it through `-SkipBuild`.

Runtime/process inventories, all three source-reference manifests, source hotspots, changed contracts, and Core C4/B07/R4 handoffs now agree on the current local Windows/Linux evidence. Genuine macOS and hosted-workflow execution remain explicitly unproven.

The portability scan was regenerated only after reviewing the protected source delta. The review found 1,129 added and 607 stale fingerprint occurrences, primarily in the governed MCP, Workbench, Manager, Docker, process-host, and executable-locator surfaces. The reviewed baseline now contains 13,562 occurrences and immediate enforcement passes.

## Commands and results

| Scope | Result |
|---|---|
| Runner negative fixtures | PASS, 7/7 |
| CI catalog contract tests | PASS, 3/3 |
| Targeted Unit Release build | PASS, 0 warnings/errors |
| Documentation validation | PASS, 172 maintained Markdown files |
| Static portability scan | PASS, 5,036 files and 30,195 findings classified |
| Reviewed portability baseline | PASS, 13,562 reviewed executable-source findings unchanged |
| Canonical manifest reconciliation | PASS, 58 shared, 53 Core, and 182 Runtime references; no missing paths |
| Legacy portability bundle structural validator | PASS in applicable portable mode, 0 warnings/errors |
| Bundle checksum verification | PASS after final M07 content generation |
| `git diff --check` | PASS; existing line-ending advisory only |

## Validation reuse/invalidation

- Invalidated keys: validation runner/stamp, runtime catalog/counts, CI build/test sequencing, portability baseline, and canonical bundle records.
- Reused evidence: M01-M06/C1/C2 product behavior and architecture proof.
- Reason reuse is valid: M07 product-source changes are limited to test contract cleanup; the other mutations are validation tooling and documentation. The affected CI contract tests passed and no product full suite was authorized.

## Residuals

The original portability bundle remains a portable template containing `{{REPO_ROOT}}`; its prepared/materialized validator mode therefore rejects it by design. Portable structural validation passes. Genuine macOS and hosted CI proof remain deferred and are not inferred from local Linux evidence.

## Decision

`GO`

## Next eligible subbundle

M08
