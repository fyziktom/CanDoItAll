# 06 – Prioritizace oprav a rizika

## P0 – Opravy nutné před dalším důvěryhodným runem

### P0.1 Resolve placeholders

Bez resolved script refs může agent nebo recovery guidance používat `artifacts/process-runs/{CurrentProcessRunId}/...`. To je špatný základ pro tool receipts i preflight.

Riziko změny: střední. Existující testy očekávají unresolved placeholder, takže bude nutné je přepsat. Backward compatibility lze držet tak, že contributor může generovat placeholder, ale assignment materialization jej vždy resolveuje.

### P0.2 Completion gate aggregate

Bez aggregate diagnostik se bude pořád opravovat první symptom. Incident potřebuje současně znát missing helper receipt i empty solution.

Riziko změny: střední. Je nutné zachovat původní diagnostic codes, aby UI/history a existující testy nepadaly zbytečně.

### P0.3 Safe retry recovery

Bez použití `SafeRetry/CurrentStepRetry` bude každá bezpečně opravitelná gate chyba dál eskalovat.

Riziko změny: vyšší. Je nutný attempt budget a explicitní výjimky pro unsafe/policy/denied případy, jinak hrozí retry loop.

## P1 – Opravy nutné pro subprocesy a UI diagnózu

### P1.1 Child diagnostic propagation

Parent packet musí ukázat child root cause. Jinak bude uživatel vidět parent symptom a bude dělat blind rework.

Riziko změny: střední. Zasahuje parent/subprocess projection, ale behavior je jasně testovatelný.

### P1.2 Ledger-first child bridge

Fyzický artifact není accepted artifact. Pokud se to neopraví, parent může časem přijmout odmítnutý child output.

Riziko změny: střední až vyšší podle stávající závislosti na file fallbacku. Doporučuji fallback ponechat jen s warningem a testy.

### P1.3 Managed artifact acceptance wording/order

Snižuje zmatek v diagnostice. Není to primární příčina eskalace, ale je to důležité pro důvěryhodnost evidence.

Riziko změny: nízké až střední.

## P2 – Strukturální zlepšení

### P2.1 Runtime-owned .NET solution setup plan

Toto je nejlepší dlouhodobá oprava pro deterministic scaffolding. Sníží závislost na LLM u přesných tool call sekvencí.

Riziko změny: vyšší. Doporučuji nejdřív guard-only implementaci, pak runtime executor.

### P2.2 Template schema contracts

Přesun hardcoded subprocess contractů a tool plans do template schema odstraní drift mezi template a runtime.

Riziko změny: vyšší kvůli migracím a starým template definicím. Doporučuji fallback + validation warning v první fázi.

### P2.3 Capability-aware assignment repair

Pomůže na obecné „agent nemá tool/skill/access“ problémy. Nemusí být blokující pro konkrétní incident, protože zde agent tool obecně měl, ale nevolal jej.

Riziko změny: střední.

## Největší anti-patterny, kterým se vyhnout

1. Přidat jen další prompt pravidla.
2. Zvýšit počet blind retries bez diagnostic repair packetu.
3. Oslabit product readback gate, aby prázdná solution prošla.
4. Přijímat fyzicky existující child markdown jako parent evidence bez ledger acceptance.
5. Eskalovat každé `NeedsManager`, i když diagnostika říká safe/idempotent.
6. Řešit parent UI zprávu bez propagace child root cause.

## Krátká odpověď na otázku „proč navržené změny pomůžou“

- Placeholder resolution odstraní nejasné tool path kontrakty.
- Gate aggregation dá agentovi i runtime kompletní důvod selhání.
- Safe retry recovery zabrání tomu, aby deterministicky opravitelná chyba šla k člověku.
- Diagnostic rework packet zabrání opakování stejného mylného vzorce.
- Child diagnostic propagation odstraní slepý parent blocker.
- Ledger-first bridge zabrání falešně pozitivnímu přijetí odmítnutých artifactů.
- Runtime-owned plan odstraní LLM z přesného orchestration místa, kde LLM nemá přidanou hodnotu.
