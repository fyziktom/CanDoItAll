# SB11 Semantic Invariants

## Invariant SB11_INV_001

- Invariant ID: `SB11_INV_001`
- Source raw note: "Run targeted provider/policy unit tests" and "Run full solution build."
- Expected behavior: Provider composition, process runtime tool policy, boundary architecture guards, and the full solution build all pass after Gate C.
- Disallowed shallow implementation: Running only compile or only a narrow facade test while provider/policy behavior can regress.
- Failing-first test: N/A - SB11 is runtime smoke proof with no production behavior change.
- Passing test: `bundle://proof/SB11/transcripts/provider-policy-unit-tests.txt`; `bundle://proof/SB11/transcripts/full-solution-build.txt`.
- Changed source files: SB11 adds proof only.
- Production assertions: `bundle://proof/SB11/source-assertions/runtime-smoke-large-screen-policy.txt`.
- Red-team negative case: Tool-policy drift, provider composition drift, or solution build breakage fails the provider/policy test set or full build.
- Downstream dependency check: SB12 can perform final red-team closure with fresh runtime smoke proof.

## Invariant SB11_INV_002

- Invariant ID: `SB11_INV_002`
- Source raw note: "Run process-filtered integration tests" and "Run hidden dependency scans."
- Expected behavior: Process-filtered integration tests pass, and hidden dependency scans show MAF/Tooling, Contracts, and dispatcher execution path did not regress.
- Disallowed shallow implementation: Treating focused SB10 tests as enough while the broader process integration filter or hidden dependency scans are skipped.
- Failing-first test: `bundle://proof/SB11/transcripts/process-filtered-integration-tests.timed-out.txt` records the initial broad run timeout before rerunning with `--no-build`.
- Passing test: `bundle://proof/SB11/transcripts/process-filtered-integration-tests.txt`; `bundle://proof/SB11/transcripts/hidden-dependency-maf-tooling-scan.txt`; `bundle://proof/SB11/transcripts/hidden-dependency-contracts-scan.txt`; `bundle://proof/SB11/transcripts/hidden-dependency-dispatcher-scan.txt`.
- Changed source files: SB11 adds proof only.
- Production assertions: `bundle://proof/SB11/source-assertions/runtime-smoke-large-screen-policy.txt`.
- Red-team negative case: A process integration failure, product dependency in MAF/Tooling, forbidden Contracts dependency, or dispatcher direct workspace call blocks SB11.
- Downstream dependency check: SB12 final closure can rely on a broad process integration pass rather than only targeted test slices.

## Invariant SB11_INV_003

- Invariant ID: `SB11_INV_003`
- Source raw note: "Record Browser Validation Analytics as N/A or large-screen PC only" and "Do not run small, medium, or mobile UI validation."
- Expected behavior: Browser validation remains N/A because no rendered UI changed, and proof paths contain no mobile/small/medium artifacts.
- Disallowed shallow implementation: Producing mobile/small/medium screenshots or omitting the browser-validation decision from runtime smoke proof.
- Failing-first test: N/A - this is a proof-policy check.
- Passing test: `bundle://proof/SB11/transcripts/no-forbidden-viewport-proof-path-scan.txt`; `bundle://proof/SB11/transcripts/git-diff-whitespace-check.txt`; `bundle://proof/SB11/transcripts/trailing-whitespace-source-scan.txt`.
- Changed source files: SB11 adds proof only.
- Production assertions: `bundle://proof/SB11/source-assertions/runtime-smoke-large-screen-policy.txt`.
- Red-team negative case: A mobile/small/medium proof artifact path or whitespace error fails the SB11 scans.
- Downstream dependency check: SB12 can close the bundle without adding browser proof unless UI changes are detected.
