# Ops runbook

## 1. Purpose
Tento runbook popisuje, jak server používat, provozovat a troubleshootovat v lokálním CanDoItAll workflow.

## 2. Preconditions
- kompatibilní `.NET 10` SDK
- platný checkout solution CanDoItAll
- nastavený server settings file
- pokud používáš HTTPS health, důvěryhodný localhost development certifikát

## 3. Bootstrap checklist
1. Zkontroluj `CanDoItAll.Mcp.DotNetWatch.settings.json`.
2. Potvrď `WorkspaceRoot` a `SolutionPath`.
3. Potvrď `DefaultApp.ProjectPath`.
4. Potvrď `Health.Urls`.
5. Ujisti se, že `.mcp-state` a test/report složky jsou excluded z watch.
6. Spusť server přes MCP klienta nebo lokálně.

## 4. Standard workflow for Codex
1. `candoitall_workspace_info`
2. `candoitall_app_start`
3. `candoitall_app_wait(condition=Healthy)`
4. edit code
5. `candoitall_app_wait(condition=QuietSinceCursor)`
6. `candoitall_app_wait(condition=Healthy)`
7. UI validation
8. `candoitall_solution_build` or `candoitall_tests_run` when needed

## 5. Log locations
Doporučené:
- in-memory logs přes MCP tools
- persisted logs v `.mcp-state/logs`
- stale registry v `.mcp-state/process-registry.json`

## 6. Common troubleshooting

### 6.1 App did not become healthy
Kroky:
1. `candoitall_app_status`
2. `candoitall_app_logs`
3. `candoitall_diagnose_start_failure`
4. ověř `Health.Urls`
5. ověř, že app opravdu vystavuje health endpoint

### 6.2 Port already in use
Kroky:
1. `candoitall_app_status`
2. `candoitall_cleanup_stale_processes`
3. znovu `candoitall_app_start`
4. pokud konflikt přetrvává, identifikuj externí proces a změň porty nebo prostředí

### 6.3 Build or tests hang / conflict
Kroky:
1. ověř, že se používá `whenAppRunning=StopAndResume`
2. `candoitall_operation_status`
3. `candoitall_operation_logs`
4. pokud běží stará session mimo server, proveď cleanup a opakuj workflow přes server

### 6.4 Watch does not react to file changes
Kroky:
1. ověř, že se mění watched soubory
2. ověř watch exclusions
3. v problémovém prostředí zvaž `DOTNET_USE_POLLING_FILE_WATCHER=1`
4. zkontroluj logy session

### 6.5 After crash, ports remain occupied
Kroky:
1. restartuj server
2. nechej proběhnout bootstrap cleanup
3. případně ručně zavolej `candoitall_cleanup_stale_processes`
4. znovu spusť app session

## 7. Recovery after MCP server crash
Očekávané chování:
- po restartu server načte registry
- identifikuje stale managed procesy
- bezpečně je ukončí
- zaloguje cleanup outcome

Pokud se cleanup nezdaří:
- zkontroluj oprávnění a ownership metadata
- nespouštěj hned nový watch session bez zjištění, co běží

## 8. Maintenance
- periodicky smaž staré log soubory dle retenční politiky
- drž tool contracts a prompts v souladu s implementací
- po větší změně CanDoItAll app struktury re-run repo discovery

## 9. What not to do
- nespouštěj CanDoItAll app bokem přes raw `dotnet watch` v běžném workflow
- nepoužívej klientské sleepy místo wait tools
- nezabíjej procesy ručně, pokud existuje managed cleanup cesta
