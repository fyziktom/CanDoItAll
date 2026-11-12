# Protokol třícestného merge a zachování draftu

## 1. Tři hodnoty, které nesmějí splynout

**B — Base:** koherentní přijatý obraz, ze kterého uživatel vycházel, včetně owner expectation. Uchovat při načtení a po potvrzeném vlastním zápisu. Neodvozovat jej zpětně z „co jsem zrovna odeslal“.

**L — Local:** právě rozepsaný návrh. Včetně raw `-`, `1,`, whitespace nebo nevalidního data, které typed model zatím nereprezentuje, a včetně nových nesaved child IDs. Po dobu merge jeho revizi buď zamknout, nebo explicitně sledovat.

**R — Remote/current:** konzistentní aktuální obraz načtený autorizovaným ownerem po požadavku na porovnání. Neskládat jej z různých event payloadů, list karet ani nesouvisejících čtení.

Každý merge plan je svázán s ResourceAddress, opening, native R witness, draft revision a verzí schema adapteru. Výsledkem je immutable návrh patchů a unresolved položek. Renderer nikdy nedrží EF entity a nemá přístup ke službě Save.

## 2. Základní pravdivostní tabulka

Pro skalární pole s explicitním equality comparerem:

| Podmínka | Výsledek | Označení |
|---|---|---|
| `L == R` | tato společná hodnota | stejná změna / beze změny |
| `L == B` | R | změnil pouze někdo jiný |
| `R == B` | L | změnil pouze tento draft |
| jinak | nutná volba / vlastní výsledek | konflikt |

Pořadí podmínek je záměrné. `null`, nepřítomné pole a prázdný text nejsou automaticky totéž. Equality a normalizace jsou doménové: název může owner trimovat, obsah promptu nesmí obecný helper svévolně trimovat. Case-insensitive equality použít jen tam, kde ji používá skutečný owner. Číselná hodnota a její raw input jsou dvě vrstvy.

Samotný výpočet je čistá funkce, deterministická a bez vedlejších efektů. [Datové vektory](../examples/merge-vectors.json) zachycují základní případy a hraniční situace; nejsou produkční implementací.

## 3. Typy polí a zobrazování

| Typ | Smysluplný renderer | Merge pravidlo |
|---|---|---|
| Krátký text, číslo, datum, enum | čitelný popisek a Původně / Moje / Aktuální / Výsledek; čas s jasnou zónou, číslo se zachováním raw vstupu | scalar truth table; invalid raw není rovno poslední úspěšně parsed hodnotě |
| Víceřádkový plain text / prompt | řádkový diff, přehled změněných bloků, editovatelný výsledný text; původní lze rozbalit | omezený diff3; prokazatelně disjunktní hunks lze spojit, overlap vyžaduje rozhodnutí |
| Markdown | bezpečný text/diff, volitelný sanitizovaný preview | žádné vykonání HTML/scriptů; neporovnávat pouze výsledné HTML |
| Tagy a enum množiny | chips přidáno/odebráno, čitelná jména | membership třícestně po normalizovaném klíči; ne prostý union |
| Seznam modelů / kontaktů / adres | řádky se stabilní identitou, změněná pole a delete/edit konflikty | keyed collection; nikdy match podle array indexu |
| Uspořádané fáze / kroky | řádky s pořadím, explicitní přesun a obsah | zvlášť existence, obsah a pořadí; konfliktní reorder neřešit sortem podle ID |
| Reference na jiný záznam | label + typ + případně krátká ID pro rozlišení | porovnávat skutečné ID, neslučovat stejnojmenné recordy; label lookup bounded/batch |
| Konfigurace podle schema | skupiny skutečných editable fieldů, readonly nepokrytá data jako zachovaná část | explicitní allowlist mutable paths; neznámé části zachovat, nepřepsat rekonstruováním modelu |
| Secret/password/token | pouze metadata a „hodnota změněna / nastavena“ | nikdy odkrytí, secret diff či přenos hodnot přes event; změna/rotace zvláštní příkaz |
| Graph | doménové změny uzlů/hran, zvýraznění selection, seznam dopadů | v tomto zadání explicitní refresh/review; žádný obecný JSON auto-merge |
| Peněžní sazby, oprávnění, smluvní podmínky | zvýrazněná významová skupina a potvrzení | i netextové nekolidující změny mohou být doménový konflikt; owner revaliduje |

