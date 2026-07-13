# SB07 Semantic Invariants

- Invariant ID: SB07-REGRESSION-ARCHITECTURE-PROOF
- Source raw note: N001, N002, N003, N004, N005, N006, N007, N008
- Expected behavior: The refactor preserves behavior and proves the runtime/dispatcher architecture is flexible through build, unit, integration/e2e, dependency, and anti-stub evidence.
- Disallowed shallow implementation: Closing the bundle with only narrative proof, without executable tests, changed-file hashes, or dependency-direction scans.
- Failing-first test: N/A - process refactor preserves production behavior; adversarial negative proof is represented by boundary tests and dependency scans rather than an intentionally committed failing test transcript.
- Passing test: build transcript bundle://proof/SB07/transcripts/build-modules-processes.txt; focused runtime test transcript bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt; integration/e2e transcript bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt.
- Changed source files: repo://src/Processes/CanDoItAll.Processes.Application/ProcessPromptCompositionContracts.cs; repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverExecutionContracts.cs; repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs.
- Production assertions: Module build, 130 focused unit tests, 4 host-backed integration/e2e tests, source/project dependency scans, and anti-stub audit pass.
- Red-team negative case: Any Processes-to-MAF reference, unformatted proof artifact, missing invariant id, or failed runtime test prevents completed-stage validation.
- Downstream dependency check: Final closure consumes all SB01-SB06 proof and records residual maintainability risks.
