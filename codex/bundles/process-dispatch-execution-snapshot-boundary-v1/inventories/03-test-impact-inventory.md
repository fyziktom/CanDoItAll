# Test Impact Inventory

Existing tests likely impacted:

- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`

New tests to add:

- Process automation execution snapshot mapping tests.
- Client failure normalization tests.
- Dispatcher forbidden AgentFramework runtime type scan.
- Receipt observation helper tests.
- Required-tool family parity tests over process snapshots.
