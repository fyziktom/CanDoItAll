# Current Hotspots To Recheck

- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `codex/bundles/process-driver-contract-prerequisites-verification-alpha-v1`
- `CanDoItAll.slnx`

## Hotspot Questions
- Is Core still dependency-clean?
- Are driver prerequisite tests still active and pointed at this bundle?
- Does the contract-only project exist without runtime behavior?
- Are denial modes executable in tests?
- Are audit/redaction facts mandatory?
- Is `.NET/Rust` still test-only?
