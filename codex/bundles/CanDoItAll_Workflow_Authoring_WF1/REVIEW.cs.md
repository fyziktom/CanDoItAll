# Revize CA1 a navazující Workflow Authoring WF1

## Závěr

CA1 bych zachovala a pokračovala k Workflow authoringu. Capability wizard/detail i technické týmy mají
skutečné oddělené renderery; není důvod vytvářet další náhradní editor agenta. Našla jsem jednu
konkrétní ohraničenou chybu při přiřazení výsledků opakovaného setup testu. Je vhodná na začátek
navazujícího bundle, nikoli na další samostatnou closure iteraci.

Kontrolovaný main: `ccca2fd3a7c4239d2869e8143617fd9ab04c3723`, větev `components-decoupling`.
Kontrola zahrnula předcházející implementaci i závěrečnou testovací zprávu, ne pouze poslední commit.
Výchozí srovnání je proti `b3aec979eae708edd0a53b42cef41b9d52fbb49b` a obsahuje sedm commitů.
SHA jsou původem revize, nikoli požadavkem přepnout nebo resetovat checkout.

Zdroje jsou vedené pod identifikátory S01–S29 v [registru](SOURCES.md). U každého je uvedený rozsah
čtení. Jde o zdrojovou revizi a posouzení zaznamenaných výsledků, ne o nové spuštění celé aplikace.

## Co je správně

Capability authoring používá jeden draft, formulářový kontext a zachycené požadavky. Setup je
explicitní externí operace odlišná od Save i A2 Verify. Přijaté ID a fingerprint se přebírají před
navazující obnovou rodiče a novější text se nezahazuje plošným nahrazením formuláře. Nativní služby,
profilové změny a skutečné spouštění nástrojů zůstávají v produkčním hostu. S02, S05–S07, S29.

Týmový metadata Save zachytí odesílané jméno, popis a ikonu, ale členství převezme z aktuálního záznamu
přímo v koordinovaném zápisu. Starší editor názvu tedy nemá vrátit původní členství. Původní ne-UI
full-team upsert zůstává oddělený; není plošně přepsaný kontrakt ostatních spotřebitelů. S08–S09.

MCP checkpoint oprava je ohraničená na podporované typy instalovaného SDK a jeho serializér. Neznámé
raw objekty a nekompatibilní typ/verze se dál odmítají. Změna neznamená vypnutí schvalování ani opakování
již provedeného externího efektu. S10–S11. To je významná oprava skutečné runtime návaznosti nalezená
nativním testem; nevznikla místo ní druhá implementace MCP v UI.

## CA1-R1: staré diagnostiky se mohou tvářit jako výsledek nového testu

První setup test A úspěšně vrátí diagnostiku a seznam nalezených MCP nástrojů. Po změně konfigurace
na B se tento výsledek správně považuje za historický. Druhý setup B ale skončí výjimkou s neznámým
potvrzením. Aktuální `catch` nastaví novou `setupRevision`, `SetupUnknown` a vynuluje příznak úspěchu,
ale ponechá `setupDiagnostics` a `setupTools` z A. Jejich gettery porovnávají pouze novou revizi;
skutečný panel je vykresluje nezávisle na nullable příznaku úspěchu. S02–S03.

Výsledkem není nutně falešný zelený badge — ten je správně prázdný — ale starý seznam nástrojů a
stará diagnostika zobrazené u současného neznámého pokusu. Je to chyba původu informace, ne prokázané
obcházení autorizace nebo poškození uložené konfigurace. Novou runtime reprodukci jsem zde neprováděla.

Oprava má držet fakta jednoho pokusu pohromadě, případně odstranit pouze zastaralé seznamy z aktuálního
panelu. Neznámý efekt musí dál blokovat slepé opakování. Regresní test má projít skutečným formulářem
pro změněnou i stejnou revizi, A → B → A, skutečné neúspěšné diagnostiky, dvě instance a zavření editoru.
Existující test už ověřuje pozdější psaní během jednoho úspěšného testu, ale ne tuto sekvenci dvou
výsledků. S04. Podrobnosti jsou v [S0](S0_CARRY_OVER.md).

## Testy CA1

| Zaznamenaný výběr | Výsledek a omezení |
|---|---|
| Source/published sandboxy a native authoring | 8/8; zahrnuje 24 capability scénářů a reálné rodiče/týmy |
| Nativní sdílení a konzumenti | 10/10 podle finálních běhů a konkrétních následných ověření |
| Sdílející instance a dva klienti | 19/19 protokolových scénářů |
| Finální Stable | 16 324 provedených, 16 315 úspěšných, 9 selhání, žádný vybraný případ přeskočený |
| Osm katalogových fixture selhání | Chybějící registrace profilové služby; následně všech 10 případů opravované rodiny prošlo |
| Poslední původní selhání scanneru | Historické a generované syntetické negativní kontroly; source-only follow-up je jiný deklarovaný rozsah |
| MCP oprava | 14/14 codec selection a 73/73 skutečných journal/restart kontrol podle reportu |

Všechna čísla jsou převzatá ze zprávy implementátora, nikoli z testů mnou znovu spuštěných. Výběry
se překrývají. Následné úspěšné kontroly nemění původní Stable na jediný all-green běh. S01.

Důležité je, že nativní kampaň používá skutečné MCP/HTTP/process setup efekty, přesné approval/denial,
Project Structure soubory s read-backem a oba sdílené klienty. Externí modelová odpověď je řízená fixture,
ale backendoví vlastníci nejsou nahrazení fiktivním úspěchem. Část helperů měla původní chyby readiness;
report je uvádí odděleně od finálních výsledků. Starší timing příčina z PP3 zůstává nepotvrzená.

