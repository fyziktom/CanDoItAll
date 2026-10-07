# Revize WB5 a dokončení Workbenche WB6 před prezentací

## Závěr

WB5 bych zachovala jako dokončené oddělení participant/meeting formulářů, chráněných referencí
 a runtime oken. V prověřených kritických cestách jsem nenašla další blokující chybu samotného
WB5. Celý Workbench ale ještě hotový není: zbývá skutečná rodina Workflow/Process integrace,
vnořené staffing/picker dialogy a poslední prezentace potvrzení smazání obsahu.

WB6 proto není další malé pokračování. Zadává dokončení všech aktivních Workbench rendererů,
nativní ověření, praktickou zkoušku se skutečným modelem a stabilizované sestavení pro přípravu
ve středu 7. října 2026 a zákaznickou prezentaci ve čtvrtek 8. října 2026. Další moduly a samostatný
Processes produkt se před prezentací neextrahují.

Kontrolovaný hlavní commit: `12322a72cd11a0de3ae98ad4dd1e0061964c9af2` na `components-decoupling`.
Components development: `a3fd4d22f2c4e0432cf389f194c6468b44ab7371`. GitHub potvrzuje platný podpis
hlavního commitu. Požadované Components už jsou vzdáleně dostupné; jejich dřívější opravy se
nemají dělat znovu. Tyto identity označují původ revize, nikoli požadovaný checkout. [S01, S02]

## Co je implementované správně

Party editor zachytí původní požadavek, projekt a uzel a převzetí PartyId odděluje od následného
čtení voleb. Conditional assignment předává původní nativní occurrence a zachovává konkrétní
assignment identities. Následný metadata writer a read-back jsou samostatné fáze.

Secret session používá původního vault vlastníka, nečte uloženou tajnou hodnotu, odděluje
přijatý secret od projektové reference a umožňuje explicitní dokončení bez druhého vytvoření.
Oprava syntetického rootu kontroluje přesný aktivní kořen a životnost projektu. Neruší kontroly
běžných uzlů.

Runtime start pracuje s konkrétním přezkoumaným plánem a oprávněním. Stop a readiness mají
původní WorkspaceOwnedProcessIdentity; nová session pod stejným uzlem nebo PID není automaticky
stejným cílem. Zavření preview zůstává odlišné od zastavení procesu. Nová renderovací knihovna
neodkazuje přímo na Workbench implementaci; EmbeddedBrowser je neutrální zachovaný potomek.
Tento směr není potřeba přestavovat. [S04–S07]

## Testy WB5: kvalifikovaný pozitivní výsledek

Implementátor uvádí 16 906 provedených Stable případů: 16 903 pass, 3 fail a nula skip, napříč
32 assemblies. Dvě chybná očekávání testů mají vlastní 3/3 a 4/4 opravné výběry. Zbývající selhání
scanu se týká čtyř zachovaných historických syntetických kontrol. Původní agregát má dál exit 1;
nejde o čistý release checkpoint. Pozdější oprava secret rootu má vlastní 53případové nativní
ověření a upravené finální image, ne zpětně změněný původní Stable.

Report dále popisuje třináct browserových consumer případů a skutečný Windows proces, jeho
potomky a zastavení při zachování sousedního canary. Protokol 19/19 patří dřívějšímu image a je
převzatý jen pro nezměněné providerové vstupy. Některé neúspěšné browserové pokusy mají následné
čtení a pokračování nad původně přijatými identitami. Nesmějí se sečíst do tvrzení o jednom
nepřerušeném zeleném běhu. [S03]

Tyto výsledky jsem znovu nespouštěla. Nemám původní privátní TRX, logy, screenshoty ani nativní
databáze. Provedla jsem kontrolu načteného kódu a testovací zprávy, nikoli vlastní certifikaci
spuštěné aplikace. Přesný rozsah přečtených souborů je v sources.json.

## Co opravit před dokončením

### WB6-W1: dokončení starého Workflow dialogu zasahuje nástupce

Ve stávajícím ProjectStructurePage.WorkflowNodes.cs není původní Add opening důsledně svázané
s návratem options ani s dokončením Create/Attach. Úspěšná cesta po čekání vyprázdní společné
pole dialogu, chybová obnoví původní dialog. Po otevření jiného dialogu B tak může dokončení A
B zavřít nebo přepsat. Start cesta kontroluje veřejné ProjectId, ale to samo neřeší nové otevření
na stejném projektu nebo A → B → A. [S09]

Jde o zdrojově odvozený problém starší odložené plochy, nikoli zde spuštěnou reprodukci nebo
prokázanou chybu databázového zápisu. Codex musí nejprve vytvořit konkrétní regresní test,
zachovat původní opening/immutable input a přijmout nativní identitu před dalším refreshem.
Původní nativní Workflow admission již má vlastní IntentId a obnovu přijatého běhu; nemá vzniknout
druhý runner nebo obecný nový framework. [S19]

### WB6-V1: nadpis náhledu překrývá canvas toolbar

