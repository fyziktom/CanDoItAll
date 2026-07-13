# SB02 Semantic Invariants

- Invariant ID: `SB02-readiness-sees-browser-requirements`
- Source raw note: `bundle://requirements/01-normalized-requirements.md`
- Expected behavior: Launch and dispatch readiness include typed required runtime tool names so missing browser proof capability is visible before work is accepted.
- Disallowed shallow implementation: Do not hide browser readiness behind generic role fit or component-local text.
- Failing-first test: `Runtime_readiness_rejects_required_browser_tool_when_agent_lacks_playwright_mcp`
- Passing test: `Runtime_readiness_rejects_required_browser_tool_when_agent_lacks_playwright_mcp`
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/AgentProcessReadinessEvaluator.cs`, `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- Production assertions: `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs` passes required runtime tool names into readiness evaluation.
- Red-team negative case: A QA agent with ordinary QA tags but no Playwright/browser MCP capability is not execution-ready for `browser_take_screenshot`.
- Downstream dependency check: The required tool names come from launch variables plus typed `ProcessCapabilityScope.RequiredReceipts`.

