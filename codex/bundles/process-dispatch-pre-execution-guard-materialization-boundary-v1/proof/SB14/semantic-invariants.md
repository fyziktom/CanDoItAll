# SB14 Semantic Invariants

- Invariant ID: `SB14-INV-001`
- Source raw note: Keep materialization side effects explicit while extracting fingerprint, journal, and rerun request helpers.
- Expected behavior: Fingerprints are order-stable and target-sensitive; rerun requests preserve target agent, dependency step, origin, and directive fields.
- Disallowed shallow implementation: A coordinator that hides rerun or journal side effects without preserving request data is rejected.
- Failing-first test: N/A process refactor; focused helper tests cover negative parity cases.
- Passing test: Focused fingerprint and rerun request builder tests pass.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: The dispatcher still owns orchestration while helper code preserves fingerprint and rerun request semantics.
- Red-team negative case: Source assertions reject order-sensitive fingerprints and incomplete rerun request payloads.
- Downstream dependency check: SB15 facade wiring consumes the same coordinator contract.

