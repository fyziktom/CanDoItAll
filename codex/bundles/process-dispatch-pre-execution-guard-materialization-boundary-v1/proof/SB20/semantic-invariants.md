# SB20 Semantic Invariants

- Invariant ID: `SB20-INV-001`
- Source raw note: Final red-team must close this bundle and leave future driver readiness as documentation only.
- Expected behavior: Closure records local helper extraction, focused test proof, final scans, and the no-core/no-driver cutline.
- Disallowed shallow implementation: Closing without manifests, semantic invariants, final scans, and focused tests is rejected.
- Failing-first test: N/A process final closure gate; completed validator and red-team checks provide closure proof.
- Passing test: Final source assertions, build proof, and focused dispatch tests pass.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- Production assertions: The next cutline remains documentation-only and does not create Process Core or production driver APIs.
- Red-team negative case: Red-team proof rejects adding Process Core, driver production APIs, UI proof, or hidden side-effect helpers in this bundle.
- Downstream dependency check: Future work must start from documentation-only driver readiness, not from production driver APIs.

