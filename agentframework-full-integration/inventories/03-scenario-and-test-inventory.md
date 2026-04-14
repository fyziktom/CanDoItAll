# 03 — Scenario And Test Inventory

## AgentFramework Scenario Harness Today

| Scenario ID | Kind | Name | Validation goal | Mode |
| --- | --- | --- | --- | --- |
| SC01 | Existing | Email PDF Summary | Integrated host must still run document summary scenario and persist response/artifact receipts. | Automated |
| SC02 | Existing | BOM Versus Quote | Integrated host must still run workbook comparison and persist analysis artifacts. | Automated |
| SC03 | Existing | Blazor Calculator Generation | Integrated host must still run controlled code-generation scenario with receipts. | Automated |
| SC04 | Existing | Approval Pause And Resume | Integrated host must still pause on approval and resume the same run after decision. | Automated |
| SC05 | Existing | Python Analysis With Artifacts | Integrated host must still run python artifact generation scenario. | Automated |
| SC06 | Existing | PowerShell Repo Inventory | Integrated host must still run constrained PowerShell inventory scenario. | Automated |
| SC07 | Existing | Restart And Resume | Manual proof must verify durable restart/resume behavior after integrated migration. | Manual |
| SC08 | Existing | Provider-Native Versus Local Comparison | Manual proof must compare supported provider-native path vs local controlled path honestly. | Manual |
| SC09 | New | Process Staffing And Launch | Create process roles, let HR recommend resources, approve them, then start the run through the new launch flow. | Automated + Playwright |
| SC10 | New | Human Escalation And Notifications | Agent escalates a blocked task to a human, Collaboration inbox surfaces it, human responds, run continues. | Automated + Playwright |
| SC11 | New | Application Writing Multi-Agent Process | Use additional template agents and a new process to collaboratively draft an app through allowed messaging links only. | Automated + Playwright |

## Existing Test Assets Worth Extending

| Test asset | Path | Why it matters |
| --- | --- | --- |
| AI agents component tests | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Components/AiAgentsPageTests.cs` | CRM-HR AI agent UI is already tested and will need adaptation. |
| Provider settings component tests | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Components/SettingsPageProvidersTests.cs` | Legacy provider UI behavior must be migrated or redirected safely. |
| Process workspace component tests | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs` | Best place to add Messaging canvas link and launch UX checks. |
| Processes integration tests | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | Captures current simplistic start flow and must be expanded. |
| Process outbox integration tests | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | Natural proof point for agent execution orchestration. |
| Staffing integration tests | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/StaffingAllocationIntegrationTests.cs` | Reuse for staffing/availability/resource ranking behavior. |
| AI agent profile integration tests | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Integration/AiAgentProfileIntegrationTests.cs` | Useful for CRM-HR to AgentFramework binding migration. |
| AI agent Playwright flow | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Playwright/AiAgentFlowTests.cs` | Good base for CRM-HR + Agents integrated UX. |
| Staffing Playwright flow | `/mnt/data/work/cando/CanDoItAll-development/tests/CanDoItAll.Tests.Playwright/StaffingFlowTests.cs` | Good base for process launch staffing UX. |

## Validation Gap To Close

- Dnešní testy neprokazují:
  - process-governed direct messaging,
  - launch plan approval flow,
  - Collaboration inbox,
  - integrated scenario harness inside CanDoItAll shell,
  - resource creation proposal for missing AI agents,
  - run-level communication transcript with artifacts and approvals.
