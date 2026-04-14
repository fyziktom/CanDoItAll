# 01 — Normalized Requirements

Normalizace níže zachovává původní směr zadání, ale převádí ho do testovatelných a ownership-aware požadavků. Každý requirement musí být při implementaci uzavřen konkrétním proofem v execution reportu.

## Requirement Set

| ID | Category | Requirement | Observable acceptance | Raw note anchors |
| --- | --- | --- | --- | --- |
| RQ-01 | Bundle | Uchovat původní zadání, vstupní artefakty a strukturované shrnutí přímo v bundle. | Bundle obsahuje `inputs/00-original-request.md`, `inputs/01-source-artifacts.md`, `inputs/02-structured-input.md` a coverage matrix. | IN-01, IN-15 |
| RQ-02 | Bundle | Vytvořit XLSX inventář actorů, user stories, requirements, scénářů a traceability vazeb. | Workbook existuje, má více sheetů, summary formule a je odkazovaný v requirements a traceability dokumentech. | IN-15, IN-18 |
| RQ-03 | Integration | Zdrojový kód AgentFrameworku se musí fyzicky zkopírovat do CanDoItAll repo a nesmí zůstat živě závislý na externím solution. | V cílovém repo nevznikne reference na `C:\repositories\CanDoItAll.AgentFramework`; všechna potřebná logika je pod `C:\repositories\CanDoItAll\src`. | IN-02 |
| RQ-04 | Modules | Před integrací agent runtime vznikne samostatný Collaboration module pro notifikace, conversations a human escalation. | Nový modul existuje, je registrovaný v Composition/Web a má vlastní persistence, queries a UI entry point. | IN-04 |
| RQ-05 | Collaboration | Notification center a messaging center musí oddělit user-facing canonical store od interní automation transport vrstvy. | Transport používá existing automation messaging, ale UI a audit čte z Collaboration persistence modelu. | IN-04, IN-06 |
| RQ-06 | Messaging | Agenti ani lidé nesmí obcházet procesní pravidla komunikace; veškerá agent komunikace jde přes autorizovanou messaging službu. | Neexistuje přímé agent-to-agent volání mimo `IProcessCommunicationService` / ekvivalent; pokusy bez policy jsou blokované a auditované. | IN-06, IN-07 |
| RQ-07 | Messaging | Process canvas musí získat explicitní Messaging link mezi rolemi a z něj se musí odvozovat runtime policy pro konkrétní run. | Designer může přidat/odebrat messaging link; runtime snapshot dovolí jen povolené role páry. | IN-07 |
| RQ-08 | Messaging | Všechny povolené zprávy a eskalace se musí ukládat s kontextem process runu, stepu, rolí a correlation IDs. | Run detail a audit zobrazí úplný transcript, účastníky, timestamps a návaznost na run artifacts/decisions. | IN-08, IN-18 |
| RQ-09 | Providers | Workspace/Security musí být canonical owner provider master dat a secrets; AgentFramework musí být canonical owner execution runtime a tool orchestrace. | Existuje jasný bridge; v systému není dvojí provider execution path. | IN-03, IN-05 |
| RQ-10 | Providers | Legacy Workspace provider execution vrstva musí být retirená nebo striktně shimnutá za feature gate s jasným sunset plánem. | Old adapters nejsou aktivní canonical runtime; testy dokazují, že requests jdou přes AgentFramework runtime. | IN-03, IN-05 |
| RQ-11 | Resources | CRM-HR zůstává canonical resource pool pro lidi, kontraktory i AI resources. | Process staffing a project assignment flows čtou resource roster z CRM-HR; Agent module jen dodává technical AI details. | IN-09, IN-10 |
| RQ-12 | Resources | Musí vzniknout canonical binding mezi CRM-HR AI resource profilem a AgentFramework technical agent definition. | Existuje binding entity/service a migration/backfill pro současné AI agent profiles. | IN-09, IN-10 |
| RQ-13 | Resources | CRM-HR UI musí umět spravovat business-facing agent resource a zároveň delegovat technical agent editing do AgentFrameworku bez druhého editable source of truth. | CRM-HR stránka zobrazuje kombinovaný view model, ale zapisuje business a technical pole do správných modulů. | IN-09, IN-16 |
| RQ-14 | Processes | Start procesu se musí změnit na staged launch flow: role snapshot -> resource recommendation -> approval -> provisioning -> actual run start. | Process už neaktivuje run okamžitě; vzniká launch/staffing plan s vlastními statusy a gates. | IN-11 |
| RQ-15 | Processes | HR recommendation musí umět navrhnout existující humans/agents i vytvoření nového agenta, když suitable resource neexistuje. | Launch plan role má candidate list, score a volitelný creation proposal. | IN-11 |
| RQ-16 | Processes | HR agent a Main Manager musí mít defaultní algoritmickou fallback strategii bez AI a zároveň možnost nahradit je AI agentem později. | Platforma umí fungovat bez provideru; později lze přepnout na AI-backed implementations bez změny process contractu. | IN-12 |
| RQ-17 | Processes | Main Manager approval authority musí být projektově specifická a musí umět nahradit AI člověkem. | Approval resolver podporuje project-specific agent binding i human party assignment. | IN-13 |
| RQ-18 | Runtime | AgentFramework workspaces nesmí být globální sandbox root; musí se scopeovat podle project/process/run a přitom zachovat izolaci artefaktů. | Existuje workspace locator/context factory; runy nekolidují v jednom globálním file store. | IN-03, IN-16 |
| RQ-19 | Runtime | Agent approvals, execution events a checkpoints se musí bridgeovat do CanDoItAll durable modelů a collaboration inboxu. | Approvals a execution run telemetry jsou dohledatelné z main app a lze je resume/approve po restartu. | IN-04, IN-18 |
| RQ-20 | Artifacts | Agent-generated artifacts se musí promítat do managed storage a process artifact records jako canonical evidence. | Artifact bridge zapisuje managed storage path, trust status a vazbu na run/step. | IN-18, IN-19 |
| RQ-21 | UI | AgentFramework Sandbox UI se musí recomposovat do CanDoItAll.Web jako jedna menu položka s interními tabs a bez duplicity shellu. | Shell navigation obsahuje Agents/AI položku; pages používají CanDoItAll layout a sdílí menu/topbar. | IN-14 |
| RQ-22 | UI | CRM-HR, Processes a Agents UI musí být propojené deep-linky a user stories musí být řešitelné bez nutnosti jít do původního sandbox hostu. | Každý user story flow má přiřazenou UI surface a Playwright proof. | IN-09, IN-11, IN-14, IN-18 |
| RQ-23 | Validation | Každá fáze musí mít přísný refactor gate; když implementace začne vytvářet duplicity, velké soubory nebo split source of truth, práce se zastaví a vznikne refactor subbundle. | Plan a prompts obsahují explicitní reopen triggers a refactor-first rule. | IN-16, IN-17 |
| RQ-24 | Validation | Codex musí ověřit UI pomocí Playwright MCP, screenshot review, FrontendSkill checklistu a kombinace component/integration testů. | Execution report obsahuje browser analytics, screenshot paths a review findings per subbundle. | IN-18 |
| RQ-25 | Validation | Codex musí validovat closure podle user stories a když některý flow nelze v UI dokončit, musí nejprve doplnit UI. | Story-to-UI matrix je kompletní a final gate vyžaduje explicitní coverage review. | IN-18 |
| RQ-26 | Validation | Scenario validation musí vycházet z reálného repozitáře: respektovat stávající SC01–SC08 a přidat nové process-centric scenarios bez fake bypassu. | Bundle explicitně řeší rozdíl mezi '5 scénářů' a skutečným katalogem, přidává nové scénáře a vyžaduje reálné run evidence. | IN-18, IN-19, IN-20 |
| RQ-27 | Migration | Musí existovat migration/backfill/cleanup plán pro provider profiles, AI agent profiles a nové binding/tabulky. | Bundle definuje pořadí EF migrací, backfill scriptů, kill switches a post-migration cleanupu. | IN-05, IN-09, IN-17 |
| RQ-28 | Governance | Efektivní permission pro messaging/escalation vzniká průnikem agent permission policy, process messaging policy a governance pravidel. | Testy dokazují, že samotné agent permission nestačí bez procesního povolení a governance statusu. | IN-06, IN-07, IN-18 |
| RQ-29 | Governance | Závěrečná bundle readiness i implementation closure musí projít pohledem QA inspektorky, development managerky a senior C# architektky. | Self-review a final execution report mají tři samostatné hodnoticí sekce a vyřešené concerns. | IN-18 |

