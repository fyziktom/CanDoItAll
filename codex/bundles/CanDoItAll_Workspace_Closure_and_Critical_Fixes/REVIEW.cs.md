# Revize Workspace a návrh uzavíracího bundle

Datum revize: 30. září 2026. Repozitář `fyziktom/CanDoItAll`, větev
`components-decoupling`, implementace `d9273a88973d28d73c4d29f8686f2b3d68ef8f3a`.
Předchůdce `1080c24163afd3cf65fcc68756913c5dd3262a62` obsahuje předchozí zadání.
Závěrečné ověření reference větve ukázalo stejné implementační SHA.
SHA není příkaz přepnout nebo resetovat checkout dalšího běhu.

## Závěr

Souhlasím s uzavírací iterací bez nového modulu. Oddělení renderování Workspace je
v zásadě dokončené, ale aplikace ještě nemá uzavřené společné ověření. Codex tento
rozdíl v dokumentaci poctivě uvádí; jeho přehled nelze zkrátit na „všechno prošlo“.

Architekturu nových UI knihoven a sandboxů bych zachovala. Opravit je potřeba tři
zaznamenané chyby společných životností, doplnit spolehlivé důkazy dvou živých cest a
vyřešit dodatečně nalezenou mezeru přesného ukládání databázového profilu.

Zdroje jsou podrobně rozepsané v `SOURCES.md` uvnitř ZIPu. R01–R08 označují současné
implementační zprávy; další Rxx jsou konkrétní připnuté zdrojové soubory a testy.
Samotné původní TRX, screenshoty a kompletní serverové logy nebyly revizi dostupné.

## Co je skutečně dokončené

Původní Core, API Access, Storage Catalog a Storage Selection nyní doplňuje samostatné
Recovery UI, Data Sources UI a neutrální konfigurační renderer. Produkční moduly stále
vlastní své adaptéry a skutečné operace. Recovery předává původní context/intent a
přesnou identitu Workflow pokračování; Data Sources má oddělená čtení a vlastní
životnosti editoru/transferu; konfigurační fallback nezavádí správu credentialů.

To, že v modulu zůstaly route, DI, registr důvěryhodných rendererů a kompatibilní
hosté, není samo o sobě nedokončený decoupling. Konečná kontrola má sledovat skutečné
vnořené renderery, typy a závislosti, nikoli počet souborů Razor. Existující census
přesto nesmí tvrdit nulové zbývající riziko u cesty, kde se potvrdí nová chyba.

Důležité dřívější opravy se nesmějí při uzavírání vrátit: zachování návrhů při výběru
stejného katalogu, původní cíle pickerů, API odmítnutí a odstranění citlivých hodnot,
Recovery výsledek před následným čtením, inicializační ochrana Data Sources a předání
MaxOutputTokens do Workflow LLM volání. U posledního bodu jsem ověřila skutečný
konstruktor neutrálního požadavku; nejde pouze o tvrzení v changelogu. (R03, R04,
R12–R16, R29)

## Jak číst výsledky testů

### Stable

Zaznamenaný zmrazený checkpoint má 15 849 provedených a úspěšných případů, bez selhání
či přeskočení. Rozdíl proti 15 794 položkám discovery tvoří 55 rozvinutých datových
případů. To je podstatně silnější důkaz než jednotlivé komponentové testy.

Není to však běh nad úplně všemi bajty finálního HEAD: oprava prvního načtení editoru
Data Sources a drobná oprava sandboxového markup přišly později a mají oddělené
cílené ověření. Takový postup může být pro úzký dopad rozumný, ale nelze ho přepsat na
„finální HEAD prošel celý Stable“. Nové opravy společných životností a případně
control-plane vlastníka už vyžadují nový závěrečný checkpoint. (R01)

### Browser

| Pokus | Výsledek runneru | Význam podle zaznamenané exekuce |
|---|---|---|
| První celý výběr, 142 případů | 123 pass / 19 fail | 122 pass / 7 fail / 12 blocked / 1 not-run |
| Druhý celý výběr, 143 případů | 129 pass / 14 fail | 128 pass / 2 fail / 12 blocked / 1 not-run |
| Následná cílená oprava TestLabu | Samostatný úspěšný běh | Efektivně 129 pass / 1 fail / 12 blocked / 1 not-run; nikoli nový čistý celý běh |

Dvanáct chybějících konfigurací prostředí se nesmí označit ani za opravené vady
produktu, ani za zelené pokrytí. V jednom případě runner vykázal úspěch, přestože
scénář skončil na vypnutém gate. Širší log dále obsahuje tři odlišné chyby životností
společného shellu. Izolovaný průchod Collaboration nebo TestLabu je nevyvrací. (R01)

