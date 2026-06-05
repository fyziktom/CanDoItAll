# SB44 Semantic Invariants

- Invariant ID: SB44-INV-001
- Source raw note: Finish the execution/retry/provider boundary bundle only after implementation, validation, source hardening, and completed-bundle closure are all proven.
- Expected behavior: The dispatcher keeps the same execution-attempt order, retry eligibility, provider fallback selection, no-progress compression, recovery journaling, and finalizer recovery behavior while all extracted helpers remain module-local and no Process Core or process-driver production API is introduced.
- Disallowed shallow implementation: Marking the bundle complete without focused tests, no-core/no-driver scans, anti-stub scans, no-UI proof, provider write locality proof, or a completed validator transcript is rejected.
- Failing-first test: N/A - process non-production refactor with no behavior change; `bundle://proof/SB44/transcripts/final-closure-source-assertions.txt` provides adversarial source checks and `bundle://proof/SB42/transcripts/broad-focused-smoke-matrix.txt` provides behavior parity checks.
- Passing test: `bundle://proof/SB42/transcripts/broad-focused-smoke-matrix.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRepairCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAssignedAgentProviderRepairCoordinator.cs`, and helper files listed in `bundle://proof/SB44/manifest.md`.
- Production assertions: `bundle://proof/SB44/transcripts/final-closure-source-assertions.txt` proves no production Process Core references, no process-driver API symbols, no dispatch stubs, no UI drift, execution loop line count below target, and provider write locality.
- Red-team negative case: `bundle://proof/SB44/transcripts/final-closure-source-assertions.txt` rejects forbidden production symbols, placeholder code, UI-file drift, oversized loop files, and provider write leakage.
- Downstream dependency check: `bundle://reviews/03-next-cutline.md` identifies `Concurrency.cs` as the next local cutline and `bundle://reviews/04-known-unrelated-failures.md` records non-blocking pre-existing repository-state coupling.
