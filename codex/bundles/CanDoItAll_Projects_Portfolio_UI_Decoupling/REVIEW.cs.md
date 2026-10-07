# Workspace R2 → Projects P1: revize a navazující zadání

## Závěr

Workspace je po stránce oddělení komponent/UI dokončený a původní kritické nálezy mají v aktuálním R2 záznamu uzavření. Přečetla jsem novou opravu odvozených počtů při mazání agenta, skutečný navigation acknowledgement probe, publikovanou Dialog interop implementaci a nové Projects hranice. V těchto prověřených cestách jsem neidentifikovala další kritickou chybu Workspace. Nejde o nový běh testů celé aplikace. [R01–R05]

Připravila jsem další modulový bundle pro Projects P1. Na začátku má omezený S0 průchod nad jediným zbývajícím neúspěšným nakonfigurovaným selectorovým testem. Příčina zatím není potvrzená, proto ji neoznačuji za kosmetickou ani automaticky za timing problém. Po uzavření nebo doloženém ohraničení tohoto případu může Codex pokračovat do P1; případný skutečný rozsáhlý problém oprávnění či dat zůstává blokující.

## Aktuální zdroje

Hlavní repo, `components-decoupling`: `fea6001813a10b9da212c60b77f8e5f77d59d7c2`.
Components, `development`: `4a858412d2c2a3f6123bf23d8c4584f05b47627d`.
Jeho strom `1e318a37f187c120e74d88357715ba22ae5cec31` odpovídá dříve lokální opravené verzi. Předešlý problém s nedostupným Components commitem tedy již není důvodem znovu zadávat stejnou opravu. Je však třeba ověřit, že konkrétní build a cílové CI opravdu použijí správnou větev/ref.

## Testování podle uložené zprávy Codexu

| Rozsah | Zaznamenaný výsledek |
|---|---|
| Nový celý Stable checkpoint | 15 879 úspěšných případů, bez selhání a přeskočení; 19 projektů |
| Celý ne-live browserový běh | 175 případů, 163 runner pass a 12 prerequisite failures |
| Význam po samostatných nakonfigurovaných následných pokusech | 167 pass, 1 fail, 6 blocked, 1 not-run |

Třetí řádek není druhý celý all-green browserový běh. Původní živý rozpočet je nadále vyčerpaný, 40/40; tento nový bundle neautorizuje další placené požadavky. Chybějící živé či generated-app prostředí je oddělené od neúspěšného běžného selectorového případu. [R01]

Neúspěch se týká skutečné cesty importu do prázdného klienta a druhého Simple Chat editoru pro sdílené Ollama: selector měl jednu možnost namísto tří, i když katalog obsahoval metadata. Pozdější úspěšná otevření nejsou potvrzením příčiny. S0 má zachytit původní generaci editoru, provider/model metadata a dokončení eventu, otestovat řízená pořadí a upravit pouze prokázanou chybu. Žádné přidávání náhodného čekání či tiché vybírání jiného modelu. [R06–R09]

## Projects P1

Nové zadání zahrnuje skutečné portfolio, strom a filtry, karty, přehled projektu, pětikrokový editor, hierarchický přehled, prezentaci výsledků mazání a package dialog. Nový UI projekt využije existující Projects.Contracts. Host zůstává vlastníkem služby, navigace, projektu/profilu a agentového kontextu. [R10–R15, R17, R22]

Soubory se v tomto kroku nepředělávají: současná skutečná Files plocha a dialog zůstanou zapojené přes hostem složenou aktivní část. Nejde o stub vydávaný za hotovou extrakci. Jejich autorizované čtení a soulad filtrů s kartami se ověří v produkci. Jejich vlastní izolace je další P2. Workbench/Project Structure, Gantt, Calendar a Processes nejsou součástí této extrakce.

Z přečteného kódu vyplývají konkrétní podmínky pro nový řez: generace načtení není ochrana proti dvěma zápisům; potvrzené ID projektu je nutné převzít před seedováním/refreshem; novější editace nesmějí zmizet při read-backu; starter plán nesmí přejít ze zrušeného nového projektu do jiného editoru. Projektový zápis a následné seedování jsou dva oddělené efekty, nikoli jedna transakce. Tyto cesty má Codex doložit regresními testy a napravit v rámci P1. [R10–R12, R15, R16]

## Velké obrazovky a rychlost práce

Nové browserové a vizuální ověření je primárně 1920×1080. Druhá velká velikost 1600×1000 je pouze volitelná tam, kde přidá funkční důkaz. Žádná nová série ladění mobilů, tabletů nebo small/medium breakpointů. Stávající nezávislé knihovní testy se neodstraňují ani neoslabují.

Pozornost patří korektním událostem, draftům, oprávněním, skutečným rendererům a izolovanému sandboxu. Naměří se reálný graf a dotnet watch cyklus; samotný přesun RCL není důkazem zrychlení celého Web hostu.

## Omezení revize

K dispozici byl připojený zdrojový kód a udržované implementační záznamy, nikoli původní kompletní soukromé TRX, logy a screenshoty. V prostředí revize není dotnet; neprováděla jsem produktový build, C# testy, browser ani nové benchmarky. Čísla testů jsou údaje zaznamenané Codexem. Každý další pokus musí zaznamenat své skutečné zdroje a výsledek. Balíčkové Python testy ověřují jen integritu předání.

Podrobná anglická pravidla a důkazy jsou v [hlavním promptu](prompt.md), [zdrojové revizi](PROJECTS_SOURCE_REVIEW.md), [S0](S0_SHARED_SELECTOR.md) a [registru zdrojů](SOURCES.md).