Sedm neúspěšných skupin kampaně jsou APP-01, APP-06, APP-10, APP-15, LIVE-01, LIVE-03
a GATE-02. Neznamená to sedm nezávislých produktových chyb: několik skupin narazilo na
tentýž společný problém. Zároveň nelze tři různé stack traces zredukovat na jeden
obecný „cancellation problém“ a potlačit jejich logování.

### Živé scénáře

Skutečné CRM plánovací čtení a HR schválení/zamítnutí jsou zaznamenané jako úspěšné.
Negativní souborový test také prošel. Pozitivní souborová cesta ale neprošla celá ani
ve třech pokusech: nestačí prokázat zápis do workspace, když nenásleduje úspěšné
připojení k projektu a oba read-back kroky s obsahem. Jedna zaznamenaná dílčí operace
soubor skutečně uložila; nesmí se tvrdit, že neproběhl žádný efekt. (R08)

Workflow nejprve narazilo na nepodporované temperature=0. Po změně fixture přišlo
HTTP 200, ale záznam provideru i Workflow zůstal Failed. Domněnka, že nestačil limit
150 výstupních tokenů, není doloženou kořenovou příčinou. Potřebujeme bezpečná data o
terminálním stavu a důvodu, nikoli bez rozmyslu zvýšit cap nebo přijmout neúplnou
odpověď jako úspěšnou. (R08, R28, R29)

Původní rozpočet 40/40 požadavků je vyčerpaný. Další bundle jej automaticky neobnovuje.
Nové placené pokusy vyžadují samostatné výslovné povolení. Bez něj se dokončí všechny
bezpečné neplacené opravy a testy; živé důkazy zůstanou označené jako chybějící, nikoli
jako selhání aplikace nebo jako splněné ověření.

## Čtyři produktové opravy pro closure

### WCL-R1: přesný edit profilu může obnovit souběžně smazaný záznam

Nový WorkspaceDataSourcesOwner nejprve ověří existenci uloženého profilu. Následně
ale volá původní Save control-plane vlastníka, který při svém uzamčeném čtení udělá
upsert i tehdy, když původní záznam už chybí. Identitu vezme z modelu.

Pořadí je tedy možné: načíst B → úspěšně ověřit B → jiný vlastník odstraní B → původní
Save pod dalším samostatným lockem B znovu vytvoří. UI busy stav nemůže zabránit práci
jiného circuitu, vlákna nebo procesu. Existující test odstraňuje profil před vstupním
ověřením; tento pozdější interval neprokazuje. (R09–R11)

Jde o závěr ze zdrojového toku, zde nespouštěný. Další běh musí začít reprodukcí se
dvěma vlastníky a řízenou bariérou. Nápravou je malá editorová hranice u skutečného
koordinovaného zápisu, ne další kontrola pouze v rendereru. Starší ne-UI upsert se
nemá plošně měnit. Je nutné zachovat hesla, aktuální/pending profil, metadata-only
smazání bez fyzického odstranění DB a význam aktivace až po restartu.

### WC-C1: Simple Chats používají prostředky po konci jejich scope

Runner uvolňuje SemaphoreSlim synchronně v Dispose, zatímco přijaté ExecuteAsync
ještě může dojít do Release. Contributor navíc může mít čekající inicializaci se
scoped aplikačními službami. Zpoždění uvolnění pouhého semaforu nestačí, pokud už
mezitím zanikl DbContext. (R05, R17–R19)

Oprava musí vyřešit skutečné vlastnictví operace a jejích závislostí. Nevyřeší ji
obecné zachycení ObjectDisposedException, singleton, zrušení serializace ani další
scope vytvořený podle právě aktuálního profilu. Stejně tak nesmí blokovat renderer
čekáním na operaci, která jej sama potřebuje k dokončení. Zvlášť se testují čekající
a přijaté operace, odstranění komponenty a zánik DI scope/circuitu.

### WC-C2: neúspěšné zavření Dialogu přeskočí další úklid

V BaseLib následuje po await CloseAsync uvolnění modulu a teprve potom .NET callback
reference. TaskCanceledException z Close může oba kroky přeskočit. DialogInterop
v dané cestě zachycuje pouze JSDisconnectedException. Tento tok jsem ověřila přímo
v testované revizi Components. (R06, R20, R21)

Oprava patří do Components a musí oddělit aktivní zavření od rušení zanikajícího
view, zajistit zbytek úklidu i po chybě a uchovat ochrany pozdního importu, opakovaného
Dispose a vnořených dialogů. Potlačení všech JS chyb v aplikaci by nebyla oprava.

### WC-C3: MainLayout po čekání pokračuje proti zaniklému browseru

První render i frontované události změny adresy mají více await kroků bez společné
životnosti layoutu. Mohou pokračovat do TrackTab/BrowserWorkspaceStateStore nebo
registrace listeneru po zániku původního circuitu. Již existující flag pro badge
Collaboration tuto obecnou cestu nechrání. (R07, R22–R24)

