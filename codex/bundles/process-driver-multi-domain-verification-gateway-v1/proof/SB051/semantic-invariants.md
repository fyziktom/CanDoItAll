# SB051 Semantic Invariants

## Gate Q Invariant
- Requirement owned: `REQ-017`.
- Required behavior: package/source validation must prove that every alpha driver package is solution-bound, dependency-clean, runtime-free, source-backed by tests, and covered by the SB049/SB050 proof set.
- Disallowed shallow implementation: a status row, README-only claim, source-scan-only claim, unit-test-only claim, upstream-manifest-only claim, or report-only closure.
- Failing-first proof: `bundle://proof/SB051/transcripts/red-team-gate-q-package-source-shallow-proof-rejection.txt` rejects shallow package validation claims that omit build, focused unit, focused integration, source/dependency scan, or upstream manifests.
- Passing proof: `bundle://proof/SB051/transcripts/gate-q-focused-package-unit-tests.txt` and `bundle://proof/SB051/transcripts/gate-q-focused-package-integration-tests.txt` verify package behavior and process read-only adapter integration.
- Source proof: `bundle://proof/SB051/transcripts/gate-q-package-source-dependency-scan.txt` verifies solution membership, exact driver project dependency direction, no package references, no Process Core reverse dependency, no forbidden runtime tokens, no stubs, no high-confidence secrets, and no UI/media drift.

## Reopen Conditions
- Reopen if any alpha driver package leaves the solution or gains unapproved package dependencies.
- Reopen if dependency direction changes without a future compatibility gate.
- Reopen if Process Core gains a driver package reference or driver namespace dependency.
- Reopen if driver package source gains runtime host, registry, selector, DI/service collection, manager command, endpoint mapping, process execution, HTTP, EF, file, or directory behavior.
- Reopen if package validation can pass without build proof, focused unit proof, focused integration proof, source/dependency scan proof, red-team rejection, proof index, and upstream SB049/SB050 manifests.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `gate-q-solution-build-no-restore.txt` | Build proof | Solution build succeeds with 0 warnings and 0 errors. |
| `gate-q-focused-package-unit-tests.txt` | Behavioral proof | Driver package, README sample, gateway, observation aggregation, and contract boundary tests pass. |
| `gate-q-focused-package-integration-tests.txt` | Integration proof | Process transcript/runtime read-only adapter and runtime-evidence source integration tests pass. |
| `gate-q-package-source-dependency-scan.txt` | Source/dependency proof | Driver packages are solution-bound, dependency-clean, runtime-free, and Core has no reverse dependency. |
| `red-team-gate-q-package-source-shallow-proof-rejection.txt` | Adversarial proof | Rejects status-only, report-only, source-only, unit-only, and upstream-manifest-only package validation claims. |
| `gate-q-proof-index.txt` | Positive proof index | Verifies Gate Q build, test, scan, red-team, invariant, and upstream manifest artifacts. |
