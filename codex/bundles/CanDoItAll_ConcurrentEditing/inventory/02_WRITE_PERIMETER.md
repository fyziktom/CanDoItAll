# Uzavření skutečného zápisového obvodu

## Proč nestačí upravit formulář

Právě načtená EF entity si pamatuje právě načtený token. Pokud se původní verze browserového formuláře neporovná s tímto stavem, SaveChanges ochrání až závod od tohoto nového čtení, nikoli změnu provedenou druhým uživatelem během předchozí editace. F01/F02/F16/F17 jsou konkrétní příklady tohoto rozdílu. S17, S27, S29, W01.

Owner proto vyžaduje immutable původní observation pro existující **replacement**. V DB musí být její kontrola a zápis atomické, ne `SELECT version` a potom nechráněný `UPDATE`. Při souběžných inserts je nutná unikátní business identity/absence podmínka; UI „nový záznam“ nesmí přepsat existující.

## Owner closure card — povinná pro každou přijatou rodinu

Pro každou položku inventáře vyplň v repo evidence záznam:

| Pole | Co doložit |
|---|---|
| Mutable aggregate | Všechna skutečně přepisovaná editable pole, děti, pořadí a membership; které runtime/derived části jsou oddělené |
| Read contract | Jedna coherent B/R, přesná scope/incarnation a token, absence semantics a unsupported schema |
| Write contract | Create vs replacement vs patch vs desired-state command; mandatory expectation pro replacement; validace inputu před awaits |
| Atomic boundary | Predicate/EF original token nebo existing coordinated owner lock/CAS; všechny child writes v témže logickém commit |
| Competing writers | UI, API, tool/agent, import/transfer, seed, background sync/health, drag/inline action, direct repository caller |
| Result | Rejected/no commit; exact accepted; accepted-secondary-warning; unknown. Žádná domněnka podle aktuálního read |
| Side effects | Search/project projection, activity, events, external effect; kdy je durable commit a co lze bezpečně opakovat |
| Event | Post-commit location, payload, scope auth, own-operation correlation, suppression, gap/backpressure |
| Migration | DB/file schema, data backfill, mixed-writer deployment, downgrade/restore constraints |
| Tests | Exact discovered test selection, new owner race proof a UI/Browser proof; measured query count |

„Controller už vrací 409“ není samostatný důkaz. Test musí ukázat, že storage obsahuje vítězný commit beze ztráty a odmítnutý request neprovedl část child writes.

## Co hledat přes CodeAnalytics MCP a lokální zdroje

Od každého Save/Update/Replace/Delete/Upsert entrypointu vyhledat references/callers a skutečné implementation. Zkontrolovat i extension methods, endpoint handlers, agent tool dispatch, source import, projection writeback, domain event handlers a fixture/maintenance writers. Vyhledat raw `ExecuteUpdate`, `ExecuteDelete`, SQL a zápisy přes jiný DbContext; token annotation zde nesmí být považována za automatickou ochranu.

Na UI začít router/ModuleAssemblies, pak skutečný renderer a jeho submit callback, footer save, inline edit, keyboard shortcut, drag/drop, shared picker, nested modal, background selection refresh a nav guard. Návrh může mít jedno místo Save, ale tři způsoby zavření a dvě kopie hosta.

Repo obsahuje starší owner mapu S38 a dokumenty uzavření jednotlivých feature rodin. Použij je pro discovery, ale ověř znovu skutečné current definitions. Zvlášť Resources a Workspace už mají nové kontrakty, které starší mapě neodpovídají. Script v balíku je pouze textový census helper, nenahrazuje call graph a dynamické registry.

## Přesná pravidla pro zápis

### Existing replacement

Missing/null/default expected token se odmítne. Povolená větev „neuvedený token => přepiš latest“ se nesmí zachovat pro veřejného současného calleru. Legacy adapters se převedou na typed error/precondition requirement; **nesmějí automaticky načíst aktuální token jen proto, aby prošly starým návrhem**. Breaking API behavior zdokumentovat, ale neobnovit nebezpečný bypass.

### Patch a narrow intent

Patch jen na vlastněná pole může mít užší původní observation, pokud invarianty dovolují nezávislost. To je výhodné pro Workbench task schedule/assignee/pricing a pro runtime health oddělené od user config. Ověřit set/query dependencies: „pole A jsem nepsal“ neznamená, že na A rozhodnutí nezáviselo.

Desired-state příkaz jako Favorite(true) nemusí být full-form replacement. Explicitně zdokumentovat jeho linearizovanou sémantiku a důvod, proč nemůže vrátit starý obsah dalších polí. Toggle vypočítaný ze starého browserového stavu nebo Delete na změněném záznamu může vyžadovat original witness. Mark-read se vztahuje k pozorované zprávě/sekvenci, ne k neomezenému přepsání novějšího unread stavu.

### Unknown a accepted-with-warning

Existing native receipts zachovat. U vlastníka bez durable receipts není nutné pro pilot stavět univerzální receipt infrastrukturu. Je ale nutné pravdivě zablokovat slepé repeat a nabídnout bezpečné read-only ověření / owner-specific recovery. Pozdější existence záznamu se stejným názvem ani current payload equality není důkaz, že právě původní operace commitovala.

External dispatch nebo vault/OAuth/plugin efekty mají vlastní transaction boundary; generic merge nemá permission je opakovat. Known commit nezmizí při selhání search indexu, activity observeru, logu nebo list reloadu. Ochranu sekundárních kroků neimplementovat tak, že se polknou skutečné validation/CAS chyby před commit.

## Kolekce a invarianty

Přidání/odebrání dítěte musí změnit odpovídající aggregate witness nebo být kryté membership predicate. Příklad: zastaralý CRM full Party model nesmí při ReplaceChildren odstranit nově přidaný kontakt jiného uživatele. Platí i tehdy, když každý původní kontakt má vlastní RowVersion.

Větší aggregate není automaticky bezpečnější UX: jediný token pro celé CRM by způsobil konflikty na nesouvisejících datech. Definuj nejmenší smysluplný editovaný invariant group a vyžaduj více observations, pokud command dotýká více takových skupin. AccountProfile+Party lifecycle/roles je konkrétní případ.

## Nasazení a data migration

Před změnou token kontraktu vyjmenovat všechny aktivní writers. Smíšené staré binárky, které revision neotáčí nebo neporovnávají, mohou ochranu obejít. Použít koordinovaný cutover, případně krátkou stopped-writer migraci podle existujícího owner runbooku. Data/backfill nesmí měnit business obsah, pořadí, archivaci, unknown konfiguraci ani vymazat legacy údaje.

Pilot Prompt Gallery již má timestamp token; nevkládat zbytečnou globální schema migration. Nové owner revisions v pozdějších vlnách jsou jejich vlastní řízené additive migrace. Resources migration má již velmi konkrétní guard/no-downgrade pravidla (S31); respektovat je, ne je nahradit univerzálním rollback příkazem.

JSON/control-plane/file owners zůstávají ve svém storage mechanismu a původním koordinovaném atomic write protokolu. Nepřestěhovat API uživatele, secret metadata či database activation do nové PostgreSQL tabulky jen kvůli sjednocení typů tokenů.
