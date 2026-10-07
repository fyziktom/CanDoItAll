# Revize WB1 a navazující Workbench Insights / Selection WB2

## Závěr

WB1 bych zachovala jako dokončenou plánovací rodinu: Calendar, celý Gantt a oba task
editory se skutečnými potomky. V prověřených nativních cestách jsem neidentifikovala
nový důvod pro plošné přepracování persistence nebo oprávnění. Našla jsem jednu
konkrétní mezeru v úklidu sdíleného Gantt interopu; patří na začátek většího WB2.
Nejde o prohlášení celé aplikace za bezchybnou nebo připravenou k release.

Kontrolované hlavní SHA: `be2045c312ee5fa99fb5b1f8526f1825ecf8352c`, větev
`components-decoupling`. Kontrolované Components `development`:
`dc573e2b438621599401a28968acef3682d14e63`. GitHub potvrzuje podpis hlavního HEAD.
Oprava Ganttu už je ve vzdáleném Components dostupná; poznámka původního reportu
„local-only“ popisuje tehdejší stav a nesmí vést k opětovnému zadávání téhož pushnutí.

SHA identifikují zdroje revize, nikoli povinný checkout. Podklady a přesné prohlédnuté
rozsahy jsou v [registru zdrojů](SOURCES.md). Historické bundles zůstanou zachované.

## Co je na implementaci dobré

Nové UI skutečně odkazuje na neutrální sdílené komponenty a lehké Planning.Contracts,
ne na implementaci Workbenche [S02–S03]. Contracts nyní také generuje XML dokumentaci.
Report uvádí devět projektů v renderovací větvi a deset v sandboxu při zachování
19 předchozích chráněných závislostních grafů. Tyto grafy jsem zde nepřepočítávala.

Původní chyba cenového náhledu je ošetřená přes identitu vstupu, execution state,
projekt/profil/životnost, otevření editoru i konkrétní callback. Změna A-B-A nezaručuje
aktuálnost původního dotazu. Cancellation source se uvolňuje až po skončení jeho
resolveru [S04]. Zůstávají lazy cache a explicitní refresh.

Výsledky task operací zachovávají identity a rozlišují task, assignment, pricing,
attachment, pořadí a kompenzace [S05]. U editace se před obnovením původního přiřazení
kontrolují skutečná očekávaná task pole uvnitř koordinovaného writeru [S07–S08].
Při ztraceném potvrzení cenového commitu se již vytvořený attachment neodstraní
naslepo; známé odmítnutí má odlišnou kompenzační cestu [S09]. Tyto změny bych nevracela.

## Drobná předřazená oprava WB2-S0

`GanttChart.DisposeAsync` nejprve čeká na `pendingInterop` a až potom volá
`DisposeInteropAsync`. Aktualizace zachytává pouze `JSException`. Pokud už úspěšně
existuje JS canvas, jeho pozdější čekající update skončí zrušením a současně se
komponenta zavírá, čekání vyhodí výjimku před zavoláním úklidu canvasu. Vnější finally
uvolní .NET callback reference, ale JS dispose tím nenahradí [S11].

Toto je závěr ze zdrojového toku, nikoli zde spuštěná runtime reprodukce nebo důkaz
poškození dat. Dosavadní nové testy ověřují pomalé úspěšné create/update a úspěšné
zavření po pozdním create, ne uvedenou chybovou větev [S12].

Nový běh má nejprve vytvořit deterministickou regresi nad pravou komponentou, potom
opravit nezávislost úklidu na výsledku předchozí operace. Musí zachovat serializaci
aktualizací, poslední model, dvě nezávislé instance a viditelnost skutečných aktivních
chyb. Nepovoluje se obecné potlačení JS výjimek, delší timeout, lokální kopie Ganttu
nebo návrat k paralelním interop voláním.

## Výsledky testů a jejich hranice

Následující výsledky uvádí Codexův report [S01], nikoli vlastní opakované spuštění:

| Oblast | Výsledek |
|---|---|
| Nativní plánovací rodina | 251 úspěšných případů |
| Nezávislá plánovací rodina | 44 úspěšných případů |
| Zmrazený Stable | 16 633 provedených: 16 629 pass, čtyři fail, nula skip, 28 assemblies |
| Následné API pipeline/coverage | 32 úspěšných případů nad opravami dokumentace |
| Konkrétní Workspace status follow-up | Dva úspěšné případy po opravě čekání na připravenost |
| Nativní consumer kampaň | Devět úspěšných případů na image b58ca3d2… |

