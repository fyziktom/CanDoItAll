# 02 — User Stories And Actors

Primární detailní evidence je v workbooku `requirements/agentframework-integration-user-stories.xlsx`. Tento markdown soubor shrnuje stejné informace čitelně pro rychlou bundle review.

## Actors

| Actor ID | Actor | Type | Responsibility | Primary modules |
| --- | --- | --- | --- | --- |
| ACT-01 | Platform administrator | Human | Spravuje moduly, providery, feature gates a platform settings. | Workspace, Security, AgentFramework, Collaboration |
| ACT-02 | Agent designer | Human | Navrhuje templates, technical agent definitions, capabilities a runtime guardrails. | AgentFramework |
| ACT-03 | HR manager | Human | Spravuje resource pool, agent resources, staffing a governance review v CRM-HR. | CRM-HR, Processes |
| ACT-04 | HR AI agent | AI resource | Navrhuje kandidáty a creation proposals pro process role staffing. | Processes, CRM-HR, AgentFramework |
| ACT-05 | Main Manager AI agent | AI resource | Schvaluje staffing plan nebo vrací comments/escalations. | Processes, Collaboration, AgentFramework |
| ACT-06 | Human approver / manager | Human | Nahrazuje AI managera nebo schvaluje citlivé resource/provisioning změny. | Processes, Collaboration |
| ACT-07 | Process designer | Human | Definuje role, steps, decision rights a messaging links na canvasu. | Processes |
| ACT-08 | Process operator | Human | Spouští process launch, sleduje run a řeší blokace. | Processes, Collaboration |
| ACT-09 | Project manager | Human | Nastavuje project-specific manager/assignments a dohlíží na delivery resource mix. | Projects, CRM-HR, Processes |
| ACT-10 | AI agent resource | AI resource | Vykonává roli v procesu přes AgentFramework runtime a komunikuje jen přes policy-governed services. | AgentFramework, Processes, Collaboration |
| ACT-11 | Human resource | Human | Employee/contractor/freelancer přiřazený do role v procesu. | CRM-HR, Processes |
| ACT-12 | Auditor | Human | Kontroluje transcripts, approvals, artifacts a resource decisions pro konkrétní run. | Collaboration, Processes, Activity |
| ACT-13 | Notification recipient | Human | Potřebuje centralizovaný inbox upozornění, eskalací a approval tasks. | Collaboration |
| ACT-14 | Automation / outbox worker | System | Doručuje durable messages, orchestration a retry/dead-letter chování. | Automation, Processes |
| ACT-15 | Scenario harness operator | System/Human | Spouští integrované scénáře a sbírá důkazy bez fake bypassu. | AgentFramework, TestLab, Processes |

## User Stories