U dlouhého textu zobrazovat jen změněné bloky s kontextem a volbou plného textu na vyžádání. Fallback při překročení časového/paměťového rozpočtu musí být **ruční porovnání textu bez automatického spojení**, nikoli zamrznutí nebo zkrácený výsledek. Algoritmus nesmí vrátit „merged“ pro neprozkoumaný zbytek.

## 4. Kolekce — konkrétní konflikty

Použít persistované row ID, přirozený owner-defined compound key nebo vlastní nesaved row identity. Nesaved row ID není libovolně persistovatelný DB primární klíč. Při akceptaci mapovat pomocí existujícího přesného ack protokolu.

- B řádek existuje, L ho odstraní a R upraví: **delete/edit konflikt**; stejně opačně.
- B řádek chybí, obě strany přidají stejný klíč s různými údaji: **add/add konflikt**.
- Obě strany odstraní stejný řádek: odstraněno.
- Jedna změní jeho pole a druhá jiné: třícestné porovnání polí, pak doménová validace.
- Reorder přes více změn musí zachovat intended ordering; nelze použít náhodné pořadí dictionary.
- Kompletní replacement kolekce vyžaduje witness jejího původního členství. Tokeny jen na starých child rows neochrání nový řádek přidaný někým jiným, který by replacement odstranil.

Příklad Prompt Gallery: klíč podporovaného modelu je normalizovaná dvojice `(Provider, Model)`, ne string spojený delimitrem. `IsPreferred` má invariant nejvýše jedné preferred položky. Dvě oddělená přidání s `IsPreferred=true` tedy nelze označit za bezpečný automatický merge jen proto, že mají jiné klíče. Kontrola celé výsledné množiny musí vyvolat srozumitelný konflikt „Vyberte nejvýše jeden preferovaný model“.

## 5. Flow od hintu po Save

1. Přijatý relevantní hint pouze označí záznam jako změněný, uloží bounded summary a nabídne „Porovnat změny“. Nezapisuje do draftu ani nekonzumuje neúplné hodnoty z listu. Save se zablokuje tam, kde je jisté nové edit observation; při pouhé nejistotě se zobrazí nutnost ověřit stav.
2. Uživatel otevře merge. Zafixuje se B a L, zachytí se opening/draft revision. V pilotu je přijatelné po dobu review disabled underlying form, nikoli jeho destrukce. Nechat běžet ostatní nezávislé editory.
3. Owner načte coherent R. Nepodaří-li se čtení, zůstává celý L a B. Retry provádí pouze read.
4. Adapter spočte návrh. Nekolidující položky označí automaticky navržené, konfliktní čekají na volbu Moje/Aktuální/Vlastní. Žádná výchozí volba „všechno moje“ pro unresolved konflikty.
5. „Použít sloučení do formuláře“ provede kontrolu current opening, permissions, draft revision a všech řešení. Vypočtený výsledný návrh se aplikuje **atomicky z pohledu UI**: buď celý platný patch, nebo nic. Neprovádět průběžné mutace původního draftu během klikání na volby. Platnost merge plánu není totéž co validita celého formuláře: plán může zachovat existující nevalidní raw vstup a přijmout cizí změnu jiného pole. Takový draft zůstává nevalidní a Save blokovaný, dokud jej uživatel neopraví.
6. B se změní na přesné R, draft na M. Native expectation se převezme z R. Lze zachovat lokální záznam předchozího L pro Undo; nelze se tím vrátit ke starému oprávnění či zrušit fakt, že R bylo přijato jako základ. Form zůstává dirty podle M proti R.
7. Uživatel ještě běžně potvrdí Save. Owner opět zkontroluje očekávání z R atomicky. Změna R→R2 během porovnávání způsobí nový konflikt, nikdy force overwrite.

Když při review přijde další hint, označit review jako potenciálně zastaralé a nabídnout přepočítání. Pilot nesmí bez vysvětlení zahodit již provedené volby; lze je zkusit přenést jen pro trojice beze změny. Bezpečnost i tak zajišťuje finální CAS. U jednoduchého pilotu lze Apply při známé novější edit revision zakázat do načtení R2. Nesmí vzniknout deadlock při nepřetržitých nesouvisejících metadatech; rozlišovat semantic category a umožnit bezpečný explicitní read.