Výběry se překrývají a nedají se sečíst jako unikátní pokrytí. Původní Stable se
nezměnil na all-green: dvě chyby dokumentace a jedna testovací synchronizace mají
samostatné opravy; čtvrté selhání je přesně identifikovaný syntetický negativní
control v historických artefaktech. Neoslabovat scanner ani nemazat původní důkazy.

Report přiznává chybějící úplný předběhový manifest hashů všech assemblies. Pozdější
hashové kontroly nelze přepsat jako důkaz zachycený před spuštěním. Poslední image
`a00017cc…` obsahuje pouze další metadata/dokumentační opravu a vlastní ověření.
Devítipřípadová kampaň patří předchozímu `b58ca3d2…`, ne tomuto poslednímu image.
Plných 19 protokolových vektorů sdílení se ve WB1 znovu nespouštělo ani netvrdilo.

Watch měření uvádí po třech vzorcích pro Razor, C#, CSS a JavaScript; několik typů
používalo automatickou browserovou navigaci při stejném PID. To není důkaz, že
všechny editace proběhly beze změny stránky, ani kontrolované zrychlení celého Webu.

## Další větší část

WB2 obsahuje celý Manager Summary, jeho volby, explicitní načtení, preflight a
potvrzení rozsahu, průběh, kompletní metriky/grafy a varování. Navazuje celý dialog
aktivit: Agents, SimpleChats, Workflows a Processes se skutečnými souhrny a jejich
odlišným stránkováním. Dále dokončí celý Selection Panel, Object Index, Signals,
Canvas Health a skutečné rozšířené detaily [S13–S24].

Nejsou to jen read-only panely. Object Index umí kontextové a hromadné mazání,
Signals skutečně zapisuje markery/progress/priority a Selection otevírá task,
souborové a runtime akce. Nové callbacky musí nést původní projekt, životnost,
výběr a otevření. Backendová oprávnění zůstávají u původních vlastníků.

U reportů je důležité zachovat oddělení rozepsaných voleb a přijatého reportu.
Změna period před Load nesmí reinterpretovat starý graf a nová odpověď starého
page requestu nesmí naplnit cache pod jinými filtry [S13–S14]. Původní omezená
retence podle profilu/projektu zůstane, ale nebude sdílet transientní operace či
rozepsané stavy dvou otevřených panelů [S16, S28]. Process cursory zůstanou nativní;
kvůli jednomu DTO se nevtáhne celý Processes backend do renderovací knihovny.

Předpokládané umístění je `CanDoItAll.Workbench.Insights.UI` a odpovídající sandbox.
Další Contracts projekt pouze pro skutečnou lehkou hranici. Hotový Planning ani
ostatní moduly nesmějí získat zpětné reference. Hlavní Structure canvas, jeho
node authoring, runtime/files a Process launch/recovery se v tomto běhu neextrahují.

## Ověření a předání

Zadání obsahuje 29 skupin důkazů, nikoli povinný počet tříd či testů. Vyžaduje skutečný
source/published sandbox, nativní souhrny a správné cíle zápisů, konflikty a odmítnutí,
agentový/file průchod a relevantní Workflow/Scheduler/History návaznosti. Externí
model může být deterministický, vlastníci ani schvalování nesmějí být simulovaným
úspěchem. Nový placený inference rozpočet není povolený.

Velké obrazovky 1920×1080, žádný mobilní tuning. Podepsané commity po celcích, včasné
PGP odemčení přes nativní pinentry, zachování signing prostředí, bez push/merge.
Širší Stable pouze podle skutečného finálního dopadu a aktuálních pravidel, ne po
každé záložce. Povinný portability-static musí mít finální enforcement.

Revize zde byla zdrojová. Neproběhl C# build, .NET/PostgreSQL/Docker/Playwright ani
watch běh. Privátní původní TRX, logy a média nebyly dostupné. Validace dodaného ZIPu
a Python nástrojů ověřuje jen předávací balíček; produktové výsledky začínají NOT_RUN.
