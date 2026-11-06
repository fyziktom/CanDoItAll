# Vlastnictví záznamů, autorizace a Undo/Redo

## Rozhodnutí: připravit hranice teď, celý produktový systém nepředřazovat

Pro aktuální merge není nutné nejdříve vybudovat kompletní víceuživatelský ownership model. Nutné je neudělat architekturu, která jej později zablokuje nebo dnes unikne data. Stejně tak není nutný globální Undo/Redo framework; potřeba je odlišit lokální uživatelský záměr od durable mutation a udržet správné baselines.

Stávající main host registruje LocalOperatorUiAuthentication a samostatné API mechanismy (S41). To samo **neprokazuje**, že existuje hotový víceuživatelský Blazor login/membership systém. P0 musí ověřit skutečný principal/access-context chain; nesmí jen použít API user GUID jako vlastnictví všech UI záznamů.

## Co patří do pilotu

Trusted actor se získá na serveru ze současného access contextu. UI editor session nese vlastní korelační ID, nikoli oprávnění. Adresa dat respektuje instance/dataset/profile/project lifetime a nepřenáší se při profile switchi. ReadCurrent, write i subscription jsou autorizované stejným skutečným owner policy kontextem.

Zavést minimální explicitní policy boundary, pokud již existuje, znovupoužít ji. Local-only režim může mít konkrétní `LocalOperator` admission; není správné přidat do zabezpečeného režimu fallback „authentication missing => allow all“. Missing service/unknown principal musí odmítnout přístup nebo být explicitně lokálně nakonfigurován, nikoli potichu autorizovat.

Autorizace při subscribe není neomezená věčná lease. Revokace/access-generation změna zastaví subscription a další doručení citlivých metadat, invaliduje rozpracovaný merge a blokuje write. Před remote read a before commit se ověří aktuální policy. Optimalizovat pomocí scope-bound authorization lease/cache se skutečnou invalidací, ne TTL-only cache, která by po odebrání oprávnění dál posílala údaje. Absence jména actoru nesmí zabránit bezpečnému upozornění „Záznam byl změněn v jiném editoru“.

Do eventu nepatří claimy dodané browserem jako pravda. Ověřovat subscription request; nepovolit klientovi zvolit cizí profile topic. Podle citlivosti i pouhá existence ID je chráněná. Logs/metrics nesmějí ukládat obsah promptů, osobní poznámky ani credentials.

## Budoucí ownership model — připravené, ale neimplementované rozhodnutí

Možný následný návrh: project má owner principal + členství s explicitními roles (reader/editor/manager). Ověření efektivního přístupu probíhá na query i command cestě, v API/agent nástrojích, search indexu, background jobs, exportu a souborech. Uživatelské vlastnictví nesmí být jen filtr seznamu v Blazoru.

Před migrací je třeba rozhodnout: sdílený versus privátní původní projekt; organizační/service účty; převod vlastníka a osiřelé účty; dědění na child data; více projektových vazeb; globální prompt/provider/secret; odkazy mezi soukromým a sdíleným recordem; katalogové counts a full-text leakage; souborové access URLs; delegovaná agent authority. Výchozí migration nesmí ze všech dnešních dat bez souhlasu udělat veřejná data ani je nedostupně přivlastnit náhodnému lokálnímu účtu.

Access version není totéž co data revision. Permission změna může invalidovat view bez změny obsahu a content změna sama nedává nové oprávnění. Policy stamps mohou pomoci invalidaci, ale finální autoritu vždy ověřuje server. Převzetí záznamu jiným vlastníkem není obyčejná merge property „Moje/Aktuální“.

Tento projekt ownership lze připravit jako samostatné schválené pokračování. Není součást automatického rollout souhlasu pro concurrency.

## Undo/Redo: tři odlišné významy

### A. Lokální nesaved formulář

Záznam historie po uživatelských významových akcích (změna pole, přidání řádku, odstranění, compound merge), bounded podle počtu i paměti. Text coalescovat podle pole a intervalu; respektovat IME/composition, nativní undo textového controlu a raw invalid input. Není účelné ukládat celé dlouhé prompty ke každému stisku klávesy.

Teď stačí seam pro typed local change/compound action a schopnost zachovat pre-merge návrh. Pilot nemusí dostat globální toolbar Ctrl+Z pro všechny inputy. Merge renderer však nesmí vynutit snapshot-only architekturu bez možnosti lokální historie. Je možné nabídnout jedinou „Vrátit poslední sloučení do návrhu“, pokud se to vejde do pilotu a nerozšíří scope; není to náhrada povinného zachování B/L/R.

### B. Undo lokálního merge

Před merge: base B, draft L. Po review: base R, draft M. Undo merge může obnovit dřívější lokální hodnoty L jako **nový návrh nad stále platně zaznamenaným R**. Nesmí znovu přijmout starý očekávaný token B nebo prohlásit staré oprávnění za aktuální. Vypočte se znovu dirty rozdíl vůči R; případné známé R2 stále blokuje save. Uživatel vidí, že vrací vlastní návrh, nikoli cizí databázovou změnu.

Redo není platné automaticky po libovolném foreign refreshi nebo změně targetu. Jeho applicability se ověřuje vůči aktuální baseline/schema a lokální historii. Nelze potichu aplikovat inverse patch na jiný dataset.

### C. Undo již persisted canvas operace

ProjectStructure a některé Process canvas příkazy commitují okamžitě. Zde „Undo“ znamená nový **kompenzační owner command** s aktuálním původním očekáváním, oprávněním, novou operation identity a domain validation. Neznamená obnovit starý snapshot celé DB ani spustit SQL opačně.

Příklad: uživatel posune uzel, jiný ho mezitím přepojí/smaže. Inverse move se nesmí bez kontroly dotknout nového objektu ani oživit smazaný. Delete může mít nevratné externí efekty; běh procesu, odeslání emailu, secret rotation, plugin installation nebo vymazání historie nemá automaticky legitimní inverse. Takový runtime command se z UI history vyřadí nebo nabídne zvláštní kompenzaci s vysvětlením.

## Co výslovně nedělat

Žádný event sourcing celé aplikace, CRDT, centrální payload log všech osobních dat ani univerzální „replay last request backwards“. Per-editor history a owner-native conditional compensation lze přidat postupně. Ownership ani Undo nejsou důvod k odstranění existing receipts, serverových přístupových pravidel či nezávislého runtime stavu.
