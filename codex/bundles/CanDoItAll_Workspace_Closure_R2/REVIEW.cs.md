# Revize Workspace closure a zbývajících UI modulů

Datum revize: 30. září 2026. Větev `components-decoupling`, HEAD
`15eadc18932e77a20ae5be2d7f2b207700a15d25`; při závěrečném ověření se nezměnil.
Zdrojové odkazy a rozsah čtení jsou v SOURCES.md / sources.json.

## Závěr

**Oddělení renderování Workspace je dokončené. Funkční uzavření aplikace zatím ne.**
V prověřených opravách databázových profilů a životnosti Simple Chats jsem nenašla důvod
návrh znovu přestavovat. Přetrvávají ovšem WCL-DEL1 a WCL-NAV1, tedy skutečně pozorované
nepotvrzené mazání agenta po dokončených bězích a neobsloužené zrušení navigace společného
hostu. Jejich příčina zatím není prokázaná, takže je nelze poctivě označit za drobné
kosmetické úpravy vhodné k připojení k nové extrakci. [R02–R04]

Nový bundle je proto cílená uzavírací iterace R2. Má oba známé nálezy reprodukovat,
diagnostikovat a podle prokázané příčiny opravit, nikoli znovu pouze přepsat jejich mapování.
Nevytváří další Workspace projekt a nezadává refaktoring Projects ani Processes.

## Opravy, které zachovat

U databázových profilů nyní kontrola přesného editorového cíle probíhá pod skutečnou
koordinací zápisu. Souběžně odstraněný záznam se tedy v prověřené cestě nemá znovu vytvořit
prostřednictvím původního upsertu. Historické ne-UI ukládání si přitom svůj kontrakt zachovává.
Potvrzený zápis katalogu před následnou chybou výběru nese konkrétní ID a vrací částečný
výsledek. Testy obsahují řízené pořadí dvou vlastníků a zachování fyzických dat. [R05, R06]

Simple Chats drží nejen semafor a token, ale také skutečný asynchronní scope závislostí
přijaté operace. Test s reálným EF kontextem nechá čtení doběhnout po zrušení původního
volajícího scope a teprve potom ověřuje uvolnění kontextu. To je vhodnější než plošné
potlačování ObjectDisposedException nebo převod služby na singleton. [R07, R08]

MainLayout má nyní identitu své životnosti, navigační generace a konkrétního browserového
listeneru. Tyto ochrany zachovat. Zbývající navigační stack je však jiná cesta než původní
zápis browserového stavu, takže samostatné úspěšné kontrolní testy jej neuzavírají. [R09]

## WCL-DEL1 — mazání agenta

Implementátor popisuje dvě dokončené interakce agenta přes skutečný runtime se skriptovaným
externím providerem. Následné potvrzené Delete vrací zprávu, že výsledek nelze potvrdit;
stejný agent je stále přítomný po novém načtení. Ruční pokus problém zopakoval. Žádný
úspěšný rollback, definitivní smazání ani příčinu ve FK či aktivním lease z toho nelze odvodit.
Původní zašifrovaný souborový stav a databázový dump mají být zachované soukromě. [R03]

Nové zadání vyžaduje získat bezpečnou informaci o konkrétní fázi a typu výjimky. Sleduje
skutečné čtení katalogu, indexů, sestavení plánu, validaci journalu, zápis jednotlivých
částí a navazující projekci. Zakazuje ruční přepis indexů, nucené odstranění, vymazání
lease, uvolnění oprávnění nebo odstranění původní testovací podmínky. [R10, R11]

Při čtení kódu jsem doplnila důležité upřesnění: **absence pending journalu sama neprokazuje,
že se nezapsalo nic**. Před jeho vytvořením může čtení indexu pro mazání opravit a zapsat
počet sessions. Stejně tak přetrvávající řádek agenta neprokazuje nezměněnost všech ostatních
úložišť. Proto jsou v opravě povinné skutečné before/after kontroly a existující hranice
obnovy po přerušení. Toto není nově spuštěná reprodukce, ale závěr ze zdrojové posloupnosti.

## WCL-NAV1 — navigace a zánik circuitu

Ve společném browserovém běhu byl zaznamenán neobsloužený TaskCanceledException pro navigaci
na `/agents`. Časová blízkost šedesátisekundovému limitu je užitečná stopa; není to důkaz,
že přesně předchozí test je původcem. Aktuální usage test čeká na URL a viditelnost grafů,
ne na potvrzení konkrétního serverového požadavku navigace. [R04, R12]

Bundle proto požaduje propojit původce, circuit, generaci, browserové volání a jeho potvrzení.
Má rozlišit chybu aplikace, chybnou synchronizaci teardownu testu a hranici frameworku.
Oprava nesmí změnit timeouty či retention, globálně potlačit výjimku ani odstranit kontrolu
celého serverového logu. Běžná aktivní chyba musí být nadále vidět.

## Opravené Components zatím nejsou doložené jako vzdáleně dostupné

Report uvádí lokálně otestovaný commit Components
`22d5b21afdf80c2bca74c1c598f0b1bb72c86f9e`. V této revizi jej konektor nenačetl — GitHub vrátil
404 s informací, že ref neexistuje. Seznam vzdálených větví stále ukazoval starší main a
development. Nezpochybňuji tím existenci lokální opravy; nelze ale tvrdit, že ji samotný
push hlavního repozitáře poskytl dalším vývojářům nebo CI. [R13, R14]

