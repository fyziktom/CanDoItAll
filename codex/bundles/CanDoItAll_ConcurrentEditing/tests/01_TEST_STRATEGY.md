# Testovací strategie a mapování dopadu

## Pravidla výběru

Řiď se aktuálními AGENTS, copilot instructions a docs/testing.md (S01–S03). Standardní Debug/Release, serializované buildy sdílených outputů, žádné nové custom konfigurace převzaté ze starých reportů. Reálná owner concurrency vyžaduje PostgreSQL; EF InMemory, bUnit a samotná annotation test nejsou důkaz atomického CAS.

CodeAnalytics MCP použij pro change impact: změněný symbol → callers → owning production roots → test references. Výsledek doplň negativními invarianty, které reference graf nenajde (tokenless API, child insert, scope leak, notification native top layer). Nedávej modelu cíl „spustit co nejméně testů“; cílem je nejmenší obhajitelná sada pro konkrétní změnu.

Žádný full suite po každé drobné změně ani automaticky po každém dokumentačním checkpointu. Přesahy společné BaseLib/Dialog, public contracts, root build policy či shared fixtures mohou vyžadovat širší gate podle repo pravidel. Ten spustit na zmrazeném stavu jednou a při další drobné opravě cílenou regresi; významná změna společného kontraktu může znovu legitimně rozšířit dopad. Přesné neprovedené lanes vždy uvést.

## Existing test lanes — výchozí mapa, nikoli dnešní passing report

| Rodina | Doložené existující testy / vyhledávací kořen | Co zachovat |
|---|---|---|
| Prompt unit | `tests/Unit/CanDoItAll.Tests.Unit/Prompts/PromptGalleryEditorSessionTests.cs` přímo čteno; namespace `CanDoItAll.Tests.Unit.Prompts.`; SearchSession/Form/PersistedContent testy uvedené S16 | generation, missing target, conflict, accepted token, mixed own readback, normalize/compare |
| Prompt components | namespace `CanDoItAll.Tests.Components.Prompts.`; `PromptsUiBoundaryTests` přímo čteno; SearchSurface/SearchHost/ItemEditorSurface/Page/Picker/Composer/Sandbox families dle S16 | shared EditContext, validation, same-target, nested effect retirement, direct references |
| Prompt PostgreSQL | S16 jmenuje `PostgreSql_MixedReadBackAfterOwnReceipt_DoesNotAuthorizeOverwritingAnotherEditorsModelSet`; owning test project dohledat lokálně | real mixed-read/foreign commit protection, not fake whole read only |
| Prompt browser | `PromptGalleryBrowserTests` uvedené S16 | production page create/save/finalize, embedded picker, URL, narrow/desktop rail |
| CRM | `CrmHrDirectoryPageFreshnessTests`, `CrmHrSameTargetRerenderTests`, `CrmHrDraftReconcilerTests`, `CrmHrFooterSaveValidationTests`, `CrmHrAssignmentsMutationTests`, `CrmHrOpportunityConversionHostTests` | draft/ack and validation invariants; nové two-editor tests nesmí nahradit tyto race testy |
| CRM browser | `CrmHrAssignmentsJourneyTests`, `CrmHrDirectoryRelationshipsPrivacyJourneyTests`, `CrmHrOpportunityJourneyTests`, `CrmHrRouteChromeJourneyTests`, `CrmHrAgentProjectionJourneyTests` | actual host, privacy, dialogs, technical/business boundaries |
| Project Structure | `ProjectStructureStaleEditHttpTests` z S38 + current ChangeFeed/Inbox/Observer/StructureChangesWindow consumers dohledat | original-state API a pre-read cursor/gap, vlastní source rozlišení, no global changes leakage |
| Projects | `tests/Components/CanDoItAll.Projects.UI.Tests` + actual host/owner ProjectDefinition/Pending/Lifecycle classes přes impact | exact accepted mapping, retained operations, seeding/delete, revision counters |
| Workflow/Process | authoring UI light test families a exact owner integration podle S25/S26; `WorkflowDocumentOwner` references | unknown no retry, lossless graph, inherited revision, publication/receipts |
| Scheduler | `SchedulerPlannerPageTests`, `SchedulerFireAdmissionPersistenceTests`, `SchedulerWorkspaceOwnerPersistenceTests`, `SchedulerSourceAuthorityPersistenceTests`, `SchedulerUiOwnerReceiptTests`, `SchedulerBrowserTests` | persisted-vs-projection, source authority, fire snapshot a independent drafts |
| Resources | `ResourceMetadataMigrationTests` a actual owner revision tests podle S31, light UI/host via impact | migration/backfill/no old-writer bypass, accepted/deleted witness, scope |
| Ostatní | Native feature UI.Tests + main host/components + real owner Integration přes inventory IDs a CodeAnalytics | nevolit celé Unit/Integration jen podle názvu modulu; file owners mají file-coordination proof |

