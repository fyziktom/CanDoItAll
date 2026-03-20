# Codex usage checklist

Tento checklist je určený přímo pro agenta, který bude server implementovat nebo používat.

## Před prací
- [ ] Přečetla jsem `00-START-HERE.md`.
- [ ] Přečetla jsem `06-tool-contracts.md`.
- [ ] Přečetla jsem `09-implementation-roadmap.md`.
- [ ] Přečetla jsem relevantní prompt pro aktuální fázi.

## Při implementaci
- [ ] Nepoužívám raw `dotnet watch`, `dotnet run`, `dotnet build` ani `dotnet test` mimo MCP server pro CanDoItAll workflow.
- [ ] Nezavádím logiku založenou na klientském `sleep`.
- [ ] Všechny komentáře v kódu píšu anglicky.
- [ ] Změny držím po malých, review-friendly krocích.
- [ ] Po každém kroku buildím a testuji.

## Při používání hotového serveru
- [ ] Nejdřív volám `candoitall_workspace_info`.
- [ ] Pro běh aplikace používám `candoitall_app_start`.
- [ ] Po změně souborů používám `candoitall_app_wait`, ne odhadované pauzy.
- [ ] Pro build používám `candoitall_solution_build`.
- [ ] Pro testy používám `candoitall_tests_run`.
- [ ] Při chybě čtu `app_logs`/`operation_logs` a teprve pak volám diagnózu.

## Při selhání
- [ ] Nezabíjím procesy bokem, pokud lze použít `app_stop` nebo `cleanup_stale_processes`.
- [ ] Nepřekračuji bezpečnostní hranice workspace.
- [ ] Při conflict outcome měním workflow, neobcházím server.
- [ ] Když čekání timeoutuje, zkontroluji status a diagnostiku.
