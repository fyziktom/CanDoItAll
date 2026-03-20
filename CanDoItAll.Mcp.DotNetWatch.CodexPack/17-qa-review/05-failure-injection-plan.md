# Failure injection plan

Cílem je cíleně rozbíjet běhy, ne jen potvrzovat happy path.

## 1. Principles
- Inject one failure at a time when possible.
- Capture logs, status snapshots, and resulting diagnostics.
- Validate both the immediate outcome and the cleanup/recovery behavior.
- Prefer reproducible fixture-based failures.

## 2. Scénáře

### FI-001 — Port conflict before app start
**Setup:** Obsadi cílový port jiný lokální proces.  
**Action:** Zavolej `candoitall_app_start`.  
**Expected:** start selže nebo přejde do Failed; `diagnose_start_failure` vrátí `PortInUse`.

### FI-002 — Compile error during WatchRun
**Setup:** Vlož do app projektu kompilátorovou chybu.  
**Action:** `app_start(mode=WatchRun)` nebo změna souboru při běžící watch session.  
**Expected:** logy ukážou build fail, `app_wait(Healthy)` timeoutne nebo failne s evidencí.

### FI-003 — Health endpoint timeout
**Setup:** App běží, ale health endpoint nevrací success nebo neexistuje.  
**Action:** `app_wait(condition=Healthy)`.  
**Expected:** Timeout s diagnostickým hintem; status ukáže Running bez Healthy.

### FI-004 — Unexpected external kill
**Setup:** Aktivní app session.  
**Action:** Externě zabij parent nebo child proces.  
**Expected:** session přejde do `ExitedUnexpectedly` nebo `Failed`; wait se ukončí.

### FI-005 — Long-running build timeout
**Setup:** Build fixture uměle čeká velmi dlouho.  
**Action:** `solution_build(timeoutMs=<krátký limit>)`.  
**Expected:** operation skončí `TimedOut`; logy a summary jsou akční.

### FI-006 — Build under running watch without preemption
**Setup:** Aktivní watch session.  
**Action:** `solution_build(whenAppRunning=Fail)`.  
**Expected:** žádný build se nespustí; vrátí se `RunningSessionConflict`.

### FI-007 — Stale registry record to dead process
**Setup:** Do registry vlož record na PID, který už neexistuje.  
**Action:** `cleanup_stale_processes`.  
**Expected:** proces je přeskočen bezpečně a registry se uklidí.

### FI-008 — Stale registry record to live orphan process
**Setup:** První server spustí app a havaruje.  
**Action:** Druhý server bootstrap nebo ruční cleanup.  
**Expected:** orphan process je nalezen, verifikován a ukončen.

### FI-009 — Disallowed project path
**Setup:** Request použije projectPath mimo workspace.  
**Action:** `app_start`.  
**Expected:** `PathOutsideWorkspace` / `ValidationError`.

### FI-010 — Disallowed health host
**Setup:** Health URL směřuje na nepovolený host.  
**Action:** start serveru nebo health probe.  
**Expected:** fail-fast config error nebo security violation.

### FI-011 — Output redaction check
**Setup:** App nebo test fixture vypíše token/password pattern.  
**Action:** čti logy přes MCP tools a file logs.  
**Expected:** citlivá část je redigovaná.

### FI-012 — Rude edit during watch
**Setup:** Aktivní watch session.  
**Action:** Proveď změnu, která vyžaduje restart.  
**Expected:** session se restartuje nebo failne akčně; nezůstane viset na interaktivním promptu.

## 3. Evidence to capture
Pro každý injection run zachytit:
- time started
- request payload
- response payload
- relevant log entries
- final session/operation status
- whether cleanup succeeded

## 4. Release gate use
Ne všechny scénáře musí být v CI každou chvíli, ale minimálně:
- port conflict
- unexpected kill
- stale cleanup
- timeout
- path guard
- redaction

by měly mít pravidelnou opakovatelnou coverage.

## 5. Exit criteria
Failure injection plan je považovaný za splněný, pokud:
- každý scénář má vlastní očekávání,
- kritické scénáře mají automatizovaný test nebo jasný semi-auto postup,
- žádný kritický scénář nevede k tichému, nediagnostikovanému selhání.