Názvy z dokumentace jsou vyhledávací kandidáti, dokud lokální discovery nepotvrdí aktuální namespace/project a počet. **Nikde nepřebírat historické počty 27/47/83/... jako dnešní očekávání.**

## Doporučený pracovní cyklus

Po změně pure scalar helperu spustit jeho úzké core tests. Po změně session přidat její tests + core. Po owner změně přidat relevantní PostgreSQL integration a dotčené adapters. Po shared modal změně BaseLib/overlay tests a konkrétní native browser scénáře, nikoli jen bUnit screenshot.

Příklad pro existující Prompt lane po obnově aktuálních build inputs (PowerShell nebo shell s odpovídajícími uvozovkami):

```sh
dotnet build src/Modules/CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj -c Debug --no-restore /m:1
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Debug --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts." /m:1
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts." --logger "trx;LogFileName=prompt-unit.trx" --results-directory artifacts/concurrent-editing/pilot /m:1
```

`--no-restore` pouze po skutečně aktuálním restore; `--no-build` pouze proti právě vybudovanému správnému target/config. Při změně filtru nebo přidání testu provést discovery znovu. Jestliže dnešní test platform používá podle repo policy jiný invocation, použít její skutečný příkaz a zaznamenat ho; neobcházet discovery přepínáním frameworků.

Report: planned FQN selection, discovered names/count, executed names/count, failures/skips, SHA, configuration a vlastník test DB. Nula objevených testů nikdy není zelená. Výjimky `Category!=...` nepsat na míru chybě; použít existující dokumentované lane policy.

## Nová povinná test matrix

Následující IDs jsou **navržené požadavky na testy**, nikoli tvrzení, že třídy existují nebo prošly. Lze sloučit několik IDs do jednoho dobře parametrizovaného testu, musí ale být dohledatelné jednotlivé chování.

