# User stories a acceptance criteria

Níže je backlog na úrovni user stories. Podrobnější implementační rozpad je v `10-backlog.csv`.

## Epic: Workspace & Configuration

### US-WS-001 — Získání informací o workspace
**Jako** Codex agent  
**Chci** znát kořen workspace, solution, výchozí app projekt, health endpointy, podporované režimy a aktivní session  
**Aby** aby nemusel hádat cesty ani spouštěcí příkazy  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool workspace_info vrátí absolutní i relativní cesty na workspace root a solution.
- Vrátí výchozí app project, doporučený režim startu a seznam test projektů, pokud jsou konfigurovány.
- Vrátí aktivní app session a běžící operace, pokud existují.
- Vrácená data neobsahují tajné hodnoty z konfigurace.

## Epic: App Lifecycle

### US-APP-001 — Spuštění aplikace v režimu Watch
**Jako** Codex agent  
**Chci** spustit CanDoItAll aplikaci přes MCP tool  
**Aby** aby bylo možné ladit UI bez ručního startu  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool app_start podporuje režim WatchRun a RunOnce.
- Ve WatchRun použije dotnet watch --non-interactive run nad explicitním projektem.
- Procesní výstup je zachycen do interního log bufferu a není zapisován na stdout MCP serveru.
- Tool vrátí sessionId, observed urls a počáteční cursor do logů.

### US-APP-002 — Idempotentní start aplikace
**Jako** Codex agent  
**Chci** opakovaně volat app_start bez duplicitních procesů  
**Aby** aby se agent nezacyklil nebo nezaložil více watcherů  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Pokud už běží kompatibilní session a reuseIfCompatible=true, vrátí se existující session.
- Kompatibilita porovnává projectPath, mode, framework, configuration, launchProfile, appArgs a relevantní env overlay.
- Pokud běží nekompatibilní session, server podle policy vrátí konflikt nebo session nahradí.
- Nikdy nevzniknou dva aktivní managed app procesy pro stejný workspace bez explicitního multi-instance režimu.

### US-APP-003 — Zastavení aplikace a ukončení stromu procesů
**Jako** Codex agent  
**Chci** spolehlivě zastavit app session  
**Aby** aby mohl bezpečně stavět, testovat nebo uvolnit porty  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool app_stop ukončí celý strom procesů, ne pouze parent dotnet proces.
- Ukončení respektuje grace period a po jejím vypršení provede force kill.
- Po stopu se session označí jako Stopped a registry stale procesů se vyčistí.
- Opakovaný stop nad už zastavenou session je bezpečně idempotentní.

### US-APP-004 — Zjištění stavu aplikace
**Jako** Codex agent  
**Chci** získat jasný stav session  
**Aby** aby věděl, zda čekat, číst logy nebo řešit chybu  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool app_status vrátí state, lastExitCode, lastRestartUtc, sessionVersion a observed urls.
- Pokud je povolen health probe, vrátí i poslední health status.
- Vrátí poslední log cursor a summary posledních klíčových událostí.
- Status je čitelný i během startu, restartu a stopu.

## Epic: Logs & Waits

### US-LOG-001 — Inkrementální čtení logů přes cursor
**Jako** Codex agent  
**Chci** číst jen nové logy od posledního bodu  
**Aby** aby nemusel znovu analyzovat celý výstup  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool app_logs přijímá cursor a vrací entries seřazené podle sekvenčního čísla.
- Response vrátí nextCursor, truncated flag a totalAvailableAfterCursor.
- Každá položka obsahuje timestamp, source, stream, sequence a text.
- Cursor je monotónní a nezávislý na restartu session; restart se projeví jako událost s novou sessionVersion.

### US-WAIT-001 — Čekání na připravenost aplikace bez Sleep
**Jako** Codex agent  
**Chci** mít explicitní wait tool  
**Aby** aby mohl čekat deterministicky místo odhadovaných sleepů  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool app_wait podporuje conditions Ready, Healthy, Running, Stopped, QuietSinceCursor a LogMatch.
- Tool vrátí timeout outcome s vysvětlením, co se mezitím pozorovalo.
- Wait nepoužívá tvrdě zakódovaný Thread.Sleep na straně klienta; polling a eventing řeší server.
- Wait může být zrušen interním timeoutem a vždy vrací poslední známý status.

