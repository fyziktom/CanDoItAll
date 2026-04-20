# 10 — Agent UI Recomposition Shell Tabs And Cross-Module Experience

## Status

- `Completed`

## Objective

- Recomposovat AgentFramework sandbox UI do CanDoItAll shellu jako jeden modul s interními tabs.
- Propojit Agents, CRM-HR a Processes deep-linky bez duplikace menu nebo shell chrome.
- Zrušit nebo redirectnout duplicitní provider/agent surfaces mimo nový flow.

## Covered Inputs

- `IN-14`, `RQ-21`, `RQ-22`, `US-02`, `US-03`, `US-21`, `US-22`

## Prerequisites

- `09-agent-execution-orchestration-artifact-bridge-and-run-observability` closed.
- `06-crmhr-resource-binding-and-agent-management-surface` closed.

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Composition/ShellNavigation.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.CrmHr/Pages/CrmHrAgentsPage.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Home.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Agents.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Providers.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Chat.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Capabilities.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Memory.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/IntegrationMap.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/Hosting.razor
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages/ScenarioHarness.razor
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/AiAgentFlowTests.cs

## Deliverables

- Integrated `/agents` route with internal tabs reflecting the original sandbox intent.
- Reused or adapted pages/components for Overview, Agents, Providers, Chat, Capabilities, Governance, Scenarios, Diagnostics.
- Deep links between `/crm-hr/agents`, `/processes` run/launch details and `/agents` tabs.
- Removal or redirect of duplicate provider management UX from Settings where appropriate.

## Dependency Impact

- Scenario proof, user-story closure a final product acceptance budou dělat browser walkthrough právě přes tuto UX vrstvu.
- Pokud tady zůstane duplicitní shell nebo rozpadlý context, finální validace neprojde.

## Validation Depth

- `Critical UI closure`
- Vyžaduje desktop i narrower browser proof a screenshot review.

## Implementation Steps

1. Navrhnout tab container a route state model pro `/agents`.
2. Recompose sandbox page content do CanDoItAll page scaffolds a existing design system components.
3. Napojit providers, agents, chat, governance a scenarios na integrated services.
4. Upravit Settings/CRM-HR/Processes entry points a deep links.
5. Projít UI/UX consistency: tab naming, hierarchy, context breadcrumbs, badges, action placement.

## Scope Exceptions

- Low-level diagnostics mohou být admin-only; musí ale být dohledatelně dostupné z Agents modulu.

## Do Not Do

- Nevkládat sandbox navigation sidebar uvnitř CanDoItAll shellu.
- Nedržet druhou provider management obrazovku ve starých Settings bez redirectu nebo jasného read-only režimu.
- Neponechat uživatelku tápat mezi business a technical edit surface.

## Acceptance Checklist

- Agents menu funguje jako jediný vstup do AgentFramework shell experience.
- Tabs pokrývají sandbox intent a používají CanDoItAll design system.
- Deep links z CRM-HR a Processes zachovávají kontext.
- Duplicitní Settings provider surface je odstraněná nebo redirectovaná.

## Proof Required

- Component/browser proof pro tab navigation a context preservation.
- Playwright desktop proof přes všechny hlavní tabs.
- Narrower viewport follow-up pass pro layout regression.
- Screenshot review sepsaná v execution reportu.

## Browser Validation Logging

- Route: `/agents`.
- Viewport: `1600x900` a např. `1280x800` / užší pass podle layoutu.
- Actions: projít tabs, otevřít provider, agent, chat, governance a scenario surfaces, ověřit deep link z CRM-HR a Processes.
- Screenshot review: žádný duplicitní shell, žádné clipping, jasná tab hierarchy.

## Progression Gate

- Scenario subbundle smí pokračovat až když user-facing shell experience je stabilní a story flows mají konkrétní routes.
- Pokud uživatelka musí kvůli nějakému flow otevírat původní sandbox host, gate failuje.

## Suggested Agent Prompt

```text
Implement only subbundle 10.

Recompose the AgentFramework sandbox experience into a single `/agents` module inside the CanDoItAll shell, with internal tabs and deep links from CRM-HR and Processes. Remove or redirect duplicate old surfaces and prove desktop-quality UX.
```