## Requirement Clusters

### Foundation And Boundaries

- `RQ-03`, `RQ-04`, `RQ-09`, `RQ-10`, `RQ-11`, `RQ-12`, `RQ-18`
- Tyto požadavky určují canonical owners a fyzickou podobu integrace. Dokud nejsou uzavřené, ostatní proofy jsou nedůvěryhodné.

### Process Governance

- `RQ-06`, `RQ-07`, `RQ-08`, `RQ-14`, `RQ-15`, `RQ-16`, `RQ-17`, `RQ-28`
- Tohle je jádro zadaného business flow: role staffing, direct messaging jen s policy, approvals a audit.

### UI And Operational Experience

- `RQ-04`, `RQ-05`, `RQ-13`, `RQ-21`, `RQ-22`
- Tyto požadavky zajišťují, že nový systém není jen backend integrace, ale skutečně použitelný z CanDoItAll.Web.

### Validation And Closure

- `RQ-01`, `RQ-02`, `RQ-23`, `RQ-24`, `RQ-25`, `RQ-26`, `RQ-27`, `RQ-29`
- Bez nich by šlo dodat „něco, co buildí“, ale ne spolehlivě integrovaný systém s důkazem, že funguje podle zadání.

## Must-Not-Weaken Clauses

- „AgentFramework bude hlavní provider AI connection“ je v bundle interpretované jako **canonical runtime owner**, ne jako důvod vytvořit druhý provider master-data store.
- „Agents must use internal messaging system“ je v bundle interpretované jako reuse `Automation` transportu + centralizované `Collaboration` canonical store; není dovoleno chápat to jako „agenti si můžou posílat zprávy jakkoli, pokud se to nějak zaloguje“.
- „CRM-HR is main resource pool“ znamená, že žádná jiná module nesmí začít vytvářet vlastní resource directory.
- „Codex must not fake tests“ znamená skutečné process launch flow, approvals, messaging policy a runtime artifacts, ne seednuté DB řádky.
