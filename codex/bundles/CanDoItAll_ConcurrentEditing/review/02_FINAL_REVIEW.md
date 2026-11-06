# Závěrečná kontrola návrhu před zabalením

## Rozsah této kontroly

Byla provedena kontrola obsahu balíku proti zadání, zdrojovému evidence ledgeru, hranicím modulů, protokolům draftu a navržené test strategii. Následné automatizované ověření kontroluje odkazy, ID, datové příklady a integritu souborů. **Není to C# build, spuštění aplikace ani důkaz, že implementace již funguje.**

## Zjištěné obavy a zapracované korekce

| Kontrola | Riziko původně jednoduchého návrhu | Finální korekce |
|---|---|---|
| Aktuálnost | Převzít Oct16 owner map jako dnešní pravdu | Oddělené CODE/DOC/HISTORICAL stavy; novější Projects, Resources, Workspace výslovně nepředělávat podle starého auditu |
| Baseline | Zaměnit odeslaný návrh za původní B | Oddělené B/L/R od S/L/A; Projects Submitted není original base |
| Konzistentní read | Sloučit root jedné revize s novějšími child rows | Coherent snapshot jako pilot prerequisite, skutečný PostgreSQL test |
| Accepted acknowledgement | Povýšit current read jiného autora na vlastní accepted A | Exact native accepted snapshot/normalization, preserve mixed-read regressions |
| Pozdní acknowledgement | Bezpodmínečné B := A by mohlo přepsat již přijatou R2 | Připojit historický výsledek, ale zachovat novější native baseline/view; samostatný scénář V25 |
| Nevalidní raw draft | „Validní merge“ by mohl zakázat převzetí jiného pole při rozepsaném čísle | Rozlišena platnost plánu od validace celého formuláře; raw stav se zachová a Save zůstane blokovaný |
| Preferred invariant | Testovací příklad by mohl vynutit právě jeden model místo nejvýše jednoho | Upraven V17; žádná změna doménové povinnosti volby modelu |
| Prompt lifecycle | Převzetí current stamp by samo odarchivovalo cizí record | Explicitní lifecycle review, no implicit restore při content merge |
| Token přesnost | Zbytečně „opravit“ již normalizované timestampy | Zachovat current microsecond NextUpdatedAt a testovat monotonic exact equality |
| Draft dirty | Použít page editorDirty jako nesaved dirty | Oddělený catalog invalidation flag od per-editor/raw-input dirty |
| UI závislosti | Přitáhnout těžký AppComponents do lehkého Prompts rendereru | Host composition nebo nová lightweight family, zachované přesné boundary tests |
| Notifications | Zvýšit pouze CSS z-index nebo dát body popover | Top-layer-aware single outlet v interaktivním modal subtree + reálný hit-test |
| Notification lifecycle | Duplikované renderery s duplicitními expiry timery | Jeden logical message lifetime, stack changes nezmění TTL/announcement |
| Feed výkon | Povýšit globální ProjectStructure signal na global app bus | Cílené subscriptions, bounded coalescing, baseline metrics, current feed migrace až později |
| Feed correctness | Potlačit všechna oznámení stejného uživatele | Origin editor/operation correlation, dvě okna stejného účtu povinně |
| Query freshness | Canonical commit event dorazí před search projection | Projection-ready/freshness strategy, žádný false completed invalidation ani nekonečný polling |
| Privacy | Hints by nesly data po ztrátě oprávnění | Trusted scope policy, revocation fence a minimal payload; no raw secrets |
| Undo | Vrácení merge by znovu nastavilo starý token | Undo jen místní návrh vůči zachované nové baseline; persisted compensation zvlášť |
| Canvas | Předpokládat všechny drags jen lokální | Process authoring persistent commands mají native receipt/observation, žádný fake local rollback |
| Test strategy | Obecné „spusť full suite“ / historické počty | Impact-based selection, build-backed discovery, owner DB + browser proof, širší gate jen odůvodněný |
| Úklid | Smazat užitečné reload/boundary/race testy jako legacy | Žádný nepotvrzený deletion, explicitní nový behavior test místo smazané garance |
| Stop gate | Po zelených testech automaticky pokračovat CRM | Samostatný P3, AWAITING_USER_APPROVAL, opravný loop také končí stopem |
| Statická kontrola balíku | První validátor neznal kvalifikované smíšené evidence stavy | Doplněn výslovný slovník všech stavů; každý řádek musí odkazovat na deklarovaný stav, žádné automatické přepsání na DIRECT |
| Claim of completeness | Vydávat rodinový source audit za provedený celo-repo/runtime proof | Evidence scope a konkrétní open items; povinný local census/owner closure pro každou vlnu |

## Výsledek

Návrh pokrývá všechny kategorie požadavků v [traceability](01_REQUIREMENTS.md). Pilot má konkrétní rozsah, dependency-safe základ, tester checklist, runtime runbook a tvrdou schvalovací hranici. Neuzavřené zdrojové/runtime otázky jsou pojmenované v [risks](03_RISKS_AND_OPEN_ITEMS.md), nikoli skryté pod obecným „hotovo“.

Finální balík byl po těchto korekcích znovu staticky ověřen. Podrobný výstup validačního skriptu je v [package-validation.json](package-validation.json). Hash manifest chrání celistvost dodaných souborů; není podpisem code review ani schválením implementace.
