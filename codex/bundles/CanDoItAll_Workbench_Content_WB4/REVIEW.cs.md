# Revize WB3 a návrh Workbench Content & Files WB4

Datum: 6. října 2026. Kontrola byla zdrojová přes připojený GitHub, nikoli nové spuštění
aplikace. Přesné zdroje a rozsah načtených úseků jsou v [SOURCES.md](SOURCES.md).

## Závěr

WB3 bych zachovala jako dokončené oddělení hlavního graph authoringu. V prověřené části
jsem nenašla nový blokující produktový problém, který by vyžadoval další samostatnou WB3
closure. To neznamená dokončení celého Workbenche ani potvrzení release-ready aplikace.
Zůstávají specializované obsahové, party/assignment, secret a runtime/launch plochy.
Následující WB4 je větší obsahový a souborový celek, s konkrétní opravou původního cíle
textového editoru na začátku. Tato cesta byla ve WB3 výslovně odložená. (S02, S10-S21.)

## Co jsem porovnávala

Hlavní větev je components-decoupling. HEAD d7384b12f435978165b4b73b40cb39f03c377892
má zprávu „commit old bundle“. Porovnání s rodičem potvrzuje pouze přidání 52 souborů
historického WB3 zadání. Produktové hodnocení vychází z rodiče
3957fe73e2a0736c042daa504a2767050423c36b a předcházejících implementačních změn.
Žádný z těchto commitů není pokynem přepnout nebo resetovat checkout. Historická zadání
se v novém běhu zachovávají; po přidání aktuálního zadání se mají zahrnout do vhodného
podepsaného checkpointu, ne nechat omylem mimo předání. (S01, S02.)

WB3 report uvádí novější lokální Components a120106bc3d4576a40c16aac29b1b9654fb31d93,
po předchozí opravě composeru 49decea8. Připojený vzdálený development ale při kontrole
stále ukazoval na 24d182c664d0b1f293098643e52caed7384a5d50 a načtení nového primary
ref skončilo 404. Nezpochybňuji existenci lokální opravy. Není však potvrzené, že ji získá
jiný checkout nebo CI. Codex ji nemá vytvářet znovu; má použít a ověřit existující lokální
zdroje a oddělit funkční ověření od vzdáleného doručení. Bez skutečně dostupné potřebné
verze nelze předstírat úspěšný build. (S02, S03, S25.)

## Co je implementované správně

StructureWorkspace skutečně vytváří CanvasWorkbench, toolbar, toolbox, composer wiring
alokované k původnímu otevření a strukturální dialogy. Planning a Insights jsou složené
přes explicitní sloty. Nejde o novou knihovnu, která by uvnitř pouze vykreslovala původní
backendově propojenou stránku. (S04.)

Hierarchická operace zachytí dialog, otevírající kontext a všechny účastnické admissions,
přijme jeden zápis a následně pracuje s původním výsledkem. Nečte po čekání jiný právě
otevřený dialog. Potvrzená změna se zachová před refreshem a ztracené potvrzení není
zaměněné za jisté odmítnutí. (S06, S07.)

Generic composer předává nativnímu writeru očekávaný projekt i původní uzel; drží identity
zápisů a neobnovuje starý draft prostým návratem ke stejnému veřejnému ID. Přečetla jsem
nativní regresní testy s řízeným zápisem, zavřením a nástupcem, novou životností, změnou
typu a bohatými metadaty. Testy jsem zde nespouštěla. Samotná kontrola očekávaného uzlu
není univerzální verzování všech hodnot; její konkrétní rozsah se nemá přehánět. (S05-S09.)

Změny publikace copy/reconnect a pořadí window state z reportu mají samostatné následné
důkazy. Zachovala bych je i opravu minimapy; nemá smysl tyto hotové části znovu navrhovat.
Stejně tak není potřeba vytvářet nové Planning nebo Insights UI. (S02.)

## Testovací důkazy a jejich hranice

Finální nativní image kampaň podle reportu prošla 19/19 protokolových scénářů a 14/14
consumer případů. Zahrnuje oba klienty, saved Workflow, známě neúplný výstup, reálné
Scheduler restarty, skutečné Agent tools/approvals, souborový canary a stažené bajty,
History, zdrojové modelové názvy a interní routování. Externí odpovědi jsou deterministické,
ale vlastníci, nástroje a uložené výsledky zůstaly skutečné. To je podstatně silnější
podklad než pouhé otevření sandboxu. (S02.)

Zmrazený Stable měl 16 669 objevených položek a po rozvinutí teorií 16 724 provedených
případů: 16 722 pass, 2 fail, 0 skip v 30 assemblies. Všech 3 289 Integration případů
je úspěšná podmnožina, nikoli další počet k přičtení. Jedno selhání je timing assertion
Workflow komponenty; původní izolovaná kontrola prošla už před úpravou testu na správně
čekaný event. Není tím prokázaná chyba nebo oprava produkčního Workflow. Druhé selhání
je původní syntetický secret control. Celý původní běh zůstává neúspěšný. (S02.)

Pozdější state/reconnect/minimap opravy nebyly součástí téhož Stable checkpointu. Mají
vlastní 19/19 a 26/26 výběry a finální image kampaň. Report tvrdí shodu primary a testovaných
izolovaných stromů; v této revizi jsem ji z privátních souborů znovu nesestavovala. Počty
výběrů se překrývají a nesmějí se sčítat jako unikátní pokrytí. (S02.)

