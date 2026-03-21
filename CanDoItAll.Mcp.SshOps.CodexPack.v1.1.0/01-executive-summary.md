# Executive summary

## 1. Co se má nově postavit

Výsledkem nemá být jen projekt `CanDoItAll.Mcp.SshOps`.  
Výsledkem má být **dvoukrokové rozšíření MCP architektury solution `CanDoItAll`**:

1. zavést shared foundation pro MCP servery,
2. na této foundation implementovat `CanDoItAll.Mcp.SshOps`.

## 2. Nejzásadnější zjištění z analýzy

### 2.1 Aktuální `CanDoItAll.Mcp.DotNetWatch` už je víc než „částečná implementace“
Z aktuálního stavu repozitáře plyne, že `CanDoItAll.Mcp.DotNetWatch` je už **substantially implemented**, nikoli jen lehký spike.

Byly nalezeny zejména tyto již existující bloky:

- stdio MCP host s čistým `Program.cs`,
- options binding a startup validation,
- tool surface s 13 veřejnými MCP tooly,
- `ToolEnvelope<T>`, `ToolError`, `ToolInvocationException`,
- `RingLogBuffer`, `FileLogStore`, `LogRedactor`,
- `OperationRegistry`,
- `WorkspaceExecutionLock`,
- `ServerInstanceIdentity`,
- `HttpHealthProbe`,
- `PathGuard`, `EnvironmentOverlayFilter`,
- `ProcessSupervisor`, `ManagedProcess`, `IProcessTreeTerminator`,
- `StaleProcessRegistry`,
- `SessionCoordinator` a `AppRuntimeManager`.

Jinými slovy: SSH server by bez refaktoru téměř jistě duplikoval velkou část základních helperů.

### 2.2 SSH návrh už od začátku obsahuje stejné koncepty
Původní SSH balík počítá s těmito koncepty:

- common response envelope a error model,
- `PathGuard`,
- `SecretRedactor`,
- `OperationWaitEngine`,
- `HttpProbeService`,
- `OperationJournal`,
- locking a detached operations,
- structured observability.

To nejsou izolované detaily. To jsou přesně oblasti, kde už `CanDoItAll.Mcp.DotNetWatch` něco obdobného má.

## 3. Architektonický závěr

Nejdřív se musí vytvořit tyto shared knihovny:

### 3.1 `CanDoItAll.Mcp.Core`
Povinná foundation pro všechny MCP servery.

Obsahuje:

- common contracts,
- response envelope,
- error model,
- correlation / operation / server identity helpery,
- mutation gate,
- log abstractions a cursorované čtení,
- file-backed log persistence,
- secret redaction,
- generické async operation primitives,
- common HTTP/TLS probe helpery.

### 3.2 `CanDoItAll.Mcp.LocalRuntime`
Volitelná shared knihovna pro všechny MCP servery, které řídí lokální child processy.

Obsahuje:

- process supervisor,
- process command runner,
- tree terminators,
- managed process wrappers,
- stale process registry,
- ownership markers a helpery.

`CanDoItAll.Mcp.DotNetWatch` ji bude používat hned.  
`CanDoItAll.Mcp.SshOps` ji používat nemusí.

## 4. Co zůstane server-specific

### 4.1 Pouze v `CanDoItAll.Mcp.DotNetWatch`
- `AppSession`, `AppRuntimeManager`, `SessionCoordinator`,
- `dotnet watch` command building,
- test runner detection,
- dotnet-specific start diagnostics.

### 4.2 Pouze v `CanDoItAll.Mcp.SshOps`
- `ISshTransport`, `SshNetTransport`,
- `HostKeyVerifier`,
- `TargetCatalog`,
- `RemoteJobRunner`,
- `RevisionBackupService`,
- Docker / Traefik / PostgreSQL / IPFS doménové služby.

## 5. Co se má stát dřív než vznikne první SSH tool

Před `targets_list` a `target_test` musí být splněno:

1. shared foundation projekty existují,
2. `CanDoItAll.Mcp.DotNetWatch` je na ně napojené,
3. dotnetwatch nemá regresi v tool contractu, logice ani startup behavior,
4. shared boundary rules jsou zdokumentované a zkontrolované.

## 6. Co tahle revize balíku dává navíc

Tato revize doplňuje:

- inventory všech sdílitelných typů a helperů,
- extrakční matici current file → target shared project,
- migrační plán pro existující `CanDoItAll.Mcp.DotNetWatch`,
- upravenou roadmapu a backlog,
- nové prompty pro Codex,
- samostatný QA gate pro shared foundation.

## 7. Doporučená implementační mantra

> Nejprve sdílej stabilní primitives.  
> Neduplikuj observability, errors, waits a locks.  
> Netahej do shared vrstvy doménovou logiku příliš brzy.  
> Než začneš stavět SSH tools, dokaž regresně, že `CanDoItAll.Mcp.DotNetWatch` stále funguje.