| Story ID | Epic | Actor ID | Story | Acceptance summary | Priority | Modules | Raw note anchors | Owning subbundle |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| US-01 | Provider governance | ACT-01 | Jako platform administrátorka chci spravovat provider profily jen na jednom místě a mít jistotu, že všechny AI requesty jdou přes AgentFramework runtime. | Provider je uložen jednou, health check se zobrazuje v Agents/Providers UI a legacy Workspace runtime není active path. | High | Workspace, Security, AgentFramework | IN-03, IN-05 | 04-provider-ownership-bridge-and-legacy-runtime-retirement |
| US-02 | Agent management | ACT-02 | Jako agent designerka chci vytvářet technical agent definitions, templates a capabilities v CanDoItAll shellu bez potřeby původního sandbox hostu. | Agents menu obsahuje tabs Agents/Capabilities/Chat/Scenarios a technické editace se ukládají do AgentFramework canonical store. | High | AgentFramework, Web | IN-14 | 10-agent-ui-recomposition-shell-tabs-and-cross-module-experience |
| US-03 | Shell integration | ACT-01 | Jako platform administrátorka chci mít AgentFramework jako běžný modul v CanDoItAll.Web a ne jako separátní application shell. | Shell navigation obsahuje Agents položku, layout je konzistentní a sandbox navigation není použita. | High | Web, Composition | IN-14 | 01-foundation-import-map-and-module-skeleton |
| US-04 | Notification inbox | ACT-13 | Jako příjemkyně notifikací chci centrální inbox pro eskalace agentů, pending approvals a procesní upozornění. | Inbox zobrazuje unread/read stav, severity, link na run a možnost otevřít thread. | High | Collaboration | IN-04, IN-18 | 02-collaboration-domain-notification-and-conversation-foundation |
| US-05 | Human escalation | ACT-10 | Jako AI agent chci umět eskalovat problém člověku přes řízený kanál místo neviditelného side-chatu. | Vznikne escalation thread a notification item se správným process/run contextem. | High | AgentFramework, Collaboration, Processes | IN-04, IN-08 | 02-collaboration-domain-notification-and-conversation-foundation |
| US-06 | Role-governed messaging | ACT-10 | Jako agent vykonávající roli chci oslovit jinou roli jen tehdy, když to proces výslovně povoluje. | Bez Messaging linku je komunikace blokovaná; s linkem vznikne uložený thread v rámci runu. | Critical | Processes, Collaboration, AgentFramework | IN-06, IN-07 | 03-process-messaging-policy-canvas-and-runtime-enforcement |
| US-07 | Audit transcript | ACT-12 | Jako auditorka chci rekonstruovat komunikaci, approvals, resources a artifacts konkrétního process runu. | Run details zobrazí assignments, messages, approvals, outbox events a artifact evidence bez mezer. | Critical | Processes, Collaboration, Activity | IN-08, IN-18 | 09-agent-execution-orchestration-artifact-bridge-and-run-observability |
| US-08 | Resource pool integrity | ACT-03 | Jako HR manažerka chci mít lidi, kontraktory i AI agenty v jednom resource poolu. | CRM-HR listing i staffing flows vracejí mixed resource candidates z jednoho katalogu. | High | CRM-HR, Processes | IN-09, IN-10 | 06-crmhr-resource-binding-and-agent-management-surface |
| US-09 | CRM-HR agent editing | ACT-03 | Jako HR manažerka chci spravovat AI resource z CRM-HR UI, ale nechci tím vytvářet druhý technický registry. | CRM-HR stránka zobrazuje business metadata a technical binding/editor přes AgentFramework facade. | High | CRM-HR, AgentFramework | IN-09 | 06-crmhr-resource-binding-and-agent-management-surface |
| US-10 | Project manager selection | ACT-09 | Jako project manažerka chci při startu procesu dostat doporučené resources pro všechny role včetně AI agentů. | Launch plan zobrazí scored candidates podle role intentu, project assignments a capabilities. | Critical | Processes, CRM-HR | IN-11 | 07-process-launch-planning-hr-recommendation-and-default-strategies |
| US-11 | HR AI recommendation | ACT-04 | Jako HR AI agent chci doporučit existující resource nebo navrhnout vytvoření nového agenta, když suitable resource chybí. | Role recommendations obsahují existing candidates i creation proposal s důvodem. | Critical | Processes, CRM-HR, AgentFramework | IN-11 | 07-process-launch-planning-hr-recommendation-and-default-strategies |
| US-12 | Role design | ACT-07 | Jako process designerka chci definovat role, preferred executor kind a staffing intent pro každou roli. | Role metadata se ukládají a používají se při staffing recommendation flow. | High | Processes | IN-11 | 07-process-launch-planning-hr-recommendation-and-default-strategies |
| US-13 | Messaging design | ACT-07 | Jako process designerka chci kreslit Messaging linky mezi rolemi přímo na canvasu. | Canvas umí přidat/odebrat Messaging connection a zobrazuje ji odlišně od responsibility/decision/artifact links. | Critical | Processes UI | IN-07 | 03-process-messaging-policy-canvas-and-runtime-enforcement |
| US-14 | Manager approval | ACT-06 | Jako schvalovatelka chci potvrdit nebo vrátit staffing plan před spuštěním procesu. | Existuje approval task s contextem rolí, kandidátů a creation requests; rozhodnutí mění stav launch plánu. | Critical | Processes, Collaboration | IN-11, IN-13 | 08-manager-approval-human-substitution-and-resource-provisioning |
| US-15 | Launch gate | ACT-08 | Jako process operátorka nechci omylem spustit run bez schválených resources. | Start run tlačítko je blokované, dokud launch plan není ReadyToStart; runtime nezačne předčasně. | Critical | Processes UI | IN-11 | 07-process-launch-planning-hr-recommendation-and-default-strategies |
| US-16 | Fallback manager | ACT-09 | Jako project manažerka chci, aby Main Manager mohl být AI agent nebo člověk podle projektu. | Resolver vybere project-specific agent binding nebo human party assignment a flow zůstane stejný. | High | Projects, Processes, CRM-HR | IN-12, IN-13 | 08-manager-approval-human-substitution-and-resource-provisioning |
| US-17 | Fallback HR | ACT-03 | Jako HR manažerka chci, aby staffing doporučení fungovalo i bez AI provideru. | Rule-based strategy vrací kandidáty a creation proposals bez volání modelu. | High | Processes, CRM-HR | IN-12 | 07-process-launch-planning-hr-recommendation-and-default-strategies |
| US-18 | Scoped workspace | ACT-10 | Jako agent runtime chci mít izolovaný workspace pro projekt nebo run, aby se scénáře a process runs nepletly. | Workspace locator používá project/process/run scope a artefakty jsou oddělené. | High | AgentFramework | IN-03, IN-16 | 05-agent-catalog-persistence-workspace-scoping-and-governance-bridges |
| US-19 | Artifact evidence | ACT-12 | Jako auditorka chci, aby artifacts vzniklé agentem byly canonical evidence v managed storage. | Process artifact records ukazují managed storage path, content type a provenance z agent runu. | High | Processes, Storage, AgentFramework | IN-18, IN-19 | 09-agent-execution-orchestration-artifact-bridge-and-run-observability |
| US-20 | Chat continuity | ACT-02 | Jako agent designerka chci pracovat s chatem a approvals i po restartu hostu. | Chat session, pending approvals a checkpoints se po restartu obnoví v integrated hostu. | High | AgentFramework, Collaboration | IN-18 | 05-agent-catalog-persistence-workspace-scoping-and-governance-bridges |
| US-21 | Deep-link usability | ACT-08 | Jako operátorka chci přecházet mezi Processes, CRM-HR a Agents bez ztráty kontextu. | Deep links otevřou správný tab/detail s project/process contextem. | Medium | Processes, CRM-HR, AgentFramework | IN-09, IN-14 | 10-agent-ui-recomposition-shell-tabs-and-cross-module-experience |
| US-22 | Provider UI migration | ACT-01 | Jako platform administrátorka chci provider management najít v Agents UI a ne ve staré Settings duplicitě. | Settings provider tab je odstraněný nebo redirectuje do Agents/Providers bez ztráty funkce. | High | Workspace, AgentFramework, Web | IN-05, IN-14 | 10-agent-ui-recomposition-shell-tabs-and-cross-module-experience |
| US-23 | Real scenario harness | ACT-15 | Jako scenario harness operátorka chci v integrated hostu spouštět skutečné scénáře místo fake mocků. | SC01–SC08 běží v integrated hostu; manuální scénáře mají explicitní proof; nic nepoužívá bypass mimo skutečný flow. | Critical | AgentFramework, TestLab, Processes | IN-18, IN-20 | 11-scenario-migration-real-e2e-validation-and-playwright-proof |
| US-24 | App-writing process scenario | ACT-15 | Jako validační operátorka chci process-centric multi-agent scénář pro psaní aplikace s reálným staffing selection a messaging policy. | Nový scénář vytvoří process definition, vybere resources podle rolí, schválí je a dokončí run s artifacts. | Critical | Processes, CRM-HR, AgentFramework | IN-18, IN-19 | 11-scenario-migration-real-e2e-validation-and-playwright-proof |
| US-25 | No fake tests | ACT-12 | Jako auditorka chci jistotu, že validační scénáře opravdu používají nový launch flow a messaging policy, ne ručně naplněný shortcut. | Execution proof obsahuje reálné DB/runtime evidence a Playwright steps přes UI. | Critical | All | IN-18, IN-19 | 11-scenario-migration-real-e2e-validation-and-playwright-proof |
| US-26 | Refactor gate | ACT-01 | Jako delivery manažerka chci zastavit implementaci, jakmile začne vznikat architektonický nepořádek. | Každá fáze má reopen triggers a executor nesmí pokračovat bez refactor subbundle, pokud selže gate. | Critical | Bundle, All | IN-17 | 12-data-backfill-cleanup-refactor-gates-and-final-closure |
| US-27 | Story coverage | ACT-12 | Jako QA inspektorka chci před closure zkontrolovat, že každá user story je řešitelná v UI a má důkaz. | Story-to-UI matrix a execution report mají coverage status pro každou story. | Critical | Bundle, QA | IN-18 | 12-data-backfill-cleanup-refactor-gates-and-final-closure |
| US-28 | Triple review | ACT-01 | Jako zadavatelka chci finální rozhodnutí z pohledu QA, development managerky a senior C# architektky. | Bundle i final execution report obsahují tři samostatné review sekce a vyřešené concerns. | High | Bundle | IN-18 | 12-data-backfill-cleanup-refactor-gates-and-final-closure |

## Coverage Notes

- User stories záměrně kombinují platform, CRM-HR, process a audit perspektivu. Zadání není čistě technická migrace; je to redesign pracovního flow.
- `US-23` až `US-25` explicitně zabraňují fake scenario validation.
- `US-26` až `US-28` zajišťují, že bundle sama vynucuje disciplínu během implementace.

## Workbook Link

- `requirements/agentframework-integration-user-stories.xlsx`
