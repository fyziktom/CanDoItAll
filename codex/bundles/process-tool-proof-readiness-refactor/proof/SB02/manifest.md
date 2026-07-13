# SB02 Proof Manifest

## Changed File Hashes

- `520502dc113bff1b5025503107dfb8336631408c4d298865c84f81346f5beb7f` `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/AgentProcessReadinessEvaluator.cs`
- `7c9ecca5eb8c500de0347355fb30ba80ea85ff9c33a411232f57b482e550b1b9` `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `dd682fff1fd6dca8da08ca8358fc54e6d3ba11892165bf378f7d10b895bf7d75` `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`
- `cb6e5b350c560bf8c5593eb97895faba4d38a05bf82fe898092cff8e88ee04cd` `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`

## Proof Artifacts

- Passing transcript: `bundle://proof/SB02/transcripts/proof-transcript.log`
- Raw focused test log: `bundle://proof/SB02/transcripts/focused-tests.log`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/proof-transcript.log`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first: N/A - process readiness behavior is covered by an adversarial negative unit test that rejects an agent without Playwright/browser MCP capability.

## Test Names

- Test name: `Runtime_readiness_rejects_required_browser_tool_when_agent_lacks_playwright_mcp`

