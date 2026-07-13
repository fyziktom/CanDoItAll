# SB02 Semantic Invariants

- Invariant ID: SB02-ADAPTER-DECOMPOSITION
- Source raw note: N002, N004, N006
- Expected behavior: AgentFramework execution behavior remains intact while the former mega-file is split into named runtime-integration responsibilities.
- Disallowed shallow implementation: Moving code into arbitrary partials without isolating adapter, result conversion, grounding, subprocess, recovery, telemetry, and managed-artifact responsibilities.
- Failing-first test: N/A - process refactor preserves production behavior; adversarial negative proof is represented by boundary tests and dependency scans rather than an intentionally committed failing test transcript.
- Passing test: build transcript bundle://proof/SB07/transcripts/build-modules-processes.txt; focused runtime test transcript bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt; integration/e2e transcript bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt.
- Changed source files: repo://src/Processes/CanDoItAll.Processes.Application/ProcessPromptCompositionContracts.cs; repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverExecutionContracts.cs; repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs.
- Production assertions: The deleted mega-file is replaced by RuntimeIntegration files and focused runtime tests still pass.
- Red-team negative case: A deleted behavior path, missing recovery observer, or incomplete result conversion fails the focused adapter tests or build.
- Downstream dependency check: SB04 evidence policy and SB06 dispatcher cleanup use the split adapter responsibilities.
