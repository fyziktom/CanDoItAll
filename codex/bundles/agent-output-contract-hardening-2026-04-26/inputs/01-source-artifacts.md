# Source Artifacts

## Repository Sources

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.Session.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.AgentFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProcessTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Execution.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.GovernedOutcomes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.GovernedRules.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ToolValidation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Models.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Package/API Sources

- `C:\Users\lucys\.nuget\packages\microsoft.agents.ai\1.0.0\lib\net10.0\Microsoft.Agents.AI.xml`
- `C:\Users\lucys\.nuget\packages\microsoft.extensions.ai.abstractions\10.0.0\lib\net10.0\Microsoft.Extensions.AI.Abstractions.xml`
- Microsoft Learn: `https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs`
- Microsoft Learn: `https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools`

## Search Notes

- `rg` could not run because `C:\Program Files\WindowsApps\OpenAI.Codex_26.422.1952.0_x64__2p2nqsd0c76g0\app\resources\rg.exe` was denied by Windows.
- `git grep` found no current use of `AgentRunOptions`, `ResponseFormat`, or `ChatResponseFormat` in the production Agent Framework integration.
- `AIFunctionFactory.Create(...)` is used for workspace and process tools, but no finalizer tool exists for process-step decisions.
