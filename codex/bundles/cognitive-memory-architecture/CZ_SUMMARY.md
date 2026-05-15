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

## Závěr

Návrh je složitější než běžný RAG, ale dobře sedí k dlouhodobému směru CanDoItAll jako procesního a agentického operačního systému. Největší hodnota bude v tom, že agenti nebudou jen hledat podobné chunky, ale budou umět vybavit relevantní zkušenost, pochopit kontext, otevřít správný detail a po práci uložit novou zkušenost zpět do paměti.
