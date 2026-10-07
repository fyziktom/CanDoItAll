# Revize Projects P1 a zadání Files P2

## Závěr

Projects P1 bych zachovala. Portfolio, strom, karty, hierarchie a pětikrokový editor jsou skutečně vyčleněné, ne pouze rozdělené do partial souborů. Dalším vhodným krokem je celý Files P2: dialog jednotlivého projektu a souborová záložka filtrovaného portfolia. Před něj patří dvě malé opravy: P1-R1 rozliší známé odmítnutí původní životnosti od neznámého výsledku zápisu a P1-R2 opraví znovuvyhození již zpracované chyby JS importu při zavření editoru.

Kontrolovaná větev `components-decoupling`: `c207b499c3165ba95e46ffaf518884097d3135dd`. Components development: `4a858412d2c2a3f6123bf23d8c4584f05b47627d`. SHA jsou původem revize, ne požadovaným checkoutem. Revize vychází z načtených zdrojů a implementačních zpráv; aplikaci jsem zde nesestavovala ani nespouštěla její C# či browserové testy. Původní soukromé TRX, logy, snímky a databáze nebyly dostupné.

## Co je dobře

Kontrakty projektového editoru a portfolia byly oddělené od EF a aplikační služby. Nová Projects.UI odkazuje na BaseLib a existující Projects.Contracts. Produkční host předává Files přes slot a nevtahuje jej do lehké knihovny. Testy kontrolují tranzitivní závislosti, veřejné typy i nerozřešené reference (R02, R03, R22, R23).

Ukládání vrací skutečné potvrzení projektu, jeho životnosti a přidělených ID podřízených řádků. Draft zachovává novější hodnoty, původní instance řádků a EditContext. Předchozí problém opuštěného starter plánu se řeší vlastnictvím draftu. Seedování přes nový port nese původní admission až do transakce Workbench; starší volání podle ID se nezměnila (R04, R07, R08, R10).

Workspace se kvůli P2 nemá znovu rozebírat. Zveřejněná oprava Components je dál dostupná v development. Stav S0 selectoru zůstává kvalifikovaný: přesná studená cesta i řízené testy prošly, ale historická příčina nebyla objasněná. Nejde o nález, že je nynější Workspace znovu nedokončený (R01, R02).

## P1-R1 — známé odmítnutí se vydává za neznámý zápis

Po potvrzeném uložení projektu se spustí seedování. Pokud mezitím jiný vlastník odstraní projekt a vytvoří novou životnost pod stejným veřejným ID, nativní seed správně odmítne původní admission ještě před zápisem. Existující test tento backendový výsledek kontroluje.

Stránka ale zachytí výjimku obecným catch a nastaví SeedOutcomeUnknown. U mazání je stejný problém s DeleteOutcomeUnknown. Ve slovníku mutationTargets navíc zůstane obsazený původní veřejný ProjectId. Zavření a nové explicitní načtení aktuální životnosti tento záznam neodstraní. Nový editor tak může být dál blokovaný operací, o které už víme, že její daná fáze byla odmítnutá (R03, R06-R09).

Nejde o prokázané poškození dat nebo obejití oprávnění; backend se zachová bezpečně. Jde o nesprávnou informaci a obnovu pracovního postupu. Úvodní fáze nového bundle má doplnit test výsledného UI a opětovného načtení, rozlišit známé odmítnutí, zachovat dříve potvrzený projekt a uvolnit jen vlastnictví konkrétní ukončené operace. Starý draft se nesmí automaticky přesměrovat na novou životnost. Skutečně neznámé výsledky se nadále nesmějí naslepo opakovat.

Tento závěr je odvozený ze zdrojového toku a existujícího testovacího kódu. Novou runtime reprodukci jsem zde nespouštěla.

## P1-R2 — doporučené zavření může zopakovat chybu JS importu

ProjectModalHost při Save uloží task importu validačního JavaScriptu. Obyčejnou JSException zachytí a doporučí zavřít a znovu otevřít editor. Při Dispose ale čeká znovu na tentýž již chybový task a zachytává pouze JSDisconnectedException. Již zpracovaná chyba importu tak může znovu uniknout během úklidu, přestože žádný modul nebyl získaný a není co uvolňovat (R05).

Zadání požaduje deterministický test import-fail → Close → nový editor, samostatné držení importu a jeho pozdní úspěch/chybu/zrušení, správné uvolnění získaného modulu a zachování skutečné validace. Nejde o pokyn přepsat BaseLib Dialog nebo plošně potlačit JS chyby. Tento nález jsem rovněž nereprodukovala spuštěním aplikace; vyplývá z přesného řetězce await/catch v přečtené komponentě.

