# Validation strategy

## 1. Cíl validace

Validace má prokázat, že server je:
- funkčně správný,
- deterministický,
- bezpečný,
- použitelný pro Codex workflow,
- robustní vůči pádům, timeoutům a kolizím.

Nestačí, aby „nějak fungoval“ při jedné ruční zkoušce.  
Potřebujeme testovat:

- běžné happy path scénáře,
- hraniční stavy,
- recovery scénáře,
- bezpečnostní omezení,
- platform-specific chování.

## 2. Test pyramid

### 2.1 Unit tests
Cíl:
- rychle ověřit čistou logiku bez procesů.

Pokrýt:
- options validation
- path guard
- env whitelist filtering
- log redaction
- session compatibility comparison
- state transitions
- wait condition evaluation
- diagnostic categorization

### 2.2 Component tests
Cíl:
- otestovat více služeb dohromady bez plného MCP hostu.

Pokrýt:
- SessionCoordinator + RuntimeManager
- OperationRegistry + WaitEngine
- StaleProcessRegistry persistence
- ProcessSupervisor nad test fixture procesem

### 2.3 Integration tests
Cíl:
- spustit skutečný server a skutečné child procesy.

Pokrýt:
- stdio host discipline
- app start/stop/status/logs
- health waits
- quiet waits
- build/test operations
- stop and resume
- cleanup stale processes
- diagnostics

### 2.4 Manual / exploratory tests
Cíl:
- zachytit UX a prostředí-specifické problémy, které se těžko automatizují.

Pokrýt:
- WSL/containers/NFS watchers
- HTTPS development certificate problémy
- macOS/Linux nuance kill tree
- velké logy a dlouhé buildy

## 3. Test fixtures

Pro integration tests doporučuji vlastní malé fixture projekty.

### 3.1 HappyPathWebApp
Vlastnosti:
- ASP.NET Core app
- `/health` endpoint
- jasný startup log
- krátký startup čas

Použití:
- základní WatchRun
- health success
- log URL parsing

### 3.2 SlowStartWebApp
Vlastnosti:
- zpožděný startup nebo health readiness

Použití:
- timeout semantics
- wait engine stabilita

### 3.3 CompileErrorApp
Vlastnosti:
- fixture s možností přepnout kompilovatelnou / nekompilovatelnou variantu

Použití:
- start fail
- build fail
- diagnóza `BuildFailed`

### 3.4 ProcessTreeFixture
Vlastnosti:
- parent proces, který vytvoří child a případně grandchild procesy

Použití:
- ověřit kill tree
- stale cleanup
- cross-platform stop

### 3.5 RunnerDetectionFixture
Vlastnosti:
- jedna varianta bez MTP
- jedna varianta s MTP / global.json

Použití:
- runner detection summary

## 4. Co je kritické automatizovat

Automatizace je povinná minimálně pro všechny P0 scénáře z `12-validation-matrix.csv`.

To typicky znamená:
- stdio cleanliness
- config fail-fast
- workspace info
- app start/stop/status/logs
- wait healthy
- wait quiet
- build StopAndResume
- tests StopAndResume
- stale cleanup
- path guard
- unexpected exit

## 5. Jak testovat stdio host bezpečně

Protože stdio MCP server nesmí znečistit stdout, musí existovat test, který:

1. spustí server jako child process,
2. provede minimální MCP handshake nebo tool call,
3. paralelně sleduje stdout stream,
4. ověří, že mimo protokol na stdout nepadá žádný textový log.

To je release-blocking test.

## 6. Jak testovat WaitEngine

WaitEngine testuj ve dvou vrstvách:

### Unit
- čisté vyhodnocení condition nad mock state snapshoty

### Integration
- skutečný start app
- skutečné logy
- skutečné health URL
- skutečný timeout

`QuietSinceCursor` testuj vždy s reálnou změnou souboru nebo simulovaným log emitterem.

## 7. Jak testovat StopAndResume

Scénář:
1. spusť WatchRun session
2. potvrď Healthy
3. zavolej build/test s `whenAppRunning=StopAndResume`
4. čekej na operation completion
5. ověř, že:
   - session byla zastavena
   - operace proběhla
   - app session byla znovu spuštěna
   - app je znovu Healthy

Důležité:
- výsledný response musí nést `resumeOutcome`
- logy musí ukázat přechod stavů

## 8. Jak testovat stale cleanup

Nejlepší je dvoukrokový integrační test:

1. první proces serveru spustí app session a vytvoří registry record
2. první server je nečekaně ukončen bez cleanup
3. druhý server při startu provede cleanup
4. ověř, že předchozí proces už neběží

Varianta:
- ruční call `cleanup_stale_processes` nad uměle vytvořenou stale registrací

## 9. Jak testovat security boundary

Povinné testy:
- path outside workspace
- disallowed env key
- external health host blocked
- log redaction na známých patternách

Bezpečnostní testy nejsou nice-to-have. Jsou součást acceptance.

## 10. Čemu při validaci nevěřit

Nevěř jen:
- ručnímu „mně to běží“
- jedné platformě
- jednomu startupu bez restartu
- čistému repozitáři bez kolizí
- jedné rychlé success cestě

Skutečná spolehlivost se ukáže až v:
- opakovaných start/stop cyklech
- kolizních scénářích
- recovery scénářích
- delších bězích

## 11. Release gate

Release kandidát nesmí projít, pokud neplatí:

- všechny P0 scénáře v matrix jsou green
- žádný známý blocker v `17-qa-review/04-known-risks-and-open-questions.md`
- runbook pokrývá hlavní troubleshooting scénáře
- prompts a docs odpovídají skutečné implementaci
- stdout discipline test je green
- stale cleanup test je green
- build/test nepoužívají `dotnet watch test`

## 12. Doporučená CI vrstva

I když MCP server cílí primárně na lokální workflow, doporučuji CI minimálně pro:

- build serveru
- unit tests
- integration tests bez browser vrstvy
- Windows + Linux matice

macOS může být volitelná, ale je velmi žádoucí kvůli procesnímu chování.

## 13. Vazba na QA review

QA review v `17-qa-review/*` doplňuje validaci o:
- threat model
- failure injection
- compatibility matrix
- runbook

Validace a QA review se navzájem doplňují.  
Jedno bez druhého nestačí.