| ID | Vrstva | Chování |
|---|---|---|
| T01 | Pure | Scalar truth table včetně null/empty/missing a equal concurrent edits; determinismus |
| T02 | Pure | Množiny: additions/removals, normalizované keys, ne prostý union |
| T03 | Pure | Keyed rows: add/add, edit/delete, foreign insertion, stable IDs versus pořadí |
| T04 | Pure | Cross-field constraints: dva preferred modely, nepřípustný range/relationship |
| T05 | Pure | Text: disjunktní hunks, overlap, CRLF/LF, unicode, repeated lines, trailing newline |
| T06 | Pure/perf | Large/adversarial text a bounded fallback bez truncation nebo falešného auto-merge |
| T07 | Component/session | Raw nevalidní číslo přežije hint, review, cancel a neúspěšný Save |
| T08 | Session | Original B není Submitted L; post-commit S/L/A a ABA edit revision counter |
| T09 | Session | Apply merge pouze stejného target/opening/draft revision; starý plan se odmítne |
| T10 | Session | Zrušení merge nezmění L/B ani native expectation; apply nevykoná save |
| T11 | PostgreSQL | Dva editory sekvenčně ze stejné B; druhý stale replacement odmítnut a DB zůstane beze ztráty |
| T12 | PostgreSQL | Překrývající se reálné write transakce; atomic predicate a žádné poloviční child writes |
| T13 | PostgreSQL | Coherent B/R během intervenovaného root/child update; whole-read fake není dost |
| T14 | Owner | Chybějící token pro update/delete dle kontraktu odmítnut; create validní; expected absence konkurence |
| T15 | Owner/session | Přesný accepted A není nahrazen R2 pozdějšího editora; own readback mixed kolekce; pozdní ack nesmí vrátit již přijatou R2 baseline zpět |
| T16 | Owner | Rollback/staged SaveChanges bez outer commit nepublikuje canonical hint; commit publikuje |
| T17 | Owner/session | Projection/activity/observer failure po known commit nezpůsobí redispatch; accepted-warning zachová ID |
| T18 | Session | Timeout/unknown zachová původní submission; current read/hint nesmí falešně prokázat přijetí |
| T19 | Owner | Every alternate writer: API/tool/import/curator/background adapter token a hint perimeter |
| T20 | Feed | Same user dvě okna a dva editory jednoho circuitu se navzájem informují; own exact operation dedupe |
| T21 | Feed | Join/read race, hint během refresh, pre-read highwater, duplicate/out-of-order hints |
| T22 | Feed | Bounded overflow/gap/restart/reconnect: jeden resync, žádné tiché accepted latest-cursor skip |
| T23 | Security/feed | Profile A→B→A, principal change/revoke a jiné lifetime; žádný cizí title/content/ID leak |
| T24 | Performance/feed | Unrelated 1000 subscriptions nemají owner reads/renders; dispose nezanechá registry entry |
| T25 | Session/list | Burst coalescing, single active query + trailing, query generation/typed search/page preserved |
| T26 | Component/list | Create/delete/membership move ovlivní správnou stránku, count/page clamp, selection stays independent |
| T27 | Integration/list | Search projection lag: invalidation není falsely completed starým query výsledkem |
| T28 | Component | Same-target rerender zachová form instance/EditContext/raw validation; reviewed patch notifications správné |
| T29 | Browser | X/Cancel/Escape/backdrop/route/back-forward/parent-close dirty guard bez ztráty |
| T30 | Browser | Skutečný native dialog + toast: paint, pointer hit-test, dismiss, keyboard focus, nested stack |
| T31 | Browser | Toast existuje před novým modálem; single expiry lifetime, no duplicate announcements |
| T32 | Browser | Floating merge nad modalem, Cancel zavře pouze child; Apply bez Save, focus se vrátí |
| T33 | Owner/session | Record deleted/archived/lifetime recreated during edit; no resurrection/implicit restore |
| T34 | Browser/owner | Třetí editor během review / po Apply před Save; nový conflict bez ztráty sloučeného návrhu |
| T35 | Integration | Existing native receipts, exact versions, publish/source authority a scoped file owners bez regressí |
| T36 | Boundary | Pure core no EF/Blazor/module deps; renderers no owner injection; transitive forbidden reference guard |
| T37 | Privacy | Escaped prompt/HTML/code, confidential CRM fields, secret rotation: žádné hodnoty v hint/log/export |
| T38 | History | Local undo seam/compound merge nevrací starý remote token; persisted command nedostane fake inverse |
| T39 | Performance | Idle query budget, open/close leak loop, list burst, write/open/input latency proti baseline |
| T40 | Runtime | 5032 skutečně běží z nových outputů/source sibling assets; žádná paralelní stará instance |
| T41 | Module owner | Multi-entity AccountProfile+Party, membership witness, background runtime/config separation |
| T42 | Canvas | Explicit pull zachová selection/viewport/dirty inspector; persisted Process gestures nesmí replay |
| T43 | Migration | Old writer cutover, token backfill, unknown data preserved, supported file platforms |
| T44 | Test tooling | Přidané/změněné filtrování má nenulové discovery = skutečně executed; no stale --no-build |

Pilot povinně T01–T34, T36–T40 a T44 v rozsahu Prompt/shared primitives. T35/T41–T43 se v pilotu provádějí pouze v přímo dotčeném scope; plné příslušné owner/canvas/migration proof patří do konkrétních vln. To není příkaz spustit všechny existující integration suites před pilotem.

## Reálná databáze, browser a fault injection

Použít repository mechanismus izolovaného `CANDOITALL_TESTS_POSTGRES_CONNECTION`, chránit přihlašovací údaje a neukazovat je v reports. Nikdy nepřesměrovat test fixtures na databázi 5032. Deterministicky řízené barriers/TaskCompletionSource místo sleep-heavy testů. Intervence pro coherent read musí proběhnout mezi skutečnými dotazy pomocí existujícího test seam/interceptoru, ne pouze před jedním fake GetItem callbackem.

Browser contexts musí být skutečně nezávislé, ne dvě komponenty stejného fake scope. Same-user případ je povinný už pro pilot; oddělené identity a revocation lze ověřit current policy fixtures, ale nesmí být reportované jako hotový produktový multiuser login, pokud ještě není implementovaný.

BUnit postupy respektují repository instructions: dispose asynchronous helper, správný renderer context pro interakce během background rerenderu. Test se nemá „opravit“ odstraněním race, kterou nová funkce legitimně způsobuje. UI-only mocks doplnit real routed-host a DB proofem pro kritické invariants.
