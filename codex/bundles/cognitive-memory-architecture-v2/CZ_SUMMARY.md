# České shrnutí pro CTO/architekta

Tento bundle navrhuje samostatný modul `Cognitive Memory` pro CanDoItAll. Cílem není udělat jen lepší RAG, ale paměťový systém podobnější lidské paměti: nejdříve hrubé vybavení tématu, potom asociace, potom zaostření na konkrétní kontext a až nakonec detailní otevření zdrojů.

## Hlavní rozhodnutí

Qdrant není zdroj pravdy. Qdrant je pouze rychlá projekce nad pamětí. Skutečná paměť je kombinace raw zdrojů, canonical records, explicitního grafu vztahů, epizod, procedur, rozhodnutí, reflexí, confidence/activation/staleness stavů a auditních stop.

## Co už v CanDoItAll existuje a používá se

- modulární architektura,
- EF model discovery,
- storage driver pro FileSystem/IPFS/FTP,
- workbench uzly a linky s X/Y souřadnicemi,
- process runtime s bohatými epizodickými záznamy,
- workflow runtime a executory,
- MAF adaptér a context providery,
- plugin systém,
- RAG/Qdrant driver,
- semantic/embedding driver.

## Co je potřeba doplnit

- source manifesty a source itemy,
- canonical memory records,
- memory itemy podle typů: source, working, episodic, semantic, procedural, decision, reflection, metacognitive,
- memory graph relations,
- staged recall orchestrator,
- consolidation engine pro idle/noční zpracování,
- Qdrant projection manager,
- human review queue,
- MAF context provider a workflow executory,
- distributed idle compute job protocol.
- Epistemic Drive vrstvu: knowledge coverage mapy, knowledge gapy, multi-dimensional knowledge need vector, learning proposal workflow a approval-gated learning tasky.

## Epistemic Drive

Epistemic Drive není náhodná zvědavost agenta. Je to metakognitivní vrstva, která během noční/idle konsolidace sleduje, kde se protíná vysoké používání, riziko, nejistota, zastaralost, chyby, strategický směr projektu a slabé zdrojové pokrytí.

Důležité pravidlo: nesmí se z toho udělat jen jedno číslo `priority score`. Systém musí ukládat celý vektor signálů, evidence refs, vazbu na aktivní projektové směry, ROI odhad, kategorii a vysvětlení.

Výstupem není automatické učení z internetu. Výstupem je human-reviewable learning proposal. Uživatel může návrh schválit, odmítnout, odložit, zúžit scope, přidat zdroje, nejdřív požádat o probing, převést ho na Codex bundle nebo přiřadit člověku/agentovi.

Typický příklad je Docker operational knowledge: systém vidí, že Docker se často používá pro plugin isolation, deployment, local development a workflow executor sandboxing, ale znalost je slabá v networkingu, volumes, secrets/configs, Compose failure modes a Swarm. Navrhne cílený learning task nad oficiální dokumentací, ale spustí ho až po schválení.

## Důležité pro mindmapy

Mindmapa se nebere jako obyčejný dokument. Bere se jako kurátorský znalostní graf. Poloha uzlů je signál. Když jsou dvě témata semanticky podobná, ale prostorově/grafově oddělená, systém to má chápat jako „souvisí, ale v jiném kontextu“.

Typický příklad:

- produkční Docker deployment,
- testovací Docker simulace.

Obě věci jsou Docker/deployment, ale nesmí se slít do jedné pravdy.

## Doporučený první vertical slice

```text
Workbench nodes -> source items -> canonical memory -> Qdrant projection -> recall -> trace viewer
```

Tento řez rychle ověří hodnotu systému bez toho, aby se hned implementovala celá distributed/idle část.

## Největší rizika

- příliš agresivní slučování podobných témat,
- záměna testovací a produkční znalosti,
- ztráta source provenance,
- ukládání vygenerovaných shrnutí jako pravdy,
- embedování secretů,
- používání Qdrantu jako DB,
- chybějící review pro high-risk procedury.
- redukce Epistemic Drive na jedno skóre,
- neřízené externí učení bez schválení,
- learning-derived znalosti bez source refs.

## Závěr