WB5 tuto vizuální závadu výslovně zaznamenává. Oprava má být malá, na skutečném produkčním
preview, 1920 × 1080/DPR1. Musí zůstat viditelný nadpis, akce, vlastní scroll a nezávislost oken.
Žádná mobilní kampaň a žádné odstranění testu či nadpisu místo opravy. [S03]

## Celý zbývající rozsah

WB6 zahrnuje Workflow link/add/input/preview/start/status; Process link, confirm/estimate,
variables, celý staffing a HR/manual matching, candidate picker, agent details a switch
confirmation; reviewed preparation, Save-close/restore, start a původní recovery/delivery.
Doplňuje poslední renderer potvrzení smazání se zachováním volby ponechat nebo odstranit
vlastněná média. Na závěr musí projít celé aktuální soupisy volajících míst, registrace,
dynamické dialogy, styly a assety. Nezůstanou aktivní neklasifikované renderery. [S08–S16]

Native host může zůstat Razor i rozsáhlý code-behind, pokud skutečně vlastní route, oprávnění,
příkazy a životnost. Dokončení se neměří nulovým počtem Razor souborů v modulu. Původní
WB1–WB5, Agents, Workflow authoring, Projects a Workspace se nepřesouvají znovu.

Preferovaný nový celek je CanDoItAll.Workbench.Execution.UI a vlastní UiSandbox. Stavové
záznamy nesmějí do knihovny přinést ProcessLaunchAuthority, prepared requests, native stores
či databázové služby. Celé smíšené OverlayStates není vhodný Contracts soubor.

## Praktická zkouška pro zákazníka

Zadání odděluje deterministické providerové fixture od reálného modelu, který sám volí nástroje
a provádí analýzu. Obě vrstvy mají skutečné nativní vlastníky, schvalování a uložené výsledky.
Skriptovaná odpověď je vhodná pro přesné chyby a souběhy, ale neprokazuje schopnost modelu
samostatně vykonat zákazníkův úkol.

Povinná reálná cesta zahrnuje textový canary neprozrazený v promptu; vytvoření a připojení
souboru; bezpečné SVG a nové analytické čtení; skutečně vygenerovaný raster a jeho vizuální
analýzu podle bajtů; reálný XLSX se vzorci a samostatné čtení buněk včetně nové revize; uložený
Workflow a malý Process, který nejen připraví plán, ale skutečně proběhne a vytvoří výstup.
Použijí se současné Project Structure a spreadsheet nástroje, ne náhradní interní zapisování
dat z testu. [S20–S22]

Pro SVG se ověřuje XML, lokální reference a bezpečný obsah. Pro obraz skutečné dekódování,
rozměry a hash. Pro Excel OpenXML package, listy, datové typy, vzorce a reprezentativní buňky;
uložená formula cache není automaticky přepočítaný výsledek. Očekávaný výsledek se získá
nezávisle z dat, nikoli tvrzením autora nebo z názvu souboru. Analytik má novou session bez
předchozí generační konverzace.

Cloudové testy musí mít existující odpovídající finanční povolení, nebo jednorázové potvrzení
na začátku Codex běhu. Navržený strop pro toto potvrzení je 10 USD, nejvýše 64 jazykových/vision
pokusů a čtyři image pokusy včetně opakování. Jde o strop, ne odhad ceny ani nově udělené
povolení. Starých 40/40 se neobnovuje. Bez potřebného povolení má proběhnout vhodný lokální
model nebo má být konkrétní cloudová zkouška označená za blokovanou, nikoli nahrazená falešným
úspěchem.

## Výstup, který půjde zítra spustit

Codex má nejprve zachovat fungující baseline, potom vydat konkrétní podepsanou candidate verzi
s otestovanými start/stop/check skripty, privátní konfigurací, demo databází a storage, seznamem
skutečně vytvořených ID a českým runbookem. Obnovu má prokázat na odděleném scratch cíli.
Dva restarty se stejnými daty ověří zachování souborů, uložených sessions a přijatých běhů bez
duplikací Scheduleru.

Po finální zkoušce se produkční verze zmrazí. Dodatečná opravná změna vyžaduje rebuild a nové
ověření dotčených scénářů i startup smoke. Další refaktoringy počkají po prezentaci.

Odděleně se vykáže dokončení Workbench UI, spustitelnost kandidáta, deterministická integrace,
reálná modelová zkouška, původní široký testovací výsledek a demo readiness. Jedno nenahrazuje
ostatní. Komplexní nový problém oprávnění nebo dat zůstane přiznaným blokátorem; pod tlakem
prezentace se nesmějí vypínat ochrany ani přepsat testy tak, aby jej ukryly.

## Balíček a kontrola

Technické zadání a komentáře jsou anglicky, tato lidská revize česky. Shared v3 je zachované
beze změny. Pomocné validátory ověřují strukturu podkladů a exportovaná demo aktiva pouze pro
čtení; neprovádějí testování aplikace a neověřují původ providerových výsledků. Produktové
skupiny začínají NOT_RUN. Konkrétní výsledky kontroly balíčku jsou v PACKAGE_VALIDATION.md.
