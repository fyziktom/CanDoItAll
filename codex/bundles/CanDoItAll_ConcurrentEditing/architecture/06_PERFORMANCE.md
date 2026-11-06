# Výkonnostní rizika a měřitelné přijímací podmínky

V tomto auditu nebyla aplikace spuštěna a žádné nové latence nebyly změřeny. Níže jsou navržené rozpočty a experimenty. Codex zaznamená skutečný baseline a může upravit číselný cíl s doložením důvodu; nemůže obejít strukturální zákazy a tvrdit rychlost jen podle subjektivního dojmu.

## Hot-path pravidla

1. Bez editace a bez relevantního eventu nevzniká periodický DB dotaz, diff ani full clone. UI render jen čte presentation state.
2. Per-keystroke vzniká nejvýše levná dirty/revision informace; ne serialize celého DTO, deep equality všech kolekcí nebo serverový publish.
3. Publisher nečeká pod DB/global lockem na subscriber callback. Odběry se vyhledají podle topic/key, nikoli průchodem všemi formuláři.
4. Relevantní burst vede na bounded invalidation/coalescing, ne jeden query na každý event nebo na každý řádek.
5. Coherent reads mají krátký DB snapshot scope; žádná long-lived transaction/DbContext po dobu editace. Read transaction nemá nechtěně serializovat všechny zapisující uživatele.
6. Runtime counters, heartbeats, token usage, scheduler fire history a streaming messages nesmějí neustále invalidovat editovaný configuration snapshot.
7. Diff/render metadata používá explicit descriptors; žádné reflection scanning při každém renderu a žádné názvové lookup N+1 pro stovky referencí.

## Konkrétní existující rizika

ProjectStructure globální wakeup/scan je přímo vidět v S20; cílený nový feed nesmí mít tuto násobnou práci. TestLab ListAsync čte široké sady včetně run records (S29); automatické requery při každé aktivitě by bylo nebezpečné. Workflow list projection načítá DefinitionJson (S24); pro live catalog zvážit existující bounded search/read model, ne nově celé grafy. Prompt search už má debounce a request generation (S13); znovupoužít je, ale oddělit user-search loading od quiet refresh.

Tyto nálezy nejsou povolením zahájit obecný performance refactor všech modulů před pilotem. U každé plochy nejprve vybrat lehkou query cestu; složitější změnu výslovně zařadit do její schválené vlny.

## Výchozí cíle pro pilotní měření

| Oblast | Návrhový cíl / ověření |
|---|---|
| Nepříslušné eventy | 0 owner detail reads a 0 view rerenders v nesouvisejících editorech; interní routing práce nezávislá na celkovém počtu jiných editorů kromě vyhledání partition |
| Idle | Po dokončeném initial load žádné nové polling DB query po dobu 60 s v izolované fixture bez background událostí |
| Burst list | 100 změn stejného katalogu v krátkém burstu vede k coalescovaným readům, typicky 1 probíhající + 1 trailing; přesné číslo test řízeným schedulerem, ne náhodným wall-clock timeoutem |
| Latence hintu | Na lokální baseline cílit běžně pod 500 ms p95 od commit po banner; uvést network/circuit overhead a případný debounce, ne zaměnit s časem Save |
| Edit responsiveness | Nový frontend merge/notification kód nemá vytvářet main-thread long tasks nad 50 ms při běžném psaní; porovnat stávající baseline a p95/p99 input/paint |
| Zápis a otevření | Na stejných datech, stroji a driveru změřit baseline versus změna; stabilní reprodukovatelná regrese p95 přes cca 10 % vyžaduje vysvětlení/opravný krok, zvlášť u absolutně velké latence |
| Large text | Detailní automatický diff může mít např. soft cap 256 KiB / 10 000 řádků na obraz; musí být pod stávající validní storage kapacitou podporován ruční bezpečný fallback, ne ztráta obsahu |
| Bounded collection | Vykreslit změněné skupiny postupně; např. 200 viditelných řádků s dalšími na vyžádání. Limits diff UI nesmějí znamenat částečné potiché uložení |
| Lifetime | 100 cyklů otevři/zavři editor nezvyšuje počet active subscriptions, neudržuje drafty přes closures, nezanechá timers/JS refs |

Čísla soft capů jsou experimentální start, nikoli produktová změna validačních limitů. Capture skutečného maximum prompt content z `PromptGalleryLimits`, ne hardcoded domněnku. Schema adapters potřebují limit chybných/duplicitních keys i řádků; přetečení musí skončit explicitně bezpečně.

## Měřicí sada

Před pilotem zaznamenat idle a edit-loop stávající Gallery na fixním seed datasetu. Během vývoje čisté core benchmarky na 10/100/1000 polí a různých délkách textu. Před stop-gate reálné dva browser contexts na izolovaném test hostu s PostgreSQL a samotné ruční sanity 5032 bez zátěžových fixture zásahů do uživatelových dat.

Pro routing použít syntetickou in-process zátěž 1/10/100/1000 odběrů v několika scope a jeden aktivní key; neotevírat tisíc reálných circuitů na pracovní 5032. Burst/gap/backpressure testy s kontrolovaným clock. Profil paměti: baseline → opakované open/merge/cancel/close → GC/retained subscription inspection. Není dostačující pouze GC total bytes v jednom okamžiku.

Metriky: active subscriptions, queued/coalesced/dropped-to-gap hints, owner read count a latency, list requery count, render count, merge input bytes/rows/duration, write outcome categories, failed observer count. Štítky nesmějí obsahovat record IDs ani uživatelské texty v nekonečné cardinalitě. Logging detailního B/L/R obsahu je zakázaný.

## Query a rollout brány

U každého nového list adapteru zaznamenat query count a plán aktuálního filter/page dotazu. Vyžaduje-li invalidace celý katalog, označit jej `DEFERRED-LIVE` a nejprve dodat bounded read/query seam, ne potichu zapnout event storm. Explicitní uživatelský refresh může do té doby zůstat, owner CAS se nesmí odložit.

Single-process pilot nemusí předem benchmarkovat distribuovaný broker. Nesmí ale mít nekonečné queue ani všeobecné singleton collection content cache. Víceinstanční deployment přidává zvláštní kapacitní a doručovací proof, ne pouhou konfiguraci flagu.