## Components zůstává otázkou doručení, ne nové implementace

Lokálně otestovaná oprava Tooltipu je `b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`. Vzdálené
`development` v této revizi stále ukazuje na `4a858412d2c2a3f6123bf23d8c4584f05b47627d` a načtení
opraveného commitu přes GitHub skončilo 404. Report samotný jej popisuje jako nepushnutý. S01.

Codex nemá opravu vyrábět znovu. Má ověřit existující lokální commit, podpis, skutečně načtené assemblies
a assety a s touto ověřenou dvojicí může pokračovat. Vzdálená reprodukovatelnost musí zůstat samostatně
kvalifikovaná. Současné CI volí matching Components větev; shodné číslo balíčku samo o sobě nestačí. S26.

## Větší další celek: Workflow authoring WF1

Existující `Workflows.UI` už má shell, katalog, history, templates, overview a analytics. Je velmi lehké
a nemá se kvůli této práci znovu vyrobit ani připojit k celému canvasovému grafu. S12–S13.

Nový bundle zahrnuje celý skutečný canvas, toolbox a plovoucí okna, grafové a uzlové inspectory,
konfiguraci executorů včetně image rendereru, přípravu a vazbu LLM komponent/Prompt Gallery a všechny
zbývající page-owned dialogy: šablonový náhled, oba vstupy pro preview, detail běhu a detail události.
Route, všech pět záložek, Curator, profilová životnost a runtime vlastníci zůstanou v produkčním hostu.
Nejde o přepis Workflow runtime, Workbenche nebo Processes. S14–S19.

Doporučená nová rodina je `CanDoItAll.AgentFramework.WorkflowAuthoring.UI` a její samostatný UiSandbox.
Původní shell se využije kompozicí. Další Contracts/Presentation projekt jen při konkrétní potřebě,
ne kvůli formální symetrii. Závislosti se vyhodnotí podle skutečného tranzitivního grafu.

### Proč zde nestačí přesunout soubory

Současný canvasový dokument nenese `InputParameters`; Save je nepředává, zatímco nativní persistující
vlastník příchozím seznamem parametry nahrazuje. To je konkrétní cesta, kterou musí první nativní
round-trip test nového řezu reprodukovat a uzavřít. S17–S18, S20. Nepřisuzuji ji CA1 — je v dosud
neodděleném starším Workflow editoru.

Současná redukovaná projekce také znovu vytváří porty a jednoduché shapes. Proto zadání vyžaduje
porovnání všech podporovaných neúpravovaných dat: bohatších schémat, port ID a null významu,
komponentových snapshotů, routingu, legitimních nulových souřadnic, konfigurací a metadat. Neznamená to
nové UI pro všechna pole. Znamená to nezahodit je při změně názvu existujícího workflow.

Druhá podstatná hranice je potvrzený Save versus read-back a nové psaní. Dnes se vymění celý dokument
a rodič má navíc `@key` podle uložené verze. Oprava musí zabránit ztrátě novějšího draftu v obou vrstvách.
Přijatá nová verze není automaticky novým uživatelským výběrem; cizí souběžný zápis ale musí dál vyvolat
správný konflikt. S14–S17, S20.

Prompt binding navíc vytváří samostatnou uloženou komponentu před dalšími callbacky. Její potvrzené ID
se nesmí ztratit nebo opakovaně vytvořit jen kvůli selhání obnovení knihovny. Preview je skutečné
spuštění s vlastním admission, nikoli validace formuláře. Zavření canvasu neprokazuje, že neproběhl efekt,
a pozdní progress nesmí zaměřit stejnojmenný uzel v jiném dokumentu. S17.

## Rozsah ověření a další pořadí

WF1 má 32 seskupených oblastí ověření. Obsahuje nezávislý skutečný canvas v source/publish režimu,
bohatý nativní round-trip, Save/konflikty, Prompt komponenty, přijímané i neúplné výstupy, podporovaný
human/external response, skutečné doručení Schedulerem, TestLab a Project Structure soubory. Finální
sdílející instance a dva klienti znovu ověří zobrazená jména i opaque route IDs nad novým image.

Během etap mají běžet cílené testy s ověřeným discovery. Jeden záměrný závěrečný Stable checkpoint se
přidá podle skutečných zásahů a current invalidation pravidel, ne po každé záložce. Žádná nová placená
inference, mobilní ladění nebo reset starého live rozpočtu. Primární viewport je 1920 × 1080. Podepsané
commity vznikají po větších ucelených etapách, nikoli jako povinná záplava mikrocommitů.

Po tomto celku zůstanou menší zbytkové chat/usage/Voice plochy k přesnému soupisu, potom Workbench a
Processes jako poslední velká oblast. WF1 neznamená automatické vyřešení každé zbývající obrazovky
v celé assembly AgentFramework. [Roadmapa](ROADMAP.md) rozlišuje skutečné renderery a legitimní hosty.

## Omezení revize

Aplikaci jsem zde nesestavovala ani nespouštěla její .NET, PostgreSQL, Docker, browserové nebo watch
testy; v prostředí není dotnet ani Docker. Původní privátní TRX, úplné logy a média nebyly předané.
Citovaná čísla jsou výsledky implementátora a nálezy ze zdroje jsou takto výslovně označené.

Balíčkové validátory ověřují pouze integritu, odkazy a konzistenci evidenční struktury. Výchozí šablony
nic nepředstírají: všechna produktová ověření jsou NOT_RUN. Záznam o skutečně provedených lokálních
kontrolách předání je v [PACKAGE_VALIDATION.md](PACKAGE_VALIDATION.md).
