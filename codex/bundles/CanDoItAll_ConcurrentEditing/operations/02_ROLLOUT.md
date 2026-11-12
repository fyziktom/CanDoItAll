# Fáze a rollout po schválení

## P0 — Inventář, důkazy, baseline (bez plošné implementace)

Zaznamenat actual HEAD/pins a dirty worktree; přečíst repo/SharedInfo instrukce. CodeAnalytics census a lokální audit spárují skutečné main-UI plochy s inventářem. Doložit prompt write perimeter, skutečný host auth context, overlay placement a current owner contracts. Zachytit failing-first reprodukce a výkonovou baseline. Neshody mezi novým SHA a podklady opravit v dokumentaci; nedělat rollback na starý audit.

Výstup: stručný aktualizovaný mapa/status, selected tests s odůvodněním a chybějícími případnými novými cases. Žádný obří mandatory workflow manifest. Praktické malé checkpoints s konkrétními důkazy jsou dostačující.

## P1 — Minimální společný základ

Čistý merge kernel a typed adapter seam; neutrální targeted observer; shared notification/modal fix. Coherent prompt snapshot a accepted-state pravidla. Core/overlay/session testy a dependency guards. Nic z toho nevyžaduje převod CRM nebo nahrazení stávajícího ProjectStructure feedu.

Zachovat aktuální provoz ostatních obrazovek. Sdílený Notification/Dialog zásah má cross-module **regresní** scope, ne implementační povolení rozšiřovat merge do dalších modulů. Pro každý nový projekt doložit lehký graph/build dopad.

## P2 — Kompletní Prompt Gallery pilot

PG01–PG04 a SH01; skutečné dvě nezávislé sessions, nested picker, route guards, list quiet refresh, merge a notification. Testy podle pilot matrix; pořád žádná další module adoption. Zachytit původní failing a poslední passing důkazy zvlášť, ne přepsat historii výsledků.

## P3 — Povinný ruční gate

Rebuild a ověření skutečné 5032 instance, handoff s [checklistem](../pilot/02_MANUAL_ACCEPTANCE.md). Stav `AWAITING_USER_APPROVAL_PROMPT_GALLERY`. Žádný rollout bez výslovného souhlasu uživatele. Ad hoc požadavky znamenají další pilot iteration a stejný gate.

## Teprve po souhlasu

| Vlna | Plochy | Proč toto pořadí / closure |
|---|---|---|
| R1 | CRM/HR CR01–CR12 | Prioritní potvrzený lost-update uživatele. Druhá doména prověří kolekce, citlivá pole, multi-entity invariants a existing draft reconciliation. Nezaměňovat dedup business merge za concurrency merge. |
| R2 | Projects + Workbench PR/WB | Navázání na hotové receipt, native expected observations a změnové okno. Jednotný UX bez odstranění recovery; canvas/dirty inspector parity. |
| R3 | Workflow + Process authoring WF/PC | Lossless grafy, inherited scopes a optional-token perimeter workflow. Procesy používají svůj durable owner; žádný generic graph auto-merge. |
| R4 | Agents, teams/capabilities, provider/shared-provider a Simple Chat definitions AG/PV/CH | Více alternate writers a external setup; oddělit konfiguraci od běžícího runtime a transcript append. |
| R5 | Scheduler, Resources, TestLab SC/RS/TL | Potvrzené Scheduler/TestLab gaps; Resources respektovat již zavedený revision protokol. Explicitní lifecycle commands a bounded list queries. |
| R6 | Workspace, Security, Plugins, Memory, Collaboration WS/SE/PL/ME/CO | Mix file control-plane, secrets, OAuth, configuration a message intent. Každý owner potřebuje svůj konkrétní kontrakt, ne plošný EF interceptor. |
| R7 | Main-UI file editors a remaining unclassified census položky | Spárovat každý remaining edit/callback, ne vydávat celkové dokončení s nevysvětlenými plochami. Externí sibling změny jen pro skutečné main-UI consumers. |

Pořadí R2–R6 lze po souhlasu rozumně změnit podle závislostí a rizik, ale nic nesmí zůstat bezejmenným „později“. Potvrzené riziko bezpečnosti dat má přednost před kosmetikou. Zvlášť citlivá mezera může vyžadovat dřívější server guard při zachování původního UI; takové přeřazení zapsat.

## Jednotný mini-checkpoint každé vlny

1. Uzavřít skutečné form/command/caller mapování pro ownera. Source claim ≠ runtime coverage. Žádná mechanická implementace podle starého souborového názvu.
2. Nejdřív failing owner test na stale replacement v nezávislých kontextech; oprava podmínky/migrace/perimeter. Pak adapter a UI behavior.
3. Přidat explicitní list/form/canvas policy a class-by-class impacted tests. Bezprostřední testy pouze cílené; případné širší cross-cutting checkpointy zmrznout na konkrétním SHA, nespouštět pořád dokola.
4. Změřit query/render/subscription dopad a ověřit real owner / main UI. Úspěšný sandbox není sám proof reálné serializace či DB CAS.
5. Zapsat dokončené IDs, důkazy, známé neprovedené lanes a další scope. Chyby neukrýt přejmenováním nebo přepisem test baseline.

## Konečné dokončení

Všechny skutečné main-UI mutable surfaces a engine writers mají explicitní disposition a test. Kde nebyl live list refresh vhodný kvůli drahé query, je vědomé `DEFERRED-LIVE` s odůvodněním a viditelným refresh; stale save protection není odložená. Bez dokumentovaného aktuálního writer closure nelze říct „engine-wide concurrency hotovo“.

Závěrečný zmrazený shared/regression checkpoint podle repo politiky, portability-static a dokumentace, source reference integrity, auth/privacy a owner migrations. Multi-instance feed, plné ownership a persisted Undo jsou samostatné skutečně nepřijaté vlastnosti, pokud nebyly mezitím explicitně objednané a prokázané. Nikde je nepropagovat jako automatický přínos této implementace.
