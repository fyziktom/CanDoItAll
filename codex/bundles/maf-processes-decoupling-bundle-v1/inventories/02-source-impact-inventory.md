# Source Impact Inventory

| Source | Current role | Required action |
| --- | --- | --- |
| `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | Directly references Processes | Remove Processes reference in SB05 |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs` | Holds process tool builder inside MAF | Move logic to Processes module in SB04; delete old file in SB05 |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | Hard-codes process tool attachment | Add registered provider attachment; remove process builder fields |
| `src/CanDoItAll.AgentFramework.Maf/README.md` | Documents ProcessToolBuilder | Update to provider-based tool composition |
| `src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Registers process services | Register `ProcessAgentRuntimeToolProvider` |
| `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` | Already references AgentFramework Core/Models | Add Tooling project reference |
| `CanDoItAll.slnx` | Solution project list | Add Tooling project |
| `tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | Unit test references | Add Tooling and/or Processes if needed for new tests |
| `tests/CanDoItAll.Tests.Integration/*` | Runtime integration tests | Adjust only when behavior-preserving proof requires it |

## Strict Deletion Rule

Do not delete `MafAgentRuntime.ProcessTools.cs` until the replacement provider is registered and process tool parity tests pass.
