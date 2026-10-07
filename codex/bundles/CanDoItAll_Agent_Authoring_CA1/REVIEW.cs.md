# Revize PP3 a pokračování: capability authoring + týmy

## Závěr

PP3 je z hlediska oddělení renderování dokončené a jeho architekturu bych zachovala. V prověřených
nových cestách jsem neidentifikovala další blokující chybu. Nepřipravila jsem proto další samostatnou
History closure: nový balíček po omezeném úvodu dokončí celý capability authoring a týmové editory.
Není to potvrzení celé aplikace jako release-ready.

Kontrolovaný hlavní HEAD je b3aec979eae708edd0a53b42cef41b9d52fbb49b, components-decoupling.
Jeho poslední commit je dokumentační uzavření; revize zahrnuje i předchozí skutečnou implementaci.
GitHub potvrdil platný podpis tohoto HEAD. Tyto commity nejsou příkazem resetovat checkout.

## Co je na PP3 správně

Modulový host nyní dodává původní read službu, profil, autentizaci, scope a clock; celá opakovaně
použitelná History rodina je v UI. Vyhledávací draft není totéž jako použitý dotaz a konkrétní řádek,
metadata i obsah mají svůj původ. Zavření nebo změna kontextu ruší správná čtení a odstraňuje obsah.
Nativní oprava Workflow PrimaryEvidence zachovává konkrétní roli, verzi zdroje, vazbu i oprávnění;
není to plošné povolení všech odkazů na obsah. Viz zdroje S03–S07.

## Výsledky testů mají kvalifikaci

Codex uvádí 84/84 nezávislých History případů, opravený 39/39 produkční výběr a 50/50 nativních
kontrol. Devět různých aplikačních cest má finální úspěšný pokus, ale není to jediný společný 9/9 běh.
První protokolový běh měl jednu timing chybu Responses; následné podporované pokračování stejného
image prošlo 19/19. Protože první chyba obsahovala jen Boolean, příčina zůstala neurčená.

Celý zmrazený Stable provedl 16 211 případů: 16 197 prošlo, 14 selhalo, nula přeskočeno, 25 assemblies.
Dvanáct selhání souviselo s chybějící registrací v dosavadních testovacích hostech a má opravný běh.
Jedna chyba je chybějící OpenAPI dokumentace modelCatalog/isSourceManaged u WorkflowProviderOption.
Jedna je scan včetně historických ignorovaných syntetických artefaktů; úspěšný source-only follow-up
neprokazuje, že prošel celý původní checkout. Výsledky proto nesčítám ani nepřeznačuji na all-green.
Podrobnosti a odlišené původy jsou v S01–S02.

Zdrojově jsem ověřila i obě nová pole WorkflowProviderOption bez popisu, zatímco okolní kontrakt
popisy má (S22). Zadání je opraví v existující dokumentační cestě bez změny JSON nebo oslabení testu.

## Components: lokální oprava není totéž jako push

Report uvádí otestovaný Tooltip commit b495d4c4a28f0a6588ba10bfaa7be6e8409eae18. Připojený GitHub
jej ale při kontrole nedokázal načíst; development stále ukazovalo na 4a858412d2c2a3f6123bf23d8c4584f05b47627d.
Opravu nezadávám znovu. Codex má najít původní lokální commit, ověřit podpis a skutečné assembly/assety
nového běhu a odděleně uvést vzdálené doručení. Lokálně ověřená dvojice může podpořit pokračování vývoje,
ale není důkazem, že ji již dostane jiné CI. Automatický push součástí není.

## Větší další část

C1 zahrnuje oba skutečné editory: celý tříkrokový průvodce vytvořením MCP/Skill/Tool a detail
Identity/Configuration/Raw. Součástí jsou všechny existující typované konfigurace, SKILL.md upload,
setup diagnostika a skutečné otevření z katalogu i A2. C2 dokončí metadata týmu, ikonový dialog,
výběr členů a produkční rodičovskou cestu. C3 ověří společné nativní funkce, sandboxy a vývojový cyklus.
Nejde o spojení datových vlastníků nebo nový společný administrační framework.

Capability authoring je jiná věc než hotový capability list a přiřazování v A2. Team v tomto řezu
je technická skupina agentů, ne HR organizace. Workflow canvas, Workbench a Processes se nezahajují.

## Záludnosti původního kódu, které zadání výslovně pokrývá

Setup test není pouze validace: může spustit proces, server nebo HTTP volání. Výsledek musí zůstat
svázaný s původní konfigurací, ne se tvářit jako platný proof nového nastavení. Přijatý Save, test setup,
assignment a Verify jsou různé operace. Pozdní výsledek nesmí zničit novější text ani spustit opakovaný efekt.

SaveCapabilityAsync již má ExpectedFingerprint a odmítá update neexistujícího ID. Nelze proto
mechanicky převzít providerový trik s předem dosazeným candidate ID pro Create. Případná chybějící
jistota commitu se má řešit malým konkrétním kontraktem skutečného vlastníka, ne odhadem podle jména.

Zvlášť důležitý je současný rodič: přiřazení capability existujícímu agentovi může uložit celý jeho
rozepsaný draft, kdežto u nového agenta jde o staging. Toto chování skutečně testují dosavadní nativní
kompoziční testy. Zadání ho nezmění na univerzální Apply/Save politiku a zachová zotavení po Create,
který uspěl, ale následné načtení katalogu selhalo (S19).

Metadata týmu dnes předávají živý model a zahrnují také členství. Nová hranice musí zachytit požadavek
před čekáním a zabránit tomu, aby úprava názvu potichu vrátila souběžně změněné členství. Tohle jsou
rizika starých právě vybíraných ploch, nikoli chyby přisuzované PP3. Zadání požaduje nejprve jejich
charakterizaci/regresní testy a zachování významu starších ne-UI API.

## Jak se ověří obecné funkce

Vedle rendererů a produkčních vlastníků jsou zadané skutečné, neškodné MCP/HTTP/process setup cesty,
vytvoření capability přes UI, přiřazení a Verify, agentový povolený i zamítnutý tool call a Project
Structure soubor s přesným nativním i staženým obsahem. History ověří stejný request a zvláštní
oprávnění k obsahu. Doplní se vhodné existující Workflow/TestLab a sdílený provider přes vlastní
kontejnery. Externí model může být deterministický, oprávnění a vlastníci nikoli falešní.

Large-screen only zůstává: 1920 × 1080, žádná nová mobilní kampaň. Delší běh znamená dokončení obou
rodin a integrací, ne opakovat celý Stable po každé záložce. Širší checkpoint se odvodí od skutečných
změn Core/katalogu a aktuálních pravidel. Podpisové commity mají vznikat po ucelených částech.

## Omezení této revize

Prošla jsem skutečný kód na uvedeném HEAD a uložené zprávy. Produkt jsem zde nesestavovala ani
nespouštěla .NET, Docker či browser testy; privátní TRX/logy/media nebyly přístupné. Nové source-derived
reprodukce nejsou vykazované jako provedené. Kontroly ZIPu, odkazů a Python nástrojů potvrzují pouze
předávací balíček. Shared v3 je zachováno beze změn; produktové skupiny začínají jako NOT_RUN.
