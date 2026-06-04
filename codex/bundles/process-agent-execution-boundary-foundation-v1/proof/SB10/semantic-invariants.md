# SB10 Semantic Invariants

## Invariant SB10_INV_001

- Invariant ID: `SB10_INV_001`
- Source raw note: "Run dependency scans for MAF, Tooling, Contracts, and Processes."
- Expected behavior: MAF and Tooling remain product-module neutral, Contracts remains dependency neutral, and dispatcher execution-path code stays behind `IProcessAutomationExecutionClient`.
- Disallowed shallow implementation: Running only tests while skipping dependency scans that would catch reintroduced product references or direct workspace execution calls.
- Failing-first test: N/A - SB10 is a refactor checkpoint and does not change production behavior.
- Passing test: `bundle://proof/SB10/transcripts/maf-tooling-product-dependency-scan.txt`; `bundle://proof/SB10/transcripts/contracts-neutrality-scan.txt`; `bundle://proof/SB10/transcripts/dispatcher-direct-workspace-call-scan.txt`; `bundle://proof/SB10/transcripts/dispatcher-coupling-counts.txt`.
- Changed source files: Gate C adds proof only.
- Production assertions: `bundle://proof/SB10/source-assertions/gate-c-boundary-consistency-review.txt`.
- Red-team negative case: Any product-module reference in MAF/Tooling, forbidden dependency in Contracts, or dispatcher `workspaceService.` call fails the Gate C scans.
- Downstream dependency check: SB11 runtime smoke can start only because Gate C confirms the boundary did not regress after SB08-SB09.

## Invariant SB10_INV_002

- Invariant ID: `SB10_INV_002`
- Source raw note: "Run source-size review for new facade and dispatcher changed files."
- Expected behavior: Gate C records source-size risk for the facade, contracts, and touched dispatcher partials without starting a broad refactor.
- Disallowed shallow implementation: Declaring consistency without measuring the new facade/contracts size or acknowledging large existing dispatcher partials.
- Failing-first test: N/A - SB10 is a review checkpoint.
- Passing test: `bundle://proof/SB10/transcripts/source-size-review.txt`; `bundle://proof/SB10/transcripts/gate-c-unit-architecture-provider-tests.txt`; `bundle://proof/SB10/transcripts/gate-c-integration-boundary-lineage-tests.txt`.
- Changed source files: Gate C adds proof only.
- Production assertions: `bundle://proof/SB10/source-assertions/gate-c-boundary-consistency-review.txt`.
- Red-team negative case: A hidden large abstraction or unreviewed dispatcher expansion would be visible in the source-size transcript and require reopening before SB11.
- Downstream dependency check: SB11 can focus on runtime smoke instead of unbounded source cleanup.

## Invariant SB10_INV_003

- Invariant ID: `SB10_INV_003`
- Source raw note: "Confirm no Process Core or driver-pack implementation exists" and "Confirm no mobile/small/medium proof artifacts exist."
- Expected behavior: The bundle still has no premature Process Core/driver-pack implementation and no forbidden viewport proof artifact paths.
- Disallowed shallow implementation: Passing Gate C while quietly adding a core/driver project or generating small/medium/mobile proof artifacts.
- Failing-first test: N/A - this is a scope checkpoint.
- Passing test: `bundle://proof/SB10/transcripts/no-core-driver-project-scan.txt`; `bundle://proof/SB10/transcripts/no-forbidden-viewport-proof-path-scan.txt`; `bundle://proof/SB10/transcripts/full-solution-build.txt`.
- Changed source files: Gate C adds proof only.
- Production assertions: `bundle://proof/SB10/source-assertions/gate-c-boundary-consistency-review.txt`.
- Red-team negative case: A `CanDoItAll.Processes.Core`, driver-pack project, or proof path containing mobile/small/medium labels fails the Gate C scans.
- Downstream dependency check: SB11 and SB12 can close runtime/final proof under the original large-screen-only policy.
