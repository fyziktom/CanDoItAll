# Pozorování změn, seznamy a canvas

## 1. Dvě nezávislé záruky

**Correctness:** owner odmítne zastaralý zápis i bez jediného doručeného eventu. **User experience:** relevantní hint ukáže, že data mezitím někdo změnil, a seznam může zobrazit aktuální stav. Event není autoritou, snapshotem ani potvrzením vlastní operace.

Pro hlavní Interactive Server UI preferovat interní odběr přes host adapter a stávající circuit transport. Nevytvářet další WebSocket/SSE spojení pro každý editor jen proto, že ProjectStructure už má externí SSE API. Budoucí remote klient může použít stejný kontrakt přes transport adapter; tento úkol nevytváří jiné UI.

## 2. Co má nést hint

Minimální datový envelope: stable scoped resource key, změnová kategorie (`Created`, `EditableContentChanged`, `LifecycleChanged`, `Deleted`, `CollectionInvalidated`, `AccessChanged`), owner witness pokud jej lze bezpečně přenést, stream epoch/sequence, trusted actor label nebo anonymní fallback, origin editor/operation correlation a omezený seznam **veřejných field/group identifikátorů**, nikoli hodnot.

Tituly a existence záznamu mohou být citlivé. Bezpečná výchozí payload je malé ID a kategorie po autorizaci; zobrazované názvy dotahovat pouze pro aktuálně povolený view. Žádné serialized entities, confidential CRM notes, secrets, celé prompty či provider konfigurace. „Co se změnilo“ v banneru má být například „Obsah a podporované modely“, přesný diff až v authorized read.

User ID, circuit ID a editor ID nejsou totéž. Dvě okna stejného uživatele se musí navzájem informovat. Dva editory v jednom circuitu také. Potlačit lze pouze přesně korelovaný vlastní accepted operation efekt; plošné `if actor == me return` je chyba. Vlastní save hint nikdy sám nevyřeší `Unknown` outcome.

## 3. Směrování a lifecycle

Navrhovaný registr: partition podle instance/dataset a owner kind; exact subscriptions podle entity key, katalogové podle container/collection. Indexy musí umožnit probudit jen zájemce, ne iterovat všechny moduly. Pro stávající filter seznamu je vhodný jeden owner/container invalidation topic, ne subscribe na všechny existující řádky.

Na circuitu může být jeden multiplexing pump, ale **draft a editor session zůstávají per opening**. Pump není singleton seznam všech uživatelských formulářů. Subscription je disposable handle. Při zavření, změně target/profile/principal nebo revokaci se odpojí. Caches, buffered summaries a registry mají explicitní limity a odstraňování prázdných partition.

Publish probíhá až po skutečném durable commit. Zámky/EF transaction nesmějí čekat na renderování uživatele. Observer error nesmí převést accepted write na failed write ani spustit další pokus. S10 ukazuje staged Prompt zápisy: jejich `SaveChanges` v enlisted contextu není sám důkaz vnějšího commit; hook musí respektovat vlastníka outer transaction.

S20 používá jednu globální queue a signal; vhodný existující precedent pro cursor/gap, nevhodný hot-path template pro celou aplikaci. Nepřebalovat ho do univerzálního interface se stejným globálním wakeupem. Náhrada starého feedu je samostatný pozdější krok s parity testy, nikoli podmínka pilotu.

## 4. Race-safe start a refresh

Bezpečný vzor: zaregistrovat subscription / zachytit high-water H0, načíst coherent snapshot, pak zpracovat hinty po H0. Je přijatelné konzervativní upozornění navíc; není přijatelné okno, v němž se změna mezi read a subscribe nenávratně ztratí. Pokud native read umí vrátit odpovídající cursor, lze použít tento atomický kontrakt.

Před explicitním refreshem zachytit H1. Po dokončení potvrdit nanejvýš bezpečně pokrytý H1, ne právě nejnovější cursor. Změna během read musí zůstat pending. Obsah tokenu potvrzuje owner, ne pořadí streamu. Out-of-order publish po různě dlouhých post-commit krocích nesmí vrátit starý stav.

Při překročení bufferu vrátit gap flag, ne tiše zahodit nejstarší přírůstky. Epoch restart, reconnect a gap způsobí jeden řízený resync: seznam znovu query, editor zachová draft a vyžádá aktuální obraz. Neprovádět replay write. Při návratu focus lze levně ověřit freshness aktivních ploch s coalescing/cooldown; žádné intervalové dotazování každého záznamu.

