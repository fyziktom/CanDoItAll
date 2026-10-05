# Revize AC1 a návrh Workbench Planning WB1

## Verdikt

Kontrolovaný hlavní commit: `9969913fe653500a59d24af06482f48b758a5ca6`, větev `components-decoupling`.
Components development: `2eccdddd05a9b1b0c90935ddee49dc559fd5eec1`. Obě vzdálené větve byly dostupné.
Závěr ze zdrojové revize: dokončení aktivních renderovacích hranic Agents včetně Workflow
bych zachovala a pokračovala Workbenchem. V prověřených změnách jsem neidentifikovala nový
blokující AC1 produktový problém. Nejde o nové spuštění aplikace ani potvrzení release readiness.
Zdroje S01–S14 jsou uvedené v SOURCES.md; rozsah čtení je tam výslovně omezený.

## Správně zapracované opravy

Scheduler nejprve připraví rozhodnutí nad skutečným triggerem a historií; teprve poté mění
runtime job. Vyčerpaný enabled plán tak nepotřebuje být smazaný nebo pozastavený, aby host
nastartoval. Nativní testy ověřují dvě náhradní aplikace nad zachovanou databází, původní
admission a běhy, budoucího souseda i odmítnutí nevalidní náhrady před odstraněním zdravého
jobu (S08–S10). Report navíc uvádí skutečný plán vytvořený UI a dva restarty všech tří instancí.

Workflow preview nyní nuluje pouze rezervaci začínajícího pokusu. Historicky potvrzený běh
zůstává oddělený a nový neznámý výsledek nepřebírá jeho ID (S11). Usage host má přesný
přijatý dotaz, samostatný úklid čtení a ukončení při změně vlastníka/profilu/autentizace (S12).
SimpleChat Start/Rename/Archive přešly do skutečné lehké surface s původem každého otevření (S13).

Udržovaný soupis uvádí 247 komponent: 117 izolovaných rendererů, 57 legitimních nativních
hostů, 20 neutrálních primitiv, 39 vývojových ploch a 14 zachovaných legacy typů. Samotná
existence Razor souboru či velkého code-behind proto není důvodem znovu otevírat Agents.
Celý seznam jsem nenahrazovala vlastní novou behaviorální revizí každého řádku; ověřila jsem
hlavní změněné cesty a aktuální vysvětlení aktivací/assetů (S06–S07).

## Výsledky testování a jejich hranice

Codexův zmrazený Stable uvádí 16 490 provedených případů: 16 489 úspěšných, jedno selhání,
nula přeskočení, 27 assemblies. Jediné selhání je identifikovaný syntetický security control
v historických souborech. Celý běh zůstává neúspěšný, nikoli zpětně all-green. Všech 3 287
Integration případů prošlo. Pozdější dialogové změny mají samostatné čerstvé ověření 28 případů.
Původní protokolové/Workflow vektory jsou zčásti převzaté při shodě jejich vstupů; nelze je
vydávat za nově spuštěné na pozdějším image. Finální změněné konzumenty mají vlastní důkazy.

Report uvádí standardní a floating chat, skutečné schvalování, souborové a obrazové bajty,
Usage/History a mezikontejnerovou identitu. Zaznamenává i expiraci fixture credential a opravu
předčasného odstranění response planu. Staré neúspěšné pokusy zůstávají rozlišené. Žádný z těchto
C#/.NET, Docker, PostgreSQL, browserových nebo watch běhů jsem zde neopakovala; příslušné
runtime nástroje ani privátní TRX/logy nebyly k dispozici (S06).

## Další větší celek: Workbench Planning WB1

Zadání pokrývá celý projektový kalendář, interaktivní Gantt včetně všech úprav a exportů,
a oba task editory (Gantt a obecný Structure formulář) se skutečnými editory odhadu, stavu,
resource pickerem a cenovým náhledem. Nejde o celý velký Structure canvas, manager summary,
assignment/runtime/file plochy ani Processes. Ty mají zůstat funkční ve stávajících hostech.

Preferovaný projekt je `CanDoItAll.Workbench.Planning.UI` a odpovídající nezávislý sandbox.
Případný úzký Contracts projekt musí být skutečně lehký. Smíšený `ProjectWorkbenchModels.cs`
obsahuje EF modely i celé služby; nesmí se přesunout jako jeden balík jen kvůli názvu Models.
Nativní Workbench/Projects/CRM zůstávají jedinými vlastníky dat, cen, přiřazení a oprávnění.

## Konkrétní nová oblast pro opravu — WB1-Q1

Starší `ProjectStructureTaskResourceCostEstimator` váže čekající quote na resource a estimate,
ale ne na execution eligibility. Pokud požadavek začne ve stavu NotStarted a tentýž editor
mezitím přejde do Started/Unknown bez změny odhadu, může se pozdní původní částka stále aplikovat.
Rodičovský Gantt dialog takovou změnu execution propouští samostatně. Existující test ověřuje
Started již před požadavkem a změnu effort/manual cost, nikoli tento přechod během požadavku
(S21, S23–S24). Zadání vyžaduje nejprve deterministickou i rodičovskou reprodukci.

Toto není nalezená regrese AC1 ani prokázané poškození uložené ceny. Nativní TaskApplicationService
ji znovu kontroluje a u úloh mimo povolené repricing stavy zachovává historickou částku (S27).
Oprava náhledu však musí zabránit zavádějícímu zobrazení a práci s cizím kontextem. Nesmí odstranit
nativní kontrolu, vyrobit cenu nula ani rozbít dosavadní explicitní a cache chování.

## Další zásadní podmínky

Skutečný kalendář je read-only. Vedlejší `BuildValidationSurface` vytváří syntetické checklisty,
playlisty a editovatelnost; ty se přesunou do přiznaných sandboxových demonstrací, nikoli do nové
produkční CRUD pravomoci (S16–S17). Persistovaný stav zobrazení zůstává skutečný nativní zápis
s vlastními pravidly pořadí a původního projektu.

Gantt musí dál ukládat reálné task/link identity a původní expected schedules, ne jen přesouvat
bary v browseru. Oba task editory zachovají rozdíl mezi osobou/agentem jako přímým assignee a
připojením Workflow/Process definice. Save může zahrnovat více nativních fází a kompenzací;
potvrzený první krok se nesmí ztratit při následné chybě. UI pozorování není oprávnění (S19–S27).

## Uzavření dalšího běhu

Nejprve krátká návaznost na AC1 a reprodukce/oprava WB1-Q1; potom všechny tři plánovací rodiny.
Cílené testy během etap, skutečný source/published sandbox a nakonec nativní gesta, task/assignment/
pricing read-back, agent nad Project Structure, schvalování a soubory, Workflow/History/Usage
a zachovaný restart Scheduleru. Široký Stable pouze podle skutečného finálního zásahu a pravidel
repozitáře, ne po každém dialogu. Jednotlivá selhání se nemažou ani nepřeznačují.

Velká obrazovka 1920×1080, žádné mobilní ladění. Podepsané větší commity a včasné odemčení
PGP přes nativní pinentry; bez push/merge, mazání historických bundles a placených modelových
volání. Po WB1 zbývají další Workbench read/Structure/integration řezy, Processes až nakonec.