## 6. Co není řešitelný běžný field konflikt

**Delete:** záznam neexistuje; neprovádět upsert pod původním ID. Udržet lokální draft a vysvětlit smazání. Případná „vytvořit kopii“ je explicitní nový create po autorizaci, ne automatická obnova.

**Projekt nebo dataset vznikl znovu:** stejné ID s jiným lifetime/incarnation není pokračování původního záznamu. Nesmí se nabídnout obyčejné přenesení baseline.

**Ztráta přístupu:** nedotahovat a nezobrazovat R. Zakázat save, odhlásit subscription, zamezit dalšímu úniku metadat. Co lze zachovat v lokálním návrhu a exportovat řídí citlivost; „save draft to localStorage“ není univerzální bezpečný fallback.

**Archivace/publikace:** owner lifecycle guard má přednost. U Prompt Gallery nesmí uživatel nechtěně odarchivovat cizí změnu tím, že převezme nový timestamp a uloží obsah přes současný SaveDraft, který IsArchived resetuje. Ukázat stav a nabídnout jen explicitně dovolený lifecycle krok.

**Změna schématu:** neznámé a hidden fields zachovat, zablokovat nepodporovanou transformaci; nepřevádět neznámou property na null. V grafových dokumentech používat existující lossless whole-document model.

## 7. Vlastní commit není foreign merge

Při odeslání se zachytí **S** a field revision counters. Při výsledku ownera vznikne **A**. Aktuální L může být mezitím jiné. Hodnota z A se aplikuje do L pouze do polí, které zůstaly od S nedotčené podle revision counters a hodnotové kontroly. Přidělené row IDs se přiřadí podle potvrzené mapping; nesmí se obnovit mezitím odstraněný lokální řádek.

B po accepted výsledku je A, ne dodatečný read-current, který už může být R2 jiného uživatele. Pokud existing contract vrací jen ID+timestamp, rozšířit jej minimálně o přesně normalizovaný přijatý editable snapshot nebo jej sestavit v owneru z téhož committed stavu. **Nenačíst znovu nejnovější model a nazvat jej acknowledgement.**

**Pozdní ack nesmí vrátit již přijatou novější baseline zpět.** Pravidlo B := A platí při vypořádání stále odpovídajícího původního odeslání, nikoli jako bezpodmínečná assignment při každém návratu callbacku. Pokud jiná legitimní cesta již přijala koherentní R2 do aktuálního view/base, starší A se připojí jako historický přesný výsledek operace, ale nepřepíše R2, jeho očekávání ani následné lokální změny. Použít existující native observation/reconciliation a opening fences; neuspořádávat GUID tokeny. Pokud relativní význam nelze bezpečně určit, zachovat draft a vyžádat read/reconciliation, nikoli hádat podle času dokončení callbacku.

Prompt pilot dnes při save disable celé fieldset (S16). Tuto politiku lze zachovat; jednotkové jádro/adaptéry ale nesmí zavést regresi u Projects/CRM/Scheduleru, kde pozdější editace existují. Tento rozdíl je důvod, proč tři podobně vypadající hodnoty nestačí slít do jedné univerzální Merge metody.

## 8. Raw input, validace a cancel

EditContext.IsModified není jediný zdroj dirty: kolekce, custom inputs, rich text a změny bez blur musí oznámit revision; raw input může být dirty i při shodě typed hodnot. Připojit handler s jasnou lifecycle a odpojit jej při dispose.

Merge Cancel zavře pouze merge window. Parent form, draft, jeho validation messages a pending recovery zůstanou. Změny výsledného textu uvnitř merge window jsou vlastním review draftem; zavření s takovými změnami musí být jednoznačné, bez skrytého Apply.

Při Apply vyvolat potřebné field notifications bez toho, aby se všechny nesouvisející inputy resetovaly. Překontrolovat parsing i doménové constraints. Neschovávat chybu jen do toastu: trvalý inline status musí vysvětlit blokaci Save. Podrobnosti modal lifecycle jsou v [overlays](04_OVERLAYS.md).
