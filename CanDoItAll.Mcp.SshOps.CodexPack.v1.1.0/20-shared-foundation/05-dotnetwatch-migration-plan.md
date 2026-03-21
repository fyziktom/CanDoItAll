# DotNetWatch migration plan

## Cíl

Migrovat existující `CanDoItAll.Mcp.DotNetWatch` na shared foundation tak, aby:

- zůstaly stejné tool names,
- nevznikla regrese v app lifecycle flow,
- nevznikla regrese v build/test operation flow,
- zůstala zachovaná stdout discipline,
- stale process cleanup byl dál bezpečný.

## Doporučené pořadí kroků

### Krok 1 – vytvoř shared projekty
- vytvoř `CanDoItAll.Mcp.Core`
- vytvoř `CanDoItAll.Mcp.LocalRuntime`

### Krok 2 – přesuň čisté common contracts
Přesuň:
- `ToolEnvelope<T>`
- `ToolError`
- `ToolInvocationException`
- `ServerInstanceIdentity`
- mutation gate

### Krok 3 – přesuň logging primitives
Přesuň:
- `LogEntry`
- `LogReadResult`
- `RingLogBuffer`
- `FileLogStore`
- `LogRedactor` → sjednoť jako `SecretRedactor`

### Krok 4 – přesuň local process runtime
Přesuň:
- process runner
- process supervisor
- process tree terminators
- ownership markers
- stale process registry

### Krok 5 – zaveď shared operation primitives
Nedělej big-bang přepis celé dotnetwatch operation domény.
Udělěj pouze to, co je stabilní a generické:
- base operation states,
- common registry/wait abstractions.

### Krok 6 – ponech dotnet-specific orchestration lokálně
Nech lokálně:
- `AppRuntimeManager`
- `AppSession`
- `SessionCoordinator`
- start diagnoser
- runner detection

### Krok 7 – proveď regression gate
Povinné smoke flows:
- `candoitall_workspace_info`
- `candoitall_app_start`
- `candoitall_app_wait`
- `candoitall_app_logs`
- `candoitall_app_stop`
- `candoitall_solution_build`
- `candoitall_operation_status`
- `candoitall_operation_wait`
- `candoitall_operation_logs`
- `candoitall_cleanup_stale_processes`

## Důležité zásady

### 1. Žádný big-bang rename tool contracts
Externí JSON kontrakt dotnetwatch serveru se má změnit maximálně kompatibilně.

### 2. Nejprve přesunout, potom čistit
Když přesuneš common typ do shared knihovny, nejprve zapoj nové reference, teprve potom maž původní duplicitu.

### 3. Keep adapters thin
Pokud po přesunu zbude v dotnetwatch tenká vrstva adaptérů, je to v první vlně v pořádku.

### 4. Nemigruj zároveň SSH server
V jedné změně nemíchej:
- extrakci shared foundation,
- velkou dotnetwatch refaktoraci,
- plnou implementaci SSH serveru.

## Exit gate

Do SSH serveru se smí pokračovat až když platí:

- shared foundation existuje,
- dotnetwatch buildí,
- regression checklist je zelený,
- duplicity common helperů jsou odstraněné nebo vědomě zúžené na adapter layer.