## Co přesně zbývá v Projects

Aktuální složka Pages/Components obsahuje ProjectFilesDialog, ProjectFilesPortfolioPane a ProjectsAgentChatContextProvider. První dvě jsou skutečné renderovací plochy s konkrétními závislostmi na souborových vlastnících. Třetí je legitimní host kontextu agenta. Samotná routovaná ProjectsPage může také zůstat v modulu; počet zbývajících Razor souborů není kritériem úspěchu.

P2 proto vyčlení obě souborové plochy do samostatné Files UI větve a sandboxu. P1/UI/Contracts/sandbox tím nesmí získat FileTools implementační závislosti. Produkce nadále použije skutečné koordinátory, granty, obsahové relace, lokální akce a download.

## Proč nestačí soubory jen přesunout

Původní dialog při resetu čeká na uvolnění přes společná pole a pak je vynuluje; po čekání také čte aktuální parametry a callback. Opožděný reset tak musí být ohraničený vůči novějšímu otevření. Chybové větve obou ploch navíc nejsou stejně ohraničené jako úspěšné výsledky (R11, R12).

U portfolia je další vrstva: UpdateAsync mění existující browserovou session. Následná kontrola generace v UI sama nevrátí starší zásah do providerů, scope mapy nebo capability mapy. Nové testy mají kontrolovat skutečně přijatou sadu zdrojů, ne jen nadpis a fingerprint (R14, R16).

FileBrowser komponenta při svém odstranění uklidí vlastní odběry a rušení, ale předanou session neuvolní. Náhled má samostatný obsahový grant a má přežít odstranění browserové komponenty; při zavření svého vlastníka se naopak musí správně zneplatnit. Tuto odlišnost P2 zachová (R15, R21).

Tyto body jsou podmínky bezpečné extrakce původní Files rodiny, nikoli automaticky nové regrese připsané poslednímu P1 commitu.

## Testování a výkon

Codex uvádí 37 úspěšných Projects page/native případů, 11 lehkých form/boundary případů a šest kontextových/CRM případů. Jedenáct Files owner případů je podmnožinou stránkové suite, ne další přírůstek. Jsou doložené pozdější Agent/file a Workflow/TestLab průchody nad projektem vytvořeným skutečným novým UI. Předchozí timeouty a opravované podmínky čekání zůstávají samostatné (R01, R02).

Celý Stable checkpoint měl 15 906 provedených případů: 15 905 pass, jedno selhání, nula skip. Skener našel syntetické argumenty svých negativních testů v generovaném discovery seznamu. Po úpravě umístění privátního artefaktu a jeho bezpečné odvozeniny prošel celý vlastnící Unit checkpoint s 9 400 případy nad stejnými binárkami. Není to nový celý all-green Stable běh. Některé finální úpravy hostu a souborového zarovnání navíc mají pozdější cílené ověření, nikoli tentýž široký checkpoint (R01).

Vývojová izolace se povedla: podle zprávy má renderer pět projektů a sandbox šest, watch set sandboxu 300 cest proti 4 597 aktuálního Web hostu. Neznamená to univerzální zrychlení; první Razor vzorek sandboxu byl naopak pomalý. Nový bundle požaduje oddělené a srovnatelné měření P2 (R02).

Veškeré nové UI ověření bude na 1920×1080. Volitelná druhá velká plocha 1600×1000 jen pro konkrétní funkční problém. Nezadávám mobilní/tabletovou kampaň ani responzivní ladění. Viditelnost náhledu, focus, scroll, skutečné bajty downloadu a zneplatnění grantů na velké obrazovce zůstávají povinné.

## Předání

Balíček obsahuje [anglický prompt](prompt.md), [opravu výsledků](S0_KNOWN_REFUSAL.md), [opravu JS životnosti](S0_FORM_JS_LIFETIME.md), [architekturu](SCOPE_AND_ARCHITECTURE.md), [zdrojový rozbor Files](FILES_SOURCE_REVIEW.md), [matici testů](VALIDATION_MATRIX.md) a [produkční scénáře](APPLICATION_JOURNEYS.md). Shared v3 je kompletní a nezměněný. Balíček sám neobsahuje předvyplněné výsledky aplikace a jeho validátor neověřuje běh produktu.
