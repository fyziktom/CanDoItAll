# Revize WF1 a další krok: Agents Completion AC1

## Závěr

WF1 dokončilo vybranou renderovací rodinu canvasu a souvisejících dialogů. Architekturu bych zachovala. Celé Agents ale ještě obsahuje skutečné zbytkové renderery: shell, trojici Usage dialogů a části runtime/floating prezentace. Nový bundle je dokončovací větší celek pro Agents, nikoli opakování Workflow nebo zahájení Workbenche.

Kontrolovaný hlavní commit: `33007c1c693c8ae6591ee0f201ed9526bbaaac78`. Components development je nově zveřejněné na `2eccdddd05a9b1b0c90935ddee49dc559fd5eec1`; přímým rodičem je oprava Tooltipu `b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`. Dřívější problém nedostupného pushnutí je tedy vyřešený. Stále je nutné ověřit skutečné použití konkrétní verze při buildu a doručené assety; staré reporty se nepřepisují.

## Opravy na začátku

**SCH-R1: start aplikace po vyčerpání konečného CRON.** WF1 prokázalo skutečný výpadek restartu. Scheduler znovu vytvoří trigger a bez rozlišení vyčerpání předá plán do ScheduleJob. Výjimka pokračuje přes awaited StartAsync a zablokuje host. Ruční pozastavení jednoho testovacího plánu obnovilo fixture, ale produktovou chybu neopravilo. To není kosmetika; oprava skutečného vlastníka projekce a restartový test jsou povinné před další extrakcí. Nesmí se přitom změnit historie, zdrojová oprávnění, chování budoucích a zmeškaných běhů ani slepě opakovat nevyřešený admission. Podklady S01–S03, externí Q1/Q2.

**WF1-R1: ID předchozího preview u nového neznámého pokusu.** Stejný WorkflowPreviewOwner zůstává aktivní pro opakované preview. Po úspěchu A uchová ReservedRunId. Při následujícím B jej před dispatch nenahradí novým pokusovým stavem; obecná výjimka po dispatch tak může vrátit Unknown s ID běhu A. Jde o chybnou atribuci výsledku, nikoli prokázané dvojí spuštění. Tento nález je odvozený ze zdroje a má být nejprve reprodukovaný testem. Dřívější potvrzená fakta se mají zachovat jako historie; do výsledku B patří jen rezervace skutečně doložená pro B. Podklady S04–S06.

## Hodnocení implementace

Pozitivní je bezeztrátové zachování vstupních parametrů a bohatších grafů, oddělení nativního Save/preview/Prompt bindingu, identita přijaté verze před následným refreshem a zachování stejného editoru při přepínání záložek. Renderovací shell nebyl zatížen canvasovými runtime závislostmi. Záznam popisuje reálné gesta, nativně uložené souřadnice, původní Prompt verzi a obsah souborů; nejde jen o počet DOM prvků nebo toasty.

## Testovací výsledky a hranice

Implementátor zaznamenal 30/30 nezávislých případů, 146/146 nativních a další cílené opravy (například library acquisition 95/95). Výběry se překrývají. Protokol 19/19 běžel na předchozím image; finální image poté ověřil dotčené Workflow/History/native cesty. Šestipřípadový finální consumer pokus byl 5/1 a grafová cesta má následný samostatný úspěch. History/source/circuit je 3/3; Back/Forward navigace další 1/1. Není správné z toho vytvořit jeden původní all-green běh.

Zmrazený Stable: 27 assemblies, 16 327 discovery, 16 382 provedených, 16 375 úspěšných, 7 selhání, 0 přeskočených. Rozdíl 55 dynamických případů je vysvětlený. Čtyři zastaralé Unit guardy mají 36případovou opravu, dvě starší notifikace setup testu mají 4případovou opravu; poslední nález patří retained syntetickým kontrolám secret scanu. Pozdější library/CSS opravy mají cílený důkaz, nikoli další celý Stable nad konečným HEAD. Report toto odlišuje poctivě.

Tato revize produktové testy neopakovala: prostředí nemá dotnet ani Docker a privátní TRX/logy/media nebyly přiložené. Výsledky jsou převzaté z implementačního záznamu; vlastní kontrola byla zdrojová. Nemám podklad označit celou aplikaci za release-ready.

## Co přesně zbývá v Agents

| Oblast | Co udělat |
|---|---|
| Shell | Vyčlenit skutečný header/statistiky/navigaci; zachovat route a native kontext. |
| Usage | Dokončit všechny tři skutečné grafy/tabulky a jejich lazy query hosty; zachovat okno, partial/unknown/unpriced význam. |
| Runtime a execution log | Přesunout skutečné read-only renderery; zachovat redakci, konkrétní běh, copy a navazující konzumenty. |
| Floating chat | Oddělit strip kontextu a původní close-choice/switch prezentaci, ne služby pro context binding nebo durable run. |
| Voice/Floating Settings | Už mají lehké surface; pouze audit a doplnění skutečných scénářů, žádná duplikace. |
| Standardní chat/SimpleChats | Již z větší části skládají hotové renderery; vyřešit skutečné zbytky a ponechat oprávněné native adaptéry. |
| Staré/developer komponenty | Prokázat dosažitelnost přes skutečné a dynamické callery; nevytvářet novou obrazovku kvůli existenci API metody. |

Preferuji rozšířit existující AgentFramework.UI a sandbox místo nového projektu pro každou drobnost. Úplný seznam musí být v dalším běhu dokončen nad aktuálním stromem; zde je tělo části souborů prověřené detailně, některé kandidáty potvrzuje pouze přesná tree položka a jsou tak výslovně označené.

## Rozsah většího běhu

S0 opraví obě konkrétní chyby. A1 uzavře aktuální census. A2 vyčlení shell/Usage. A3 runtime a floating adjuncts. A4 dokončí audit/proof již existujících Voice/Floating/SimpleChats integrací a posledních dosažitelných rendererů. A5 provede samostatný publish, native a mezikontejnerové scénáře, opakovaný restart Scheduleru a podpisy.

Výsledek musí rozlišit dokončené hranice UI, opravené blokující produktové nálezy, skutečný zdrojový pár a obecnou release připravenost. Velké code-behind soubory se nemají rozbíjet jen pro počet řádků. Po skutečném uzavření Agents bych pokračovala Workbench — menší kalendářové/task/read plochy před Structure a soubory — a Processes jako posledním velkým modulem.

Nové vizuální ověření pouze 1920×1080; bez mobilního ladění. Podepsané commity po ucelených etapách, včasné native PGP odemčení, bez unsigned fallbacku/push/merge. Historické bundles zůstávají. Žádné nové placené inference a žádné resetování běžícího prostředí na 5032.
