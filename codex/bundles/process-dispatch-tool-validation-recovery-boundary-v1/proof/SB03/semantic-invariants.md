# SB03 Semantic Invariants

- Invariant ID: `SB03-SEAM-DESIGN`
- Source raw note: Continue smaller dispatcher isolation steps and do not rush Process Core.
- Expected behavior: Local helper seams exist under the process module and preserve dispatcher wrappers for later migration.
- Disallowed shallow implementation: Creating a Process Core project, production driver API, or helper that hides storage/state side effects.
- Failing-first test: N/A - process design proof; no standalone production behavior was added in SB03.
- Passing test: `bundle://proof/SB04/transcripts/gate-a-architecture.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessToolReceiptFacts.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRequiredToolValidationRules.cs`
- Production assertions: Helpers are module-local, typed, and side-effect free; dispatcher keeps orchestration.
- Red-team negative case: Architecture scan rejects Process Core and production driver-surface references.
- Downstream dependency check: SB04 architecture gate and SB08 required-tool parity consume the seam.