### US-WAIT-002 — Čekání na quiet period po změně
**Jako** Codex agent  
**Chci** po změně souborů poznat, že watch dotáhl rebuild a restart  
**Aby** aby mohl bezpečně otevřít UI a validovat výsledek  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- QuietSinceCursor je splněno až když od cursoru nepřibyl žádný log po zadanou quietPeriodMs.
- Pokud mezitím proběhne restart nebo build error, tool to vrátí ve výsledku.
- Tool umí kombinovat quiet period s health probe.
- Výstup obsahuje elapsed time a cursor, na kterém byla podmínka splněna.

## Epic: Build

### US-BUILD-001 — Build solution při běžící watch session
**Jako** Codex agent  
**Chci** stavět solution i když byla předtím spuštěna app session  
**Aby** aby se vyhnul lockům a ručnímu stop/start  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool solution_build má parametr whenAppRunning s variantami StopAndResume, StopOnly, Fail a ContinueIfSafe.
- Výchozí politika pro CanDoItAll je StopAndResume.
- Pokud build vyžaduje stop aktivní session, server ji korektně zastaví, provede build a podle politiky ji obnoví.
- Výsledek build operace vrátí jasně, zda došlo k resume a s jakým výsledkem.

### US-BUILD-002 — Build operation jako session
**Jako** Codex agent  
**Chci** sledovat dlouhý build přes operationId  
**Aby** aby mohl čekat, číst průběžné logy a nedržet jeden blokující call  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool solution_build vrací operationId a immediate state.
- Operation_status, operation_logs a operation_wait umožní průběžné sledování.
- Operace má vlastní log cursor a korelační ID.
- Po dokončení je dostupný exit code, duration a summary.

## Epic: Tests

### US-TEST-001 — Spuštění testů bez dotnet watch test v MVP
**Jako** Codex agent  
**Chci** spustit stabilní testy přes dotnet test  
**Aby** aby se vyhnul známým problémům watch test v .NET 10  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Tool tests_run v MVP nikdy nepoužije dotnet watch test.
- Runner je Auto a detekuje VSTest vs Microsoft.Testing.Platform podle global.json nebo konfigurace.
- Tool podporuje target project, filter, configuration, framework a collect coverage flag.
- Výsledek vrátí souhrn testů, failed count a odkaz na artefakty, pokud jsou uloženy.

### US-TEST-002 — Managed preemption i pro testy
**Jako** Codex agent  
**Chci** aby testy řešily aktivní app session stejnou politikou jako build  
**Aby** aby nedocházelo ke kolizím binárek a portů  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- tests_run respektuje whenAppRunning stejné jako solution_build.
- Při StopAndResume server po testech obnoví app session pouze pokud byla před testy aktivní a kompatibilní.
- Pokud testy selžou, resume policy stále proběhne a výsledek obsahuje jak outcome testů, tak resume outcome.
- Tool nikdy nenechá workspace v neznámém stavu bez explicitní chyby.

## Epic: Diagnostics

### US-DIAG-001 — Diagnostika start failure
**Jako** Codex agent  
**Chci** dostat strojově čitelnou diagnózu selhání startu  
**Aby** aby nemusel volně interpretovat dlouhé logy  
**Priorita:** Should  
**Fáze:** MVP

**Acceptance criteria**
- Tool diagnose_start_failure analyzuje poslední failed app session nebo operation.
- Rozpozná aspoň kategorie PortInUse, BuildFailed, MissingSdk, HealthTimeout, ProcessExitedEarly, Unknown.
- Vrátí recommendedActions seznam a citace relevantních log řádků.
- Diagnostika je čistě read-only a nespouští nové procesy.

## Epic: Recovery

### US-REC-001 — Detekce neočekávaného exit
**Jako** Codex agent  
**Chci** vědět, že app session nebo operace skončila samovolně  
**Aby** aby mohl navázat diagnostikou místo slepého čekání  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Process supervisor zachytí exit event a promítne jej do statusu ExitedUnexpectedly nebo Failed.
- Poslední exit code a lastSeenPid se uloží do session historie.
- app_wait a operation_wait se okamžitě ukončí s relevantním outcome, pokud proces skončí neočekávaně.
- Status obsahuje timestamp neočekávaného exit.

