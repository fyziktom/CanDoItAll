# Test Impact Inventory

## Directly Affected Tests

| Test area | Why affected | Required protection |
| --- | --- | --- |
| MAF capability composition tests | Tool attachment path changes | Add provider-composition tests with zero providers and fake providers |
| Process tool policy tests | Tool names and approval wrapping must remain stable | Explicit process tool name and approval tests |
| Static hardening tests | Source paths and strings change | Update assertions to new architecture; add stronger dependency guards |
| Process runtime integration tests | Process tools still need to appear under app composition | Runtime smoke after provider registration |
| AgentFramework execution tests | Capabilities and tool receipts may change | Verify no missing process capabilities |
| Plugin tests | They assert plugin assemblies do not depend on MAF | Ensure new Tooling project does not introduce reverse dependency |
| Playwright audit proof tests | May indirectly rely on process run tooling | Run targeted smoke only if UI/process proof is impacted |

## New Tests To Add

- `AgentRuntimeToolProviderArchitectureTests`
  - MAF csproj does not reference Processes.
  - MAF source does not contain `CanDoItAll.Modules.Processes`.
  - MAF source does not contain `MafAgentRuntime.ProcessTools.cs`.
  - Tooling project does not reference Modules.
- `MafAgentRuntimeToolProviderCompositionTests`
  - zero providers works;
  - fake provider attaches a tool;
  - duplicate tool names are deterministic and/or rejected;
  - provider exception includes provider type/name.
- `ProcessAgentRuntimeToolProviderParityTests`
  - exact tool names match inventory;
  - read tools are approval-free;
  - mutation tools are approval-wrapped by default;
  - suppressApprovalRequirements bypasses wrappers only where current behavior allows.
- `ProcessAgentRuntimeToolProviderAccessTests`
  - read denied throws the same semantic error;
  - write denied throws the same semantic error;
  - definition scope denied throws the same semantic error;
  - imported definition grants access;
  - deleted definition revokes access.

## Existing Commands

Run at minimum:

```powershell
dotnet build CanDoItAll.slnx
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "AgentRuntimeToolProvider|ProcessAgentRuntimeToolProvider|AgentRuntimeHardeningStaticRegression|AgentToolInvocationPolicy"
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "AgentFrameworkExecutionCapabilityFiltering|MafAgentRuntime|Process"
```

Adjust filters to real test names after implementation.