Je třeba ověřit přesnou dvojici aplikace/Components a to, kterou větev či SHA skutečně
použije cílový build. Lokální source-mode důkaz, nupkg a publikace správné větve jsou různé
věci. Žádný nevyžádaný push/merge ani lokální kopie Dialogu v aplikaci nejsou řešením.

U FileTools jsem naopak znovu ověřila porovnání lokálního SHA a CI pinu: žádné změněné
soubory. Není tedy vhodné vynucovat aktualizaci jen kvůli rozdílným SHA. Zaznamenat zdrojovou
ekvivalenci a podle potřeby ověřit metadata/binární výstup je přesnější postup. [R15]

## Vyhodnocení testování

| Rozsah | Zaznamenaný výsledek | Význam |
| --- | --- | --- |
| Celý Stable | 15 866 případů; 15 865 prošlo, jeden selhal, nula přeskočeno. | Není to čistý celý běh. |
| Následná oprava testu receiptů | 31 úspěšných případů celé vlastnící testovací třídy. | Silný dílčí důkaz, ne zpětné přebarvení původního Stable. |
| Celý ne-live browser | 171 případů; runner 157 pass / 14 fail. | Nutné rozlišit skutečné chyby od chybějících předpokladů. |
| Po samostatných následných ověřeních | 162 pass / 2 fail / 6 blocked / 1 not-run. | Kombinace samostatných důkazů, ne nový čistý 171-case běh. |
| Živé testy na nové dvojici zdrojů | Nula nových požadavků; chybí nové povolení. | Starých 40/40 zůstává vyčerpaných; historický úspěch není čerstvý pass. |

Jde o údaje ze zprávy implementátora. Původní TRX, kompletní logy ani screenshoty této
revizi nebyly předané. Nový bundle požaduje cílené reprodukce a po ustálení oprav nový
společný Stable/browser checkpoint. Chybějící oprávnění či prostředí zůstávají samostatně
pojmenované blokátory, nikoli důvod přepisovat správný produktový kód. [R02]

## Co ještě zbývá oddělit od backendu

Náročnost je relativní architektonický odhad, nikoli časový příslib. Orientační počet řezů
znamená kompletní hranici s rendererem, produkčním zapojením, sandboxem a testy. Přesný rozsah
je nutné před každým zadáním znovu zmapovat.

| Rodina | Zbývající rozsah | Náročnost | Orientační členění |
| --- | --- | --- | --- |
| Projects | Portfolio, board/karty, editor/hierarchie, souborové a package plochy. | 3/5 u prvního řezu, 4/5 celý modul. | 2–3 řezy; vhodný další hlavní modul po closure. |
| AgentFramework — zbytek | Katalog, Overview a capabilities již oddělené; velký detailový editor a další provider/chat/dialog renderery ještě vyžadují postupné řezy. | 3–4/5. | 2–4 řezy podle skutečného soupisu; neopakovat dokončený katalog. |
| Workflow authoring | Lehký MAF UI projekt existuje, ale celý WorkflowCanvasEditor není z implementace vytažený. | 4/5. | 2–4 řezy; reuse UI základů, zachovat verze/vstupy/admission. |
| Workbench | Calendar a další přehledy, native/task/assignment editory, Structure canvas, soubory/runtime a agentový kontext. | 5/5 celek. | 4–6 řezů; začít menšími plochami, ne celým canvasem. |
| Processes | Katalog/editor, launch, live monitoring, schvalování a recovery; krátké route wrappers neznamenají lehký renderer. | 5/5. | 4–6 řezů; nechat jako poslední velkou rodinu. |
| Simple Chats | Existující UI zachovat, doauditovat hosty, doplňující renderery a scénářové pokrytí. | 2/5 audit, ne nová celá extrakce. | Podle nalezených zbytků. |
| Web dashboard/runtime capabilities | Nejsou samostatné doménové moduly; zkontrolovat opakovaně použitelný renderer proti legitimní kompozici hostu. | 2–3/5. | 1–2 případné závěrečné řezy. |

Sources pro tento plán: aktuální solution, Projects stránka a soupis komponent, Workbench
vlastnictví a adresář, Processes projekt, Agent UI README a skutečné detailové/workflow
renderery. U velkých modulů to není kompletní řádková revize každého souboru. [R16–R29]

CRM/HR, Prompts, Collaboration, TestLab, Plugins, SchedulerPlanner, Memory, Resources a
Workspace již mají své renderovací hranice. Ty se nemají znovu vytvářet. Security nepočítám
jako samostatnou novou UI extrakci bez konkrétní zbývající obrazovky; jeho backendové služby
nepotřebují UI sandbox jen proto, aby měl sandbox každý projekt.

Aktuální `docs/architecture/modules.md` obsahuje zastaralý přehled dokončení. Jeho oprava
je malý úkol nového bundle; historické sealed audity ve shared v3 zůstávají beze změny.

## Doporučený postup

Uzavřít oba funkční nálezy a přesné převzetí Components. Potom vzít Projects portfolio a
skutečný editorový tok jako první další ucelený řez. Pokud zůstanou pouze administrativní
předpoklady živých testů či publikace, jasně je oddělit od produktových chyb a předložit
rozhodnutí provozovateli; nevymýšlet falešný ready stav ani další zbytečné opravy.

Tato revize produkt nesestavovala ani nespouštěla C# či browserové testy: v prostředí není
dotnet. Kontroly předávacího ZIPu a Python nástrojů se týkají pouze předávacího balíčku.
