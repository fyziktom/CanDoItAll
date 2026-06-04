# SB04 Semantic Invariants

## Invariant SB04-RQ005

- Invariant ID: `SB04-RQ005`
- Source raw note: "Architecture guards before movement" and "Add/extend tests and scans before production movement starts."
- Expected behavior: Static tests now fail if the staged execution boundary design disappears, the SB02 inventory omits direct dispatcher calls, MAF/Tooling neutrality regresses, or a premature Process Core/driver-pack project appears.
- Disallowed shallow implementation: Relying on manual review only while allowing production movement to begin without executable guardrails.
- Failing-first test: N/A - no production behavior changed in this process guardrail gate; the new guardrails are validated by `bundle://proof/SB04/transcripts/process-boundary-architecture-tests.txt`.
- Passing test: `bundle://proof/SB04/transcripts/process-boundary-architecture-tests.txt`; test name `ProcessAgentExecutionBoundaryArchitectureTests`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`; hashes are recorded in `bundle://proof/SB04/transcripts/hashes.txt`.
- Production assertions: `bundle://proof/SB04/source-assertions/gate-a-guardrails.md`.
- Red-team negative case: A new `CanDoItAll.Processes.Core`, process driver pack, missing facade cutline, or missing dispatcher direct-call inventory would fail the new guardrail class.
- Downstream dependency check: SB05/SB06 may start only after these guardrails pass.

## Invariant SB04-RQ011

- Invariant ID: `SB04-RQ011`
- Source raw note: "Run deeper refactor reviews after SB03, SB07, and SB10."
- Expected behavior: Gate A records a refactor checkpoint after SB03 and before source movement.
- Disallowed shallow implementation: Skipping Gate A or treating it as prose while continuing into production changes.
- Failing-first test: N/A - no production behavior changed in this process guardrail gate.
- Passing test: `bundle://proof/SB04/transcripts/provider-tooling-architecture-tests.txt`; test name `AgentRuntimeToolProviderArchitectureTests`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB04/source-assertions/gate-a-guardrails.md`.
- Red-team negative case: MAF product-tool reference drift or Tooling product references fail existing architecture tests before movement continues.
- Downstream dependency check: SB05 is unblocked only because Gate A tests passed.

## Invariant SB04-RQ013

- Invariant ID: `SB04-RQ013`
- Source raw note: "Do not run small, medium, or mobile UI validation."
- Expected behavior: The guardrail test and proof-path scan reject proof artifact paths labelled mobile, small-screen, medium-screen, phone, or tablet.
- Disallowed shallow implementation: Hiding mobile screenshots in proof while reporting browser validation as N/A.
- Failing-first test: N/A - no production behavior changed in this process guardrail gate.
- Passing test: `bundle://proof/SB04/transcripts/large-screen-proof-path-scan.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB04/source-assertions/gate-a-guardrails.md`.
- Red-team negative case: A forbidden proof artifact path would fail `Bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts`.
- Downstream dependency check: SB11/SB12 must cite this gate when closing the large-screen-only policy.
