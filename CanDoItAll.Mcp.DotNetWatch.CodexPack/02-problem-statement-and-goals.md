# Problem statement a cíle

## Výchozí problém

Pro CanDoItAll chceš MCP server, který umožní Codexu plynule:
- ladit UI,
- spouštět aplikaci,
- čekat na rebuild/restart,
- spouštět testy,
- číst logy,
- řešit chyby bez manuální obsluhy.

Běžný přístup „dej agentovi možnost zavolat `dotnet watch`“ je ale nedostatečný.  
V praxi naráží na tyto problémy:

### P1 — Agent obchází domluvený lifecycle
Agent si někdy aplikaci pustí nebo stopne sám.  
Důsledky:
- rozpadne se představa „někde na pozadí běží watch“,
- vzniknou duplicity procesů,
- locknou se porty nebo binárky,
- není jasné, který běh je ten správný.

### P2 — Buildy a testy nejsou kompatibilní s volně běžícím watcherem
U řešení s `dotnet watch` může aktivní běh kolidovat s buildem a testem.
Důsledky:
- build nebo test visí,
- agent začne chaoticky zabíjet procesy,
- ztrácí se kontinuita workflow.

### P3 — Čekání je nedeterministické
Když rebuild trvá různě dlouho, klientský `sleep 5s` nefunguje spolehlivě.
Důsledky:
- test se spustí moc brzy,
- UI validace běží nad nedostartovanou aplikací,
- selhání jsou flaky a těžko reprodukovatelná.

### P4 — Po pádu kontextu zůstávají procesy
Když spadne agent nebo MCP server, mohou zůstat:
- běžící `dotnet` procesy,
- child procesy,
- obsazené porty,
- zamčené artefakty.

### P5 — Logy bez cursors a korelace jsou prakticky nepoužitelné
Při watch režimu přibývá hodně výstupu.
Bez:
- cursorů,
- session ID,
- operation ID,
- jasné diagnostiky  
agent neví, co z logů je nové a relevantní.

## Primární cíle

### C1 — Zavést server-owned lifecycle
Server musí být jediným zdrojem pravdy pro:
- start aplikace,
- stop aplikace,
- stav aplikace,
- build a test operace,
- logy a waity.

### C2 — Zajistit stabilní workflow pro UI iterace
Cílový flow:

1. `workspace_info`
2. `app_start`
3. změna kódu
4. `app_wait`
5. UI kontrola
6. případně `solution_build` / `tests_run`
7. diagnostika při chybě

### C3 — Eliminovat nahodilé čekání
Server musí umět explicitně:
- čekat na ready,
- čekat na healthy,
- čekat na quiet period,
- čekat na dokončení build/test operace.

### C4 — Ošetřit konflikty mezi app během a build/test
Build a test nesmí pasivně předpokládat, že aktivní watch session je bezpečná.
Musí existovat policy:
- `StopAndResume`
- `StopOnly`
- `Fail`
- `ContinueIfSafe`

### C5 — Poskytnout strojově čitelnou diagnostiku
Při start failu nebo timeoutu musí jít vrátit:
- klasifikace problému,
- související log řádky,
- doporučené další kroky.

### C6 — Udělat řešení bezpečné a review-friendly
Server musí:
- být omezený na CanDoItAll workspace,
- nepřijímat raw shell příkazy,
- nepsat mimo MCP protokol na stdout,
- redigovat tajná data z logů,
- být testovatelný a auditovatelný.

## Sekundární cíle

- zkrátit čas mezi změnou kódu a validací UI
- dát Codexu predikovatelné workflow
- omezit ruční zásahy
- zjednodušit lokální troubleshooting
- umožnit rozšíření o Playwright/browser kontrolu

## Co je úspěch

Návrh je úspěšný, pokud po implementaci bude platit:

- Codex už pro CanDoItAll **nepotřebuje** přímé `dotnet` CLI volání pro runtime/build/test workflow.
- Běžný vývojový cyklus lze dělat jen přes MCP tooly.
- Build/test/app start mají jasné wait a log rozhraní.
- Při chybě dostane agent použitelnou diagnózu místo chaotického textového výstupu.
- Po pádu serveru se dají bezpečně uklidit vlastní osiřelé procesy.