### US-OPS-001 — Cleanup stale managed processes při startu serveru
**Jako** správkyně serveru  
**Chci** po restartu MCP serveru uklidit osiřelé procesy, které dříve sám založil  
**Aby** aby nezůstaly zamčené binárky a porty  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Server udržuje registry vlastních managed procesů v .mcp-state nebo temp umístění.
- Při startu serveru s povoleným cleanup provede verifikaci, zda procesy stále běží a patří tomuto workspace.
- Stale procesy, ke kterým už neexistuje živý server context, jsou ukončeny a akce je zalogována.
- Tool cleanup_stale_processes lze spustit i ručně a vrací seznam ukončených či přeskočených procesů.

## Epic: Security

### US-SEC-001 — Ochrana proti nebezpečným cestám a příkazům
**Jako** vlastnice workspace  
**Chci** omezit server na CanDoItAll workspace a explicitně povolené projekty  
**Aby** aby MCP nástroje nespouštěly libovolné procesy mimo repozitář  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Každá vstupní cesta je normalizována na absolutní a ověřena proti povoleným rootům.
- Tooling nepřijímá raw shell command string; pouze strukturované argumenty pro dotnet CLI.
- Health probe dovoluje pouze loopback hosty, pokud není výslovně povoleno jinak.
- Logy a chyby nevrací neredigované tajné hodnoty z env nebo connection strings.

## Epic: Observability

### US-OBS-001 — Korelace logů a operací
**Jako** Codex agent  
**Chci** spojit logy, session a operace pomocí korelačních identifikátorů  
**Aby** aby přesně věděl, ke kterému běhu log patří  
**Priorita:** Should  
**Fáze:** MVP

**Acceptance criteria**
- Každá app session a operation má stabilní ID a každá log entry nese correlationId.
- Při StopAndResume se nový app start zapíše jako nový sessionVersion v rámci stejné logické session nebo jako nový session podle policy; toto chování je dokumentované a konzistentní.
- Tool responses obsahují odkazy na relevantní sessionId a operationId.
- Korelace je použitá i v interním file loggeru.

## Epic: Configuration

### US-CFG-001 — Validace konfigurace při startu serveru
**Jako** správkyně serveru  
**Chci** odhalit špatné cesty a neplatné timeouty hned při bootstrapu  
**Aby** aby se chyby neobjevily až v ostrém běhu  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Server fail-fast validuje SolutionPath, DefaultApp.ProjectPath, timeouty, log buffer a security policy.
- Chyby konfigurace se zapisují do stderr/file loggeru, ne na stdout.
- Při invalidní konfiguraci server nenaběhne do nekonzistentního stavu.
- Validace vrací akční chybovou zprávu s přesným klíčem nastavení.

## Epic: Cross Platform

### US-CROSS-001 — Cross-platform procesní ukončení
**Jako** správkyně serveru  
**Chci** aby stop a cleanup fungovaly na Windows, Linuxu i macOS  
**Aby** aby byl server použitelný lokálně i v CI/WSL  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Abstrakce IProcessTreeTerminator má implementace minimálně pro Windows a Unix-like systémy.
- Integrace test ověří, že child procesy nezůstanou běžet po stopu.
- Ukončení používá nejprve graceful signal a následně force kill.
- Rozdíly mezi platformami jsou zdokumentované v compatibility matrix.

## Epic: Codex Contract

### US-CODEX-001 — Behavior contract pro Codex
**Jako** vlastnice workflow  
**Chci** jednoznačně definovat, jak má Codex MCP server používat  
**Aby** aby neobcházel tooly a znovu nerušil watch lifecycle  
**Priorita:** Must  
**Fáze:** MVP

**Acceptance criteria**
- Balík obsahuje prompts a checklist, které výslovně zakazují používat raw dotnet run/watch/build/test mimo MCP server pro CanDoItAll.
- Contract říká, že se má používat app_wait nebo operation_wait místo klientských sleepů.
- Contract popisuje doporučený flow pro UI změnu, rebuild, test a diagnostiku.
- Contract popisuje i recovery flow pro stale procesy, timeouts a build fail.
