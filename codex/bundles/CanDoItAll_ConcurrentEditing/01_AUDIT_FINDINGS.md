# Nálezy v současném stavu a doporučené předřazené zásahy

## Závěr o připravenosti

Codebase je pro tento krok výrazně lépe připravena než monolitický soubor formulářů: většina rodin už odděluje renderer, draft/session a skutečného vlastníka zápisu. Jsou přítomné generace asynchronních požadavků, snapshoty odeslaných hodnot, stabilní EditContext, rozlišení potvrzených a neznámých výsledků a u některých vlastníků velmi pokročilé receipts/recovery. **Nedoporučuji další plošný decoupling před pilotem.** Doporučuji několik malých společných mechanismů a postupné opravy původních podmínek konkrétních ownerů. Podklad: S06, S11, S16, S18, S21–S28.

Současně nelze od úspěchu Project Structure odvodit ochranu CRM nebo celé aplikace. Transakční serializace může zabránit souběžnému rozbití databáze, ale stále postupně přijmout dva formuláře vycházející ze stejné staré verze. To je přesně typ problému uživatele. [W01]

## Potvrzené nálezy

| ID | Nález a dopad | Důkaz | Zařazení |
|---|---|---|---|
| F01 | `SavePartyCoreAsync` načte aktuální Party a přepíše skaláry i kolekce bez kontroly původní verze editoru. Lost update je možný i při bezchybně fungující EF transakci. | S17, čtený save úsek | CRM první vlna po schválení |
| F02 | `SaveAccountProfileAsync` nemá původní content expectation. Zápis současně mění account profil, část Party a případně roli Customer. Samotný token jediného profilu by neuzavřel celý agregát. | S17 kolem 970–1050 | CRM: explicitně definovat editovaný agregát |
| F03 | Prompt draft a finalizace již používají očekávaný `UpdatedAtUtc`; mapa označuje timestamp jako concurrency token. Nevytvářet druhý paralelní CAS protokol jen proto, že je jiného typu než Projects GUID revision. | S07–S09 | Zachovat v pilotu |
| F04 | `LoadItemDetailsAsync` načítá root a navázané kolekce postupnými dotazy. Pro třícestný merge není takový obraz bez snapshotové konzistence dostatečný. Dosavadní session umí některé mixed read-back situace rozpoznat, ale to není generická konzistence libovolného čtení. | S08, S11, S16 | Předpoklad pilotu: coherent edit read |
| F05 | Prompt editor detekuje cizí obsah a nabízí explicitní reload se zahozením draftu. Chybí standardní B/L/R merge a proaktivní odběr změn v prohlédnuté stránce. | S11–S12, S16 | Pilot |
| F06 | `PromptGalleryPage.CloseEditorAsync` nemá guard neuložených změn; `editorDirty` značí přijatý commit pro refresh seznamu, nikoli dirty draft. Použít jej pro navigační ochranu by bylo chybně. | S12 | Pilot, včetně URL a nested picker |
| F07 | `PromptGalleryItemEditorSurface` při jiné source referenci vytváří nový form a EditContext. Merge nesmí být realizovaný bezmyšlenkovitou výměnou Source. | S14–S15 | Pilot: patch existujícího draftu |
| F08 | `ArchiveAsync` a `SetFavoriteAsync` jsou úzké identity/intention příkazy a zvedají root timestamp. Draft save navíc nastavuje Draft a ruší archivaci. Je nutné oddělit obsahový konflikt, metadata a lifecycle: cizí archivaci nesmí merge nenápadně převést na obnovení. | S07 | Pilot lifecycle testy a explicitní rozhodnutí |
| F09 | Sdílený `Notification` je obyčejný fixed div, inline z-index 900; `Dialog` je native dialog. Zvýšení čísla samo neopraví top layer a inertness. | S42–S43, W03–W04 | Pilot: oprava ve sibling Components |
| F10 | Notification renderer vlastní expiry timers. Naivní přidání dalšího rendereru do každého modálu zdvojí lifecycle a může měnit dobu zobrazení i accessibility oznámení. | S42 | Při návrhu outletu zachovat jeden logical lifecycle |
| F11 | ProjectStructure feed má bounded queue 512, jeden globální sequence/signal a filtruje při čtení. Každá změna budí všechny odběratele tohoto feedu. Současný problém výkonu nebyl změřen; rozkopírování tohoto vzoru napříč moduly je však škálovací riziko. | S20 | Nový feed cílený; stávající nemigrovat před schválením |
| F12 | ProjectStructure feed je procesový, není durable audit ani víceinstanční transport. Jeho cursor/epoch/gap a explicitní pull jsou vhodné vzory, ne důkaz cross-server komunikace. | S19–S20 a commit 015749f | Přiznat rozsah a připravit transport seam |
| F13 | Projects má již vyspělý draft, conflict read, previous-draft reconciliation a operation recovery. `Submitted` v conflict objektu je místní návrh, ne automaticky původní B. | S21–S22 | Doplnit chybějící baseline; nebourat recovery |
| F14 | Workflow native owner má sticky `Unknown`; persistent save kontroluje očekávaný head pouze pokud byl dodán. To jsou dvě různé věci: dobré UI outcome chování neuzavírá tokenless writer obvod. | S23–S24 | Workflow vlna, všichni volající |
| F15 | Agent `SaveAgentAsync` kontroluje timestamp jen je-li dodán. Přístupové změny mají vlastní smysl a mění konfiguraci; bez kontroly obvodu může starý full-form save jejich změny vrátit. | S32 | Agent vlna; žádný tokenless update |
| F16 | Scheduler Save načítá aktuální plan a zapisuje editor bez původního content očekávání. Rozlišení Persisted / ProjectionSynchronized už existuje a musí zůstat. | S27–S28 | Prioritní další vlna |
| F17 | TestLab Save chrání project binding/lifetime, ale prohlédnutý save přepisuje plan a synchronizuje Cases/Evidence/Runs bez původního content očekávání. List navíc načítá mnoho dat najednou. | S29 | CAS + oddělit editable plán od výsledkové historie; opatrný live refresh |
| F18 | Memory profile konfigurace načte nejnovější profil, aplikuje návrh a Upsert bez parametru původní verze. | S35 | Memory vlna |
| F19 | PluginGrantStore.SetAsync přepisuje grant načtený ze současnosti bez původního očekávání, přestože návratový model nese ConcurrencyToken. UI union oprávnění by byl bezpečnostní problém. | S36 | Plugin vlna; oprávnění vždy explicitní review |