Opakované watch změny Razor/C# byly pozorované, ale CSS removal zůstává kvalifikované.
Uvedené časy obsahují také zpoždění měření; nejsou to obecné compiler benchmarky ani
příslib univerzálního zrychlení. Nový bundle nepovoluje kvůli kosmeticky zelenému výsledku
neomezené přestavění SDK či build systému. (S02.)

## Úvodní nález WB4-T1: původní textový editor nemá zachyceného vlastníka zápisu

CreateTextAssetAsync předává koordinátoru ProjectId a metodu CreateTextAssetNodeAsync.
Tato metoda pak volá CreateObjectAsync bez capturedSurface a capturedNavigationRevision.
CreateObjectAsync si v takovém případě vezme aktuální surface a její admission. Samotný
ProjectId v context recordu neuzamyká delegáta na původní projekt; koordinátor ho používá
zejména pro diagnostiku. Sousední task větev už takové zachycení má. (S10-S12.)

Konkrétní kontrapříklad: otevřít textový editor nad původním projektem/rodičem, nechat
otevření nebo přípravu čekat, změnit či znovu vytvořit původní životnost a načíst aktuální
surface, potom doručit starý submit. Zápis nesmí získat novou admission pouze proto, že
má nový projekt stejné veřejné ID a rodiče. Jiný neexistující parent by mohl chybu náhodou
zakrýt, proto musí regresní test prokázat přímo vlastnictví původního cíle.

Jde o závěr ze zdrojového toku, nikoli mnou provedenou runtime reprodukci nebo důkaz
incidentu v uložených datech. Codex má nejprve test přes skutečný dialog a native writer.
Malá oprava má vést původní kontext až k zapisujícímu vlastníkovi a zachovat již přijaté
identity. Nesmí to řešit globálním CloseAll, automatickou novou autorizací nebo opakováním
již uloženého souboru. Detaily jsou v [S0_TEXT_TARGET.md](S0_TEXT_TARGET.md).

## WB4 jako větší dokončovací celek

| Rodina | Co se vyčlení |
|---|---|
| Textová aktiva | Celý Create new/Upload existing editor, pět podporovaných textových typů, validace a přesné výsledky. |
| Kolekce souborů | Reálné plovoucí FileBrowser okno, projekt/uzel, podprojekty, bounded search, preview, download a povolené lokální akce. |
| Přímý obsah | Celý FileInteraction overlay, autorizované View/Edit, revision conflicts, Save a dirty/saving/close guard. |
| Obrázky | Skutečné nastavení a stav původní placeholder–queue–provider–media cesty. |
| Souhrny a transkripty | Progress Summary, inline status, uložené XLSX/Mermaid/image exporty, scaffold a explicitní analýza transcriptu. |

Nejdůležitější rozdíl: kolekce aktivuje soubor read-only, ale přímý náhled může mít nativní
SaveTarget a být editovatelný. Zjednodušit vše na read-only by byla funkční regrese; povolit
vše editovat zase bezpečnostní chyba. Přijatá revision, přesný file handle a nativní save
jsou samostatné od UI otevření. (S16-S19.)

V odložené souborové komponentě jsou i běžná rizika asynchronních catch/finally a úklidu:
chyba staré operace se může publikovat bez správné kontroly a uvolnění sdíleného pole může
zasáhnout nástupce. Zadání vyžaduje konkrétní testy, ne přidávání vrstev bez důkazu. (S16.)

Obrázková fronta je skutečně bounded Channel(64) v paměti. Není to durable restart
protokol. Původní op/node/project musí přejít k workeru a až k zápisu; nelze to napravit
jen filtrem notifikace. Placeholder, zařazení do fronty, provider response a uložené
bajty mají odlišný význam. Výpadek nebo neznámé potvrzení nesmí potichu zopakovat model.
Nesmí vzniknout nový obecný job framework v rámci UI refaktoringu. (S14, S15.)

Exporty Progress Summary vytvářejí skutečná souborová aktiva v grafu. Transcript scaffold
není rozpoznávání řeči a Find my tasks ukládá analytický text, ne nově vytvořené úkoly.
Tyto rozdíly jsou v zadání výslovné. (S20, S21.)

## Co zůstává a jak postupovat dál

Po WB4 ještě nejsou uzavřené participant/meeting/directory assignments, secret formuláře,
runtime/terminal/web preview ani Workflow/Process linkage a launch/recovery. Mají zůstat
funkční v původních hostech. Jednotlivé navazující rodiny jsou přibližně 3–5/5 náročností;
Processes zůstává poslední velký modul. Není to časový odhad ani pevný počet projektů.
[Podrobná roadmapa](DEFERRED_AND_ROADMAP.md) rozlišuje legitimní hosty a skutečné zbytky.

WB4 má 36 skupin ověření, nikoli předepsaných 36 testů. Cílené buildy/discovery se dělají
během etap, finální native/browser kampaň nad skutečným párem a případný širší Stable podle
reálného dopadu. Velké obrazovky pouze 1920×1080/DPR1; bez mobilního tuningu. Podepsané
commity po větších etapách, včasné native PGP odemčení, bez push/merge a bez nových placených
inference. Sealed historie zůstává beze změny. (S23, S24.)

Moje kontrola zde nespouštěla .NET, PostgreSQL, Docker, browser ani watch a neměla původní
privátní TRX/logy/media. Pythonové ověření předávaného balíčku kontroluje strukturu,
integritu a odkazy, nikoli správnost nebo skutečné provedení aplikace.