Návrh je složitější než běžný RAG, ale dobře sedí k dlouhodobému směru CanDoItAll jako procesního a agentického operačního systému. Největší hodnota bude v tom, že agenti nebudou jen hledat podobné chunky, ale budou umět vybavit relevantní zkušenost, pochopit kontext, otevřít správný detail a po práci uložit novou zkušenost zpět do paměti.

## Doplnění: Interactive Memory Probing

Do balíčku jsem doplnila samostatnou architekturu pro `Interactive Memory Probing`. To je režim, ve kterém si můžeš s pamětí povídat jako se studentem: ptát se, proč si něco myslí, odkud to ví, jestli existuje novější zdroj, kde si není jistá, nebo ji přímo opravit.

Důležité pravidlo: rozhovor s pamětí nesmí přímo přepisovat pravdu. Rozhovor vytváří evidence, correction candidates, review items, knowledge gapy a regression testy. Aktivní canonical memory se smí změnit až přes autoritativní služby, policy a případně human review.

Tento probing se propojuje s vrstvou `Epistemic Drive`. Epistemic Drive může generovat otázky podle slabých znalostních regionů, stale záznamů, rozporů, aktivních projektových směrů a řízené náhodnosti. Výsledky probingu se pak vrací zpět jako evidence pro coverage mapy, knowledge need vector a learning proposals.

Největší nová hodnota je v tom, že z běžného dialogu vznikne živý fuzz testing paměti. Když paměť udělá chybu, lze z toho vytvořit review item nebo regression test, aby se stejná chyba později nevrátila. První povinný scénář je Docker context separation: produkční Docker deployment, testovací Docker simulace, lokální Compose a CI Docker se nesmí slít do jedné pravdy.

## Doplnění: neuro-kognitivní patch

Architekt přidal důležitou vrstvu, která posouvá návrh od dobrého enterprise RAG/paměťového systému k řízenější kognitivní architektuře. Do v2 bundle jsou teď zapracované:

- cognitive workspace: aktivní pracovní rámec s focus slots, cíli, otevřenými otázkami a inhibovanými kandidáty,
- attention router: explicitní rozhodnutí, jestli odpovědět, dohledat, zeptat se, spustit probing, review, replay, learning proposal nebo abstain,
- claim/evidence/belief ledger: paměťové itemy už nejsou jediná jednotka pravdy; pod nimi jsou atomické claims se support/attack evidencí,
- mutation authority: autoritativní změny paměti nejdou přes veřejný upsert, ale přes idempotentní command s evidencí, audit logem, policy a projection invalidation,
- prediction error a salience signals: chyby, překvapení, riziko, užitečnost, rework cost, user interest a source weakness se ukládají jako vícerozměrné signály, ne jako jedno skóre,
- temporal replay: důležité, slabé, rizikové nebo často používané vzpomínky se mají přehrávat a revalidovat,
- procedural skill memory: procedury nejsou jen textové runbooky, ale skill graph s preconditions, steps, failure modes, validation evidence a maturity,
- simulation sandbox: hypotézy a analogie jsou explicitně speculative a nesmí se stát pravdou bez evidence/review,
- metamemory answer gate: systém musí před odpovědí umět říct, že nemá dost zdrojů, kontext je nejasný, claim je sporný, procedura je riziková nebo je lepší abstain.

Nejdůležitější dopad na plán: tyto části nejsou pozdní rozšíření. `claim/evidence/context/mutation authority` musí vzniknout ještě před source ingestion a recall. Workspace/attention a salience/prediction-error ledger musí vzniknout před recall/probing/Epistemic Drive. Teprve potom dává smysl stavět Dialogue Workbench, learning proposals, cross-project memory a distributed compute.

Další oprava: skórování nesmí být sada lokálních add/subtract vzorců. Přidáváme `01b-score-geometry-driver`, který zavádí typed score spaces, vektory, tvary/regiony, scalar projections a evaluation traces. Recall rank, attention routing, belief state, replay priority, probing assessment, answer confidence, Epistemic Drive i cross-project promotion musí používat tento společný model. Jedno číslo smí být jen odvozená hodnota pro UI sorting/frontu, ne zdroj rozhodnutí.
