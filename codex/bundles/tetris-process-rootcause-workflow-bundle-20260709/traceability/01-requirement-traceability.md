# Requirement Traceability

| Requirement | Source inputs | Owning subbundles | Source surfaces | Proof required |
|---|---|---|---|---|
| R01 Branch-aware receipt rules | `bundle://02-root-causes.md`, `bundle://03-source-map.md` | SB02 | `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`, `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` | Parser compatibility tests and object-rule launch preservation tests |
| R02 Branch-aware gate enforcement | `bundle://02-root-causes.md`, `bundle://04-target-architecture.md` | SB03 | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`, adapter receipt files | Accepted/repair branch gate tests and skipped-rule trace assertions |
| R03 Completion issue routing | `bundle://02-root-causes.md`, `bundle://05-codex-workflow-plan.md` | SB04 | Adapter result conversion and new route metadata | Incident route tests and retry-budget tests |
| R04 Runtime gate findings | `bundle://04-target-architecture.md`, `bundle://10-observability-and-ui-diagnostics.md` | SB04, SB10 | Managed artifact/evidence services | Source assertion and downstream repair-read smoke |
| R05 Receipt deduplication | `bundle://02-root-causes.md` | SB03 | Product and capability receipt gates | Duplicate diagnostic negative test |
| R06 Generic boundary repair | `bundle://07-domain-boundary-rules.md` | SB05, SB11 | `ProcessStepRecoveryInstructionBuilder`, adapter domain bridges | Forbidden-token architecture test and CodeAnalytics proof |
| R07 Template migration coverage | User request, `bundle://03-source-map.md` | SB06, SB07 | `repo://Templates/Processes/processes` | Migration/exemption inventory and template load tests |
| R08 Acceptance criteria matrix | `bundle://02-root-causes.md`, `bundle://08-acceptance-criteria-matrix.md` | SB08 | Project-structure process flow and artifacts | Calculator/Tetris-like matrix fixtures |
| R09 .NET runtime lifecycle | `bundle://08-maf-wrapper-and-tool-lifecycle-notes.md` | SB09 | .NET workspace tool layer and process templates | Fake host run/stop/orphan tests |
| R10 Observability and operator UX | `bundle://10-observability-and-ui-diagnostics.md` | SB10 | diagnostics, traces, UI projection | Trace assertions and operator summary smoke |
| R11 Regression proof | `bundle://06-test-strategy.md` | SB00, SB11 | Unit/integration tests | Failing-first, passing, build, and final closure transcripts |
| R12 Real adapter responsibility extraction | `bundle://inputs/03-architecture-refactor-request.md` | SB12 | Adapter partial cluster, composition registration, focused unit tests | No adapter partials; adapter is a thin facade; direct collaborator tests and DI smoke pass |
| R13 Domain policy isolation | `bundle://inputs/03-architecture-refactor-request.md`, `bundle://07-domain-boundary-rules.md` | SB13 | Completion receipt matching, recovery guidance, runtime-owned driver composition | Generic code contains no domain branching; .NET policy contribution tests prove matching and non-matching behavior |
| R14 Compatible OpenAI package review and autonomous Tetris E2E | `bundle://inputs/03-architecture-refactor-request.md`, `bundle://06-test-strategy.md` | SB14 | Central package versions, Processes/Agents HTTP APIs, TetrisGame project structure, output root | Compatible update or explicit no-update evidence; automation-dispatch run with process-bound agent runs, tool receipts, artifact lineage, and provider usage |
