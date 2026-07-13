# SB05 Semantic Invariants

- Invariant ID: SB05-DOMAIN-LAUNCH-ISOLATION
- Source raw note: N005, N006, N008
- Expected behavior: Software-delivery/project-structure launch context stays isolated from generic runtime so non-software enterprise processes remain first-class.
- Disallowed shallow implementation: Embedding .NET, Blazor, project-structure, or AgentFramework assumptions directly into generic prompt or dispatcher paths.
- Failing-first test: N/A - process refactor preserves production behavior; adversarial negative proof is represented by boundary tests and dependency scans rather than an intentionally committed failing test transcript.
- Passing test: build transcript bundle://proof/SB07/transcripts/build-modules-processes.txt; focused runtime test transcript bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt; integration/e2e transcript bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt.
- Changed source files: repo://src/Processes/CanDoItAll.Processes.Application/ProcessPromptCompositionContracts.cs; repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverExecutionContracts.cs; repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs.
- Production assertions: Generic prompt tests cover business/supplier/reporting/quality/data prompts, while project-structure launch/subprocess e2e tests preserve software-domain behavior.
- Red-team negative case: A generic process prompt leaking .NET finalizer guidance or a Processes project referencing MAF fails tests or dependency scans.
- Downstream dependency check: SB07 e2e process launch and subprocess proof depend on this separation.