## Kde starší audit už nelze převzít

**Resources:** aktuální `ResourceRegistryOwner` předává `ReplaceResourceMetadataCommand` / `DeleteResourceMetadataCommand`, aktuální dokumentace zavádí samostatnou revision a exact snapshot. Neoznačovat automaticky za nezabezpečený historický `SaveAsync`. Před rolloutem ověřit skutečnou DB migraci a celý service obvod, nikoli vynalézat další token. S30–S31.

**Workspace:** současný façade už deleguje na `WorkspaceSettingsWriter` a nabízí V1/V2 snapshot/replace API. Historické tvrzení „SaveSettingsAsync prostě přepisuje latest row“ už na prohlédnutý façade nesedí. Interní writer a oba protokoly jsou k ověření při vlně Workspace. S33 vs S38.

**Projects a Processes:** nové revision/receipt kontrakty existují; Oct16 návrh není seznam dnešních chyb. Procesy mají podle udržované dokumentace společný podmíněný agregát pro authoring, importy a canvas příkazy. Navázat na něj. S21–S26, S39.

**Prompt timestamp:** současný `NextUpdatedAt` normalizuje na mikrosekundy a přidává mikrosekundu při potřebě. Není důvod jej v tomto úkolu „opravovat“ na obyčejné `UtcNow` nebo přičtení jednoho ticku. Přidat regresi pro stejný čas, zachovat PostgreSQL přesnost. S07.

## Předřazené refaktoringy — jen co skutečně pomůže

### Nutné před dokončením pilotu

A. **Coherent read + přesná accepted baseline** pro Prompt Gallery. Krátká read transakce nebo ekvivalentní prokazatelně koherentní query; nikdy transakce po dobu otevřeného formuláře. Oddělit edit snapshot od historie verzí a dalších drahých read-only dat, pokud tím lze snížit počet dotazů bez rozbití kontraktu.

B. **Malé jádro merge a adapter**, nikoli nové univerzální editor sessions. Zpřístupnit dirty/raw-input/capture/apply/close-guard v existující prompt komponentě bez duplikace dat. UI zdroje a callbacky zachovat lehké.

C. **Topic/key-bound change observation** se životností konkrétního otevření; post-commit publisher. Zabránit globálnímu wakeupu a zadržování zavřených komponent. Stávající ProjectStructure klient může zatím zůstat na svém feedu.

D. **Top-layer-aware notification outlet** ve správném sibling komponentovém vlastníkovi. Oddělit message lifetime od změny aktivního render outletu, jestliže to zvolená implementace vyžaduje.

### Těsně před každou další vlnou

Přísné původní expectations, coherent editable snapshot, accepted result, ochrana aktualizací/delete/collection membership a migrace všech volajících daného vlastníka. Pokud owner sdílí člověkem editovanou konfiguraci s health/heartbeat výsledky, oddělit zápisové záměry, ne nutně všechny tabulky.

### Výslovně nedělat jako předpoklad

Nevytvářet centrální generic repository pro všechny moduly, jednotnou receipt tabulku pro každou operaci, Redis/Kafka, event sourcing, CRDT, plný formulářový DSL, novou doménovou hierarchii `IEditableEntity`, plošné kopírování přes JSON, velké generické inheritance editor base classes ani kompletní vlastnictví projektů. Novou strukturu souborů nepovažovat za hodnotu samu o sobě. Každý dodatečný zásah musí mít nález a konkrétní test, který bez něj nelze rozumně napsat.
