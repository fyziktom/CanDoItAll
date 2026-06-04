# Test Impact Inventory

Expected affected tests/slices:

- `ProcessRunAutomationDispatchServiceTests`
- `ProcessAutomationExecutionClientTests`
- `AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests`
- artifact-lineage smoke tests
- receipt metadata / required tool family tests
- process-filtered integration tests
- architecture tests guarding MAF/Tooling neutrality and no premature Core/driver projects

Codex must extend this inventory with exact test names found in the current branch before SB05 production movement.