Potřebujeme kontrolovat původní layout, route a profil v relevantních bodech po
čekání, nikoli jen na začátku. Nepotvrzený browserový zápis není úspěšný Save; aktivní
chyba úložiště se nesmí globálně potlačit. Součástí testů zůstane normální navigace,
back/forward, taby, startup dialog a klíče browserového stavu po profilech.

U těchto tří společných chyb není prokázané, že je způsobila právě poslední extrakce.
Ani zprávy, ani tato revize nedokládají únik dat nebo obejití autorizace. To ale není
důvod nechat opakované chyby společného hostu otevřené před dalšími refaktoringy.

## Další důležitá úprava testovacího scénáře

Živý pomocník před schválením kontroluje běh, identitu approval a povolené názvy dvou
mutujících nástrojů. Je vhodné doplnit také explicitní kontrolu skutečných argumentů:
přesný projekt, uzel, relativní cesta, overwrite a očekávaný obsah či zdroj assetu.
Samotný název nástroje není dostatečná testovací hranice pro automatické potvrzení
modelového požadavku. Neočekávaná mutace se odmítne, nikoli schválí kvůli postupu
testu. To je zpevnění harnessu, nikoli nalezený důkaz neoprávněného produkčního zápisu.
(R25)

Dále jsou oddělené skutečné outbound rezervace, provider journal, HTTP úspěchy, tool
batches a výsledky vlastníků. Současný druhý watchdog počítá batches, ne požadavky.
Je potřeba zaznamenat přesný spouštěč zastavení a zachovat původní databázi do konce
sběru důkazů. Screenshoty a manifesty mají vlastní adresář každého pokusu, aby se
nezaměňovaly či nepřepisovaly. (R26, R27)

## Závislosti a doručení změn

Oprava Dialogu může být lokálně správná a přesto se k dalšímu buildu nedostat. CI nyní
vybírá matching Components branch podle cílové větve a teprve potom zaznamená její
SHA. Není zde obecný pevný Components pin, který by stačilo přepsat. Při kontrole
navíc v seznamu větví Components nebyla components-decoupling. (R30, META02)

Bundle proto povoluje úzkou opravu BaseLib v sourozeneckém repozitáři a požaduje
přesnou dvojici otestovaných commitů, source/asset graf a ověření zamýšlené cesty
konzumace. Nepovoluje automatický push, merge nebo vydání balíčku. Lokálně ověřená
dvojice a dosud nepublikovaný dependency commit se vykážou odděleně; nesmí se tvrdit,
že je CI již ověřené nad touto závislostí.

## Závěrečná kampaň

Uzavírací zadání zachovává všech 29 skupin původní kampaně a přidává šest skupin pro
konkrétní opravy, harness a doručení závislosti. Nejde o 35 předem hotových testů,
ale o 35 povinných důkazních oblastí.

Po cílených reprodukcích a opravách následuje nový zmrazený Stable checkpoint,
kompletní ne-live browser inventář a samostatná opakovaná společná navigační sekvence
s řízeně opožděnými čteními. U souborové cesty se ověřují původní projekt/uzel,
schválení, identity, skutečné bajty i hash a obnovený UI preview. U Workflow skutečný
run/verze a vytvořený asset; HTTP 200 ani text modelu nejsou dostačující důkaz.

Chybějící prostředí se prvně znovu ověří a bezpečně připraví, pokud je to dostupné.
Neřeší se vyřazením testů, novou karanténou, ignorováním serverových chyb ani výměnou
reálného driveru za fake se zachováním označení live. Drobné prokázané chyby se opraví
rovnou; nové rozsáhlé problémy autority, schémat a více vlastníků se důkladně zmapují
a ponechají jako explicitní blokery. Známé WC-C1/C2/C3 už jsou tímto bundle zadány k
opravě, ne pouze k opětovnému napsání dokumentace.

## Co revize provedla a neprovedla

Prošla jsem připojený repozitář, současné záznamy testování, relevantní implementace
a konkrétní testy. Závěrečná reference větve se nezměnila. Dotaz na Actions pro
kontrolované SHA vrátil nula běhů; to samo o sobě nevyvrací lokální testování.

V tomto prostředí není dotnet. Produkt jsem tedy nesestavovala, nespouštěla C# testy,
browser ani benchmark. Nové nálezy ze zdroje se musí reprodukovat v dalším běhu.
Ověření Python nástrojů, JSON, odkazů, manifestu a ZIPu se týká předávacího balíčku,
nikoli bezpečnosti nebo funkčnosti aplikace.

Cílový výsledek uzavíracího běhu je jednoznačný: opravené zdokumentované problémy,
ověřená sestava zdrojů, čerstvé požadované důkazy a pravdivé rozhodnutí, zda lze začít
další modul. Pokud zbývá povolení živých testů, externí fixture nebo publikace závislosti,
uvést přesný zbytek práce a oddělit ho od dokončené opravy kódu.
