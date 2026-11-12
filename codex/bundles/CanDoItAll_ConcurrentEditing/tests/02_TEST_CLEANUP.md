# Úklid testů — co skutečně odstranit a co naopak zachovat

## Výsledek tohoto auditu

**Nebyl potvrzen žádný konkrétní současný test, který by bylo bezpečné bez náhrady smazat jako nesmyslný.** Přečtené existující regresní testy a dokumentované coverage mají relevantní invarianty. Tento závěr je důležitější než uměle vyrobený seznam „legacy“ mazání. Část dříve mrtvých CRM komponent už byla odstraněna při minulém refactoringu (S18); znovu je nezadávat jako práci.

Byly identifikované konkrétní skupiny testů pro **vědomou úpravu kontraktu**, nikoli automatické odstranění:

| Kandidát | Rozhodnutí |
|---|---|
| `PromptsUiBoundaryTests.Rendering_library_references_only_contracts_and_ui_primitives` | Zachovat. Je-li renderer složen hostem, nemusí se měnit vůbec. Při skutečně přidané lehké referenci povolit pouze přesné nové assembly, ne celou rodinu těžkého AppComponents. Současný forbidden substring by mohl zachytit i název nové lehké rodiny; výjimka musí být přesná a doplněná transitive closure guardem. |
| `Rendering_components_inject_no_services_and_public_signatures_stay_in_the_light_graph` | Zachovat. Nové typy smějí reprezentovat čistý merge model, ne service/DbContext/principal. |
| `Reload_after_a_conflict_discards_the_draft_and_accepts_the_latest_revision` uvedený S16 | Stále platí pro explicitní confirmed discard/reload. Upravit klikací flow o potvrzení, přidat samostatný merge-preserves-draft test. Nesmazat jen proto, že přibylo lepší řešení. |
| Same-target/unchanged-context no-reload tests | Zachovat pro pouhý parameter echo. Doplnit explicitní foreign invalidation trigger; neproměnit test v „každý render musí refresh“. |
| Mixed own-read-back / foreign collections persistence tests | Zachovat jako defence regressions; coherent read přidává jinou záruku a postcommit R2 stále existuje. |
| Native dialog z-index string assertions, jestliže je local census najde | Neoznačovat samy za UI proof. Nahradit/rozšířit real native top-layer pointer/focus browser testem; jednoduchý style assertion může zůstat jako detail, ne gate. |
| Testy dovolující tokenless existing replacement, jestliže je impact skutečně najde | Po změně kontraktu nahradit expected rejection a explicitním create/maintenance testem. Neponechat allow-all jen pro pohodlí fixture. Zde není tvrzen konkrétní takový test name bez inspekce. |
| Testy celých source text snapshots / starých komponent | Mazat až po ověření, že už nechrání aktuální consumer a invariant je jinde. Název „legacy“ nestačí. |

## Fáze úklidu

Před pilotem pouze inventarizovat stale test references, které brání správné malé test selection, a opravit dotčené fixtures/guards. Při každé vlně aktualizovat přímo dotčený kontrakt. Samostatné plošné přejmenovávání, přepis celé test infrastruktury či test-project reorganizace není předpoklad concurrency.

Deletion receipt musí obsahovat původní FQN/file, proč je behavior skutečně neplatný, aktuální production references, náhradní test/invariant nebo důvod, že už není relevantní, a discovery před/po. Žádné „test flakoval, tak ho smazat“, vypnutí categories, broad baseline regeneration či ignorování nových warnings bez kontroly.

Historický secret-scanning failure v S16 byl spojen s retained ignored artifacts. Neznamená povolení smazat celé `artifacts`, `.artifacts`, gitignored uživatelská data nebo vypnout secret scan. Current failure znovu zjistit a řešit jen v prokazatelně task-owned fixtures; citlivé soubory nikdy nekopírovat do výsledného reportu.

Úklid dokončit malým ledgerem `kept / changed-with-replacement / deleted-with-evidence / deferred`. Prázdný seznam legitimně smazaných testů je v pořádku. Počet smazaných testů není cílem ani známkou úspěšné implementace.
