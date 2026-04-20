# OpenAI Agent Capability And Process Template Hardening

## Status

- `Completed`

## Objective

- Harden the serious-delivery agent set so the right AgentFramework-owned agents use OpenAI, carry strong C# and Blazor instructions and skills, and expose validated Playwright plus screenshot-analysis capability where UI work or QA requires it.

## Covered Inputs

- `N003`
- `N005`
- `N006`

## Prerequisites

- `subbundles/01-canonical-agentframework-ownership-and-crm-hr-projection` closed with proof

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceExecutionService.ProviderNativeMcp.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `C:\repositories\CanDoItAll\Templates\Processes`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AiAgentProfileIntegrationTests.cs`

## Deliverables

- Serious delivery-agent definitions or provisioning updates with OpenAI-backed configuration.
- Improved agent instructions and skill attachments for C#, Blazor, review, UI review, QA, and release work.
- Verified Playwright availability and screenshot-aware QA expectations for the agents that need them.
- Template-driven process composition updates where capability assumptions or role requirements are currently too weak.

## Dependency Impact

- Provisioning and runtime execution depend on this phase. If the agent capabilities or template role expectations are wrong here, the live run will fail for the wrong reasons.

## Validation Depth

- `Critical foundation`
- `Process-critical closure`

## Implementation Steps

1. Inspect seeded or provisioned serious-delivery agents and compare them to the user’s requested roles and skills.
2. Update OpenAI provider usage, instructions, skill attachments, and Playwright-capable agent definitions.
3. Strengthen template-driven process composition so the right role requirements and agent capabilities are enforced for serious delivery.
4. Add tests and runtime evidence checks for seeded capability presence and actual MCP execution evidence.
5. Browser-verify that the relevant agents are visible and inspectable from the AgentFramework UI.

## Scope Exceptions

- This phase prepares capability and reusable process composition but does not yet execute the serious project end to end.

## Do Not Do

- Do not hardcode project-specific logic into generic agent seeds unless the same logic belongs in baseline serious-delivery defaults.
- Do not claim Playwright support based only on catalog metadata without runtime evidence.

## Acceptance Checklist

- Required delivery agents are AgentFramework-owned and OpenAI-backed.
- UI and QA agents can reach `playwright-local-mcp` and screenshot handling is part of their expected behavior.
- Process templates or composition logic require the right roles and capabilities for serious delivery.

## Proof Required

- Integration tests for seeded agent capability and runtime MCP evidence.
- Runtime proof of a real agent tool call path involving Playwright-related actions or screenshot handling.
- UI proof that the relevant agents are inspectable from the Agents page.

## Closure Notes

- Shared role resources and serious-delivery process templates now expose the architecture, QA, security, review, and release roles as `person-or-agent` where the flow is intended to bind AgentFramework-owned AI agents.
- The seeded serious-delivery catalog now includes dedicated Code Review, UI Review, Security Review, and Release Readiness agents with OpenAI-backed provider setup and C# / Blazor-focused skill attachments.
- Legacy organization workspaces now refresh stale built-in architect, QA, and programming seed records to the current serious-delivery baseline instead of leaving older Ollama or calculator-era defaults in place.
- Live UI proof on the target profile shows the refreshed baseline plus the new serious-delivery agents on `/agents?tab=agents`, and the selected `Delivery QA Observer` record remains editable through the AgentFramework page.

## Browser Validation Logging

- Target route: `/agents?tab=agents`
- Required viewport: `1600x900`
- Required Playwright MCP actions: inspect the serious-delivery agent list, open relevant agent detail or edit UI when available, capture screenshot evidence
- Expected evidence paths: execution-report entries for agent catalog and runtime evidence artifacts
- Screenshot review questions: are the serious roles visibly present, do the labels or capabilities reflect QA or UI focus correctly, and is there any remaining showcase-only framing

## Progression Gate

- Do not start subbundle `03` until required serious-delivery agents are OpenAI-backed, Playwright-capable where needed, and proven by tests or runtime evidence instead of catalog assumptions alone.

## Suggested Agent Prompt

```text
Implement only subbundle 02. Harden the AgentFramework-owned serious-delivery agents and process-template composition so they use OpenAI, carry strong C# and Blazor guidance, and expose validated Playwright plus screenshot-analysis capability for UI and QA roles. Prove capability with tests and runtime evidence before closing the phase.
```
