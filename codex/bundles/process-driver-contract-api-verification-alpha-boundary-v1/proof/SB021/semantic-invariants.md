# SB021 Semantic Invariants

## Invariant
- Invariant ID: SB021_INV_001
- Source raw note: inspect latest Codex work, repair missing gaps, improve the next architecture phase, plan stable Process Core with domain drivers, add broader areas, and prepare a bundle zip.
- Expected behavior: The .NET/Rust transcript verifier rehearsal is test-only and uses transcript references and diagnostic categories without command execution.
- Disallowed shallow implementation: a table-only closure, empty abstractions project, fixture-only positive check, broad token scan that blocks approved contract vocabulary, or runtime-like type name hidden outside tests.
- Failing-first test: N/A - process/non-production no behavior change beyond approved contract/test/doc/proof closure; negative proof is bundle://proof/SB037/transcripts/source-scans.txt.
- Passing test: bundle://proof/SB009/transcripts/passing-focused-contract-tests.txt and bundle://proof/SB037/transcripts/focused-process-driver-tests.txt.
- Changed source files: repo://src/CanDoItAll.Processes.Drivers.Abstractions/CanDoItAll.Processes.Drivers.Abstractions.csproj, repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.Abstractions contains immutable contracts only; repo://CanDoItAll.slnx includes the project; repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs blocks forbidden runtime surfaces.
- Red-team negative case: bundle://proof/SB042/transcripts/final-proof-index-red-team.txt rejects fake proof, missing artifacts, runtime-token drift, and broad token scans that hide approved contract-only vocabulary.
- Downstream dependency check: bundle://proof/SB037/transcripts/dotnet-build-no-restore.txt, bundle://proof/SB037/transcripts/dotnet-test-unit-no-build.txt, and bundle://proof/SB037/transcripts/source-scans.txt passed.

## Raw Note Closure Link
- Raw note owned: bundle://inputs/raw-request.md and bundle://traceability/01-input-coverage.md.
- Shipped behavior: contract-only driver abstractions, focused tests, compatibility docs, roadmap refresh, source scans, proof manifests, validators, and final bundle zip closure.
- Source proof: repo://src/CanDoItAll.Processes.Drivers.Abstractions and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs.
- Test proof: bundle://proof/SB037/transcripts/focused-process-driver-tests.txt and bundle://proof/SB037/transcripts/dotnet-test-unit-no-build.txt.
- Shallow-pass trap: file existence without dependency/runtime-token tests would pass weakly but is rejected by the focused tests and source scans.
- Adversarial negative proof: bundle://proof/SB037/transcripts/source-scans.txt plus bundle://proof/SB042/transcripts/final-proof-index-red-team.txt.
- Semantic positive proof: bundle://proof/SB009/transcripts/passing-focused-contract-tests.txt.
- Anti-stub audit: bundle://proof/SB037/transcripts/source-scans.txt states no stubs in scoped process sources.
