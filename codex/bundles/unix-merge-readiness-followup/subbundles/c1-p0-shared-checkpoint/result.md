# Checkpoint result — C1

## Anchor

- Repository commit: `386d8beb6038035f89a9a6961ec017d8213879a5` with accepted M00–M03 working-tree changes
- Dependency mode: package
- Windows SDK: `10.0.303`

## Aggregate result

MR-P0-001 through MR-P0-003 are closed. The package graph was restored after the explicit source-mode validation so stale source-mode assets could not contaminate the checkpoint. The subsequent Release solution build resolved only package dependencies and completed with zero warnings and zero errors.

The focused M01–M03 tests passed: 23 Unit cases and one PostgreSQL migration case. The complete runtime portability catalog passed 422 Unit, 33 Integration, and one Browser case.

The one authorized stable Windows execution reached the command's 15-minute ceiling after the Components assembly reported 955/960. Its five failures were in unchanged component tests outside the invalidated persistence, dependency, and process-lifecycle surfaces. An exact five-test rerun under normal OS access passed one and reproduced four unrelated component defects. Per the validation strategy, the aggregate was not rerun at C1 and remains scheduled for the frozen M08 candidate.

## Review

- Architecture: plan versioning remains in Builder/Persistence, dependency switching remains centralized in MSBuild, and process ownership remains behind the Core process boundary.
- Security: dependency source mode fails closed on exact clean commits; executable identity remains exact; no new secret-bearing diagnostics or shell interpolation were introduced.
- Lifecycle: every M03 termination path targets the persisted ownership boundary and verifies it empty; caller cancellation cannot abandon cleanup after it starts.
- Static analysis: the prior M01–M03 governed CodeAnalytics snapshots have no blocking findings.

## Residuals

- Four pre-existing/unrelated Component tests remain failing and are not changed in this bundle.
- The stable-suite aggregate must run again only at M08 after the candidate is frozen.
- Actual macOS execution remains deferred to M09.

## Decision

`GO`

## Next eligible subbundle

M04