Single-process pilot má explicitně process-local hints. Pokud skutečná instalace používá více Web procesů či externí writery bez publisheru, dokumentuj omezení a navrhni adapter pro shared transactional change log/outbox nebo jiný skutečný transport. **Neprezentuj static singleton jako cross-instance collaboration.** Zavedení distribuovaného transportu není automatická podmínka pilotu; CAS a resync zůstávají povinné.

## 5. Tiché seznamy

Quiet refresh zachová viditelné řádky během načítání, filtry, typed search, page size, sort, selection, scroll anchor a fokus. Nemění URL ani neotevírá detail. Nový výsledek má generation fence; opožděná odpověď starého filtru nic nepřepíše. Spinner lze použít subtilně, ne celý blocking overlay nebo prázdnou tabulku.

Coalesce více invalidací během krátkého okna. Nejvýše jeden běžící query pro tentýž view; během něj se ukládá bounded „ještě invalidováno“ příznak a následuje nejvýše jeden novější read. Nezahazovat nový filtr kvůli starému background refreshi. Nepoužívat stejný debounce key pro psaní dotazu a cizí změnu, která by znovu posunovala uživatelovo hledání.

Create/update/delete mohou ovlivnit členství filtru i mimo současnou stránku. Subscribing pouze na zobrazená ID nestačí. Full query aktuální **bounded stránky** je bezpečnější výchozí řešení než naivní lokální patch filtrovaného listu. Lokální patch používat jen tam, kde je prokázaná membership/sort/count sémantika. Při smazání poslední položky upravit page index nejvýše na platný rozsah. Otevřený detail zůstává samostatným targetem i když karta zmizí z filtru.

Board drag, inline edit, bulk selection a rozpracovaný create jsou editace, ne read-only list. Během takové interakce neremountovat položku, na kterou uživatel míří. Potřebný refresh odložit/coalescovat, po skončení doplnit. U feedů používat stabilní `@key` podle identity, ne celý seznam podle jeho revision.

### Projection lag

Zjisti, zda konkrétní list čte canonical EF, search index nebo jinou projekci. Hint po canonical commit může předběhnout index. Úspěšný query se starým indexem není důkaz, že invalidace byla vypořádaná. Pokud query není čerstvý vůči očekávanému watermarku, ponechat pending stav; použít projection-ready hint, bounded následný refresh nebo owner-supported canonical fallback. Nezavádět nekonečný polling při rozbité projekci. Trvalá chyba má viditelný status a read-only Retry.

Editor hint publikovat nezávisle na úspěchu nepovinného indexování. Known commit s index warning musí zůstat accepted. List a editor mohou mít odlišný freshness mechanismus; nepřevádět index warning na možnost opakovat save. V pilotu ověřit skutečně zvolený Prompt search driver.

## 6. Formuláře

U otevřeného readonly detailu i čistého edit formuláře ve výchozím nastavení nabídnout „Načíst změny“. Uživatel požaduje explicitní pull u detailu; automatický reset formu nepovažovat za „vždy bezpečný“, i když IsModified právě false. Detail může mít focus, rozbalené sections či raw vstup bez blur. Čistý specifický readonly view lze aktualizovat potichu jen jako zdokumentovanou výjimku.

Dirty formulář po hintu drží původní B i L. Banner zůstává, dokud není stav bezpečně převeden; toast je pouze doprovod. Save handler odmítá známý stale/merge-pending stav bez ztráty obsahu. Ztracený hint je pokryt serverovým CAS.

## 7. Project Structure, Workflow a Process canvas

Zachovat model přehledu změn a explicitního pull. Změny seskupovat podle uzlů/příkazů, s viditelným počtem a možností znovu otevřít changes window. Bounded summaries lze zkrátit s označením, že je nutný celkový refresh; nikdy nevytvořit nekonečnou historii v paměti.

Refresh musí respektovat nezávislé inspector drafts, viewport, zoom, selection, drag a transient UI. Volitelně obnovit čistý graf až po uživatelově potvrzení; dirty inspector otevře jeho konkrétní typed form merge, nikoli dump celého graph JSON.

Workflow má whole-document definition draft a immutable accepted version. Process authoring podle S26 ukládá i jednotlivé canvas příkazy do společného durable agregátu. Nelze předpokládat, že oba canvas editory jsou pouze lokální nesaved dokument. Native příkazy stále potřebují původní observation a konflikt se řeší podle ownera. Automatický merge grafové topologie, runtime reexecution či replay drag příkazů není součást tohoto rollout zadání.
