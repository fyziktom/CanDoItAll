# 02 — Gap Matrix

| Area | Expected by request | Current state after audit | Status | Closure requirement |
| --- | --- | --- | --- | --- |
| AgentFramework source import | Přenést AgentFramework do CanDoItAll jako vlastní module, ne externí reference | Importovaný je jen modulový skeleton a placeholder page | Blocking gap | Zkopírovat a integrovat skutečné runtime/models/persistence/components/scenario assets |
| AgentFramework service registration | Modul má registrovat reálné služby a orchestration | `AddAgentFrameworkModule()` vrací `services` bez registrací | Blocking gap | Zaregistrovat provider, agent catalog, orchestration, chat, scenarios, governance services |
| Providers canonical owner | AI provider ownership má přejít pod AgentFramework | Provider ownership zůstává ve stávajících surfaces; i placeholder to přiznává | Blocking gap | Přesunout canonical owner, udělat migration/backfill, odstranit duplicity |
| CRM-HR ↔ AgentFramework bridge | CRM-HR spravuje resource-facing identity, AgentFramework technical identity | V CRM-HR nejsou auditované změny a není bridge na technical agent definition | Blocking gap | Přidat binding model a UI bridge |
| Collaboration / escalation center | Notification + messaging centrum před agent integration | Implementovaný foundation module | Delivered foundation | Rozšířit o launch approvals a agent/human work routing bez paralelního store |
| Process messaging policy | Agents nesmí komunikovat bez explicitní Messaging link policy | Implementovaný allowed/denied enforcement | Delivered foundation | Dále využít při agent orchestration a scenario tests |
| Process launch planning | Start procesu musí nejdřív udělat staffing / recommendation / approval | Kód stále startuje run přímo přes `StartRunAsync` | Blocking gap | Zavést LaunchPlan entities, services, UI a gates |
| HR recommendation | HR AI agent nebo fallback má navrhovat resources podle rolí | Nenalezená implementace | Blocking gap | Candidate search + proposal engine + creation proposal path |
| Main Manager approval | Manager nebo člověk musí schválit resources | Nenalezená implementace | Blocking gap | Project-specific approval resolver + collaboration tasks + provisioning |
| Default HR / Main Manager | Systém musí fungovat i bez AI | Nenalezená implementace | Blocking gap | Rule-based fallback strategies a testy |
| Agent execution orchestration | Po schválení mají role běžet přes vybrané resources | Nenalezená implementace | Blocking gap | Process-run execution bridge, artifact storage, observability |
| `/agents` UI recomposition | Jedna menu položka s interními tabs převzatými ze sandboxu | Placeholder stránka s budoucím plánem | Blocking gap | Skutečné tabs: agents, providers, chat, governance, scenarios |
| Scenario migration | Reálné scenarios z AgentFramework + nové process-centric scenarios | Nenalezený import ScenarioHarness | Blocking gap | Přenést SC01–SC08 a přidat nové process-centric scenarios |
| Playwright proof | Reálné browser proof + screenshot review pro celé flow | V repu nejsou nové Playwright tests ani slíbené screenshot artifacts | Blocking gap | Přidat reprodukovatelné tests/proof logs a uložené screenshots |
| Bundle closure truthfulness | Completion claim smí přijít až po uzavření všech subbundles | Execution report sám říká `In progress` a `not honestly closable yet` | Blocking gap | Reopen, dodělat 04–12, teprve pak uzavřít |
