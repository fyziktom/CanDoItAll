# Rizika, neuzavřené otázky a hranice důkazu

| Otázka / riziko | Zjištěno nyní | Co musí Codex doložit | Kdy je blocker |
|---|---|---|---|
| Novější HEAD / sibling source | Baseline konkrétní SHA; používané lokální zdroje nejsou z této relace viditelné | diff relevantních ownerů a skutečný Components pin/local graph | před změnou příslušného souboru |
| Main UI principal | Program registruje local-operator auth; detailní implementation chain nebyl zde celý přečten | skutečný authorized context a policy fallback v secured/local režimu | před feed subscription pilotu |
| Všichni Prompt writers | Draft/version/archive/favorite/staged create čtené; import/curator/detail caller graph ne celý | uzavřený current call graph, root revision a events na všech relevantních cestách | před pilot completion |
| Coherent read implementace | Sekvenční query zjištěné | PostgreSQL proof mezi child/root dotazy, absence dlouhé editor transaction | před používáním B/L/R |
| Exact accepted baseline | Native receipt existuje, normalized submitted obsah se používá | všechny owner normalizace/children/IDs tvoří přesné A | před pilot completion |
| Search driver / index lag | Drivers existují, aktivní binding zde nebyl spuštěn | actual query driver, refresh latency/watermark/failure behavior | před quiet-list acceptance |
| Některé current owner internals | Resources/Workspace current adapter/facade již změněné; starý audit není současný proof | aktuální writer/migration před jejich vlnou | neblokuje pilot, blokuje tvrzení o uzavření těchto ownerů |
| Kompletní per-file census | Rodiny všech modulů zmapované; neproveden místní mechanický průchod celého working tree | routes, custom/inline/drag edits, dynamic registries, filetools main-UI consumers, unresolved=0 pro hotovou vlnu | P0 mapa a každá rollout closure |
| Runtime tests a výkon | Zde neprovedeny | cílené tests, real DB/browser a před/po baseline | bez důkazu neoznačit pilot test-ready |
| Full multi-instance | Feed precedent jen process-local | skutečný deployment; shared transport only if needed; no falsely promised delivery | pokud cílový ruční test skutečně jde přes více serverů |
| Native notification interaktivita | top-layer příčina zjištěná; outlet návrh není implementovaný | real hit-test, click/focus/stack/expiry a podporované browser engines | pilot blocker |
| Test „úklid“ | žádný bezpečný smazatelný test potvrzen | konkrétní invariant/replacement a current discovery pro každý deletion | před každým deletion |
| Plné ownership / persisted Undo | vědomě neimplementované následné funkce | samostatný scope, produktové rozhodnutí, migrations a bezpečnost | neblokují tento pilot při správných seams |
| Durable drafts přes crash | není slíbeno; existing operation stores musí zůstat | explicitní privacy/storage policy pro novou feature, jen pokud objednána | neblokuje řízené modal retention; nesmí být tvrzena záruka crash survival |

Při nedostupné dependency, test prostředí nebo runtime právech reportuj konkrétní blokátor a poslední ověřený stav. Nenahrazuj neprovedený test slovem „covered“ jen proto, že v dokumentaci existuje jeho název. Zároveň neodkládej opravy potvrzených silent overwrites za neurčitý budoucí security projekt.
