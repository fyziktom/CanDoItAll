# Revize A2 a další krok: Provider Profiles Core PP1

## Závěr

Technický editor agenta má nyní oddělených všech deset sekcí. V prověřených částech aktuální implementace jsem nenašla nový blokující problém, kvůli kterému by bylo nutné opakovat samostatný closure bundle. Doporučuji zachovat A2 a pokračovat providerovou administrací. Toto je závěr ze zdrojového kódu a uložené evidence, nikoli nové spuštění aplikace nebo potvrzení celé release.

Kontrolovaný produktový commit je `25ea60327ab572daee2728d6e86589f035942d94` na `components-decoupling`. Záznam A2 vznikal před závěrečným commitem; jeho text o tehdy necommitnutém stavu není důkaz chybějících nyní zveřejněných změn. Historické bundles se během další práce zachovávají a jejich úklid zůstává samostatný krok před mergem.

## Co je správně

Verify nyní pracuje s nativním potvrzením proof a jeho přesnou původní verzí. Nemění rozepsanou konfiguraci ani EditContext; jiný konkurenční zápis není potichu přijat jako vlastní nový baseline. Nativní testy obsahují úpravy před i během Verify, selhání read-backu, opakované čtení bez diagnostiky a dva editory. Template read-back zahrnuje i šablony. [R02–R04, R10]

Oprava Memory pouze předřazuje Skip pro explicitní režim bez direktivy před kontrolu neprázdného dotazu. Tím nezavádí automatické Memory volání při approval continuation a neodstraňuje validaci skutečně požadovaného dotazu. [R05]

A2 ponechává načítání Memory profilů a driverovou způsobilost v produkčním hostu a důvěryhodné externí cesty u původního registru. Samotné renderery nemají tyto backendové reference. Přesun neutrálních pickerů do RecordBrowsing navíc zmenšil závislosti jiného spotřebitele místo jejich dalšího rozšiřování. [R01, R06–R09]

## Testovací závěr

Report uvádí 16 023 vykonaných Stable případů: 16 022 prošlo, jeden selhal a žádný nebyl přeskočen. Selhání byla zastaralá očekávaná závislost StorageSelection; následný kompletní třicetipřípadový opravný výběr prošel. Není to zpětně nový celý zelený Stable. [R01]

Zaznamenané další výsledky zahrnují 248/248 editorových a souvisejících případů, 25/25 nezávislého editoru a 16/16 opravené aplikační kampaně. Původní selhání souborové cesty kvůli Memory continuation zůstává přiznané; změna planneru má vlastní cílené testy a návazné skutečné cesty. Výběry se překrývají a nelze je sčítat jako unikátní pokrytí. Podrobnosti jsou v [hodnocení evidence](TEST_EVIDENCE_REVIEW.md).

Nevykonala jsem zde C# testy, PostgreSQL, browser ani watch. Původní soukromé TRX, logy a screenshoty nebyly předané. Kontroly předávacího ZIPu nejsou testy aplikace.

## Další bundle

PP1 obsahuje celý providerový katalog a shell a čtyři lokální sekce Connection, Prices, Runtime a Thinking. Zahrnuje skutečné tabulky cen, Thinking dialog i společný formulář. Sharing, request History a Shared provider connections zůstanou funkční přes původní hosty; jejich bezpečnostně odlišná správa není potichu přidána do nové knihovny. [R12, R19–R21]

Doporučená knihovna je `CanDoItAll.AgentFramework.Providers.UI` a samostatný Providers.UiSandbox. Nesmějí se stát závislostí hotového Agent Editoru, Workspace, Projects ani backendových vlastníků. Další Contracts/Presentation projekt má smysl jen při skutečné hranici.

V původním providerovém kódu je nutné při přesunu ošetřit zejména nahrazování draftu při refreshi a opětovném výběru stejného cíle, oddělené textové buffery tagů/modelů, skutečné zachycení input před blur, raw čísla/JSON a životnost Thinking dialogu. Selhání načítání secret metadat se také nesmí vydávat za důkaz smazaného secretu. Nejsou to nově přisuzované chyby A2; zadání obsahuje konkrétní pořadí pro jejich charakterizaci. [R12–R20]

Health pracuje s uloženým providerem a u lokálního profilu může uložit diagnostická metadata. Load from provider pracuje s odeslaným draftem a připraví modely/ceny lokálně, bez Save profilu. Existující identity, concurrency a verified-retry mechanismus zůstávají zachované. [R15, R16, R22]

## Správný další směr

Po PP1 zůstává sdílená/providerová administrace a request history či provozní dialogy, capability-definition a týmové editory a omezený audit chat/usage/Voice. Následuje Workflow authoring, Workbench a až nakonec Processes. Projects, Workspace a technický editor se znovu neextrahují. [R11]

Nové UI kontroly jsou jen pro velkou obrazovku, primárně 1920×1080. Během práce mají běžet cílené testy; širší Stable pouze podle skutečného zásahu a současných pravidel. Náročnost PP1 je přibližně 4/5 integračně, ne časový příslib. Výsledkem má být skutečně nezávislý vývojový sandbox a měření dotnet watch, nikoli slib zrychlení celého Webu.

Na začátku S0 není přikázaná vymyšlená oprava A2. Codex ověří aktuální stav, případnou reprodukovanou drobnost opraví a pokračuje. Nové rozsáhlé problémy oprávnění, schématu nebo durable protokolu musí přesně zmapovat místo jejich schování do UI přesunu.
