> **Revize 1.1.0 – shared foundation addendum**  
> Tato architektura nově předpokládá, že před implementací `CanDoItAll.Mcp.SshOps` vznikne shared vrstva `CanDoItAll.Mcp.Core` a `CanDoItAll.Mcp.LocalRuntime`.  
> Detailní návrh, dependency rules a migrační plán jsou v `20-shared-foundation/*`.  
> Všechny části, které jsou stabilní cross-server primitives, se mají extrahovat dřív, než začne implementace SSH toolů.

# Architektura

## 1. Přehled

Cílová architektura je **remote operations orchestration server** pro solution CanDoItAll.  
Není to generický SSH terminál. Je to řízený orchestrátor vzdáleného prostředí s vlastními:

- tool contracts,
- locky,
- validační vrstvou,
- remote job runnerem,
- rollback mechanikou,
- redakcí tajných hodnot,
- remote state a revision historií.

## 2. Vysoká úroveň

```mermaid
flowchart TD
    Client[Codex / MCP client]
    Host[MCP Host<br/>stdio transport]
    Tools[Tool layer]
    Coordinator[TargetCoordinator]
    Locks[TargetLockManager]
    Config[TargetCatalog + SecretResolver]
    Policy[PathGuard + CommandPolicy + HostKeyVerifier]
    Domain[Docker/Traefik/Postgres/IPFS Services]
    Waits[WaitEngine + ProbeEngine]
    Ops[OperationJournal]
    SSH[ISshTransport]
    RemoteHost[Ubuntu target]
    RemoteState[/opt/candoitall/.mcp-state]
    RemoteDocker[Docker Engine + Compose]
    RemoteTraefik[Traefik stack]
    RemoteApp[App stack]
    Logs[stderr/file logs]

    Client --> Host --> Tools
    Tools --> Config
    Tools --> Policy
    Tools --> Coordinator
    Coordinator --> Locks
    Coordinator --> Domain
    Coordinator --> Waits
    Coordinator --> Ops
    Domain --> SSH
    Waits --> SSH
    SSH --> RemoteHost
    RemoteHost --> RemoteState
    RemoteHost --> RemoteDocker
    RemoteDocker --> RemoteTraefik
    RemoteDocker --> RemoteApp
    Host --> Logs
    Ops --> Logs
```

## 3. Hlavní vrstvy

### 3.1 MCP host vrstva
Odpovídá za:
- stdio transport,
- registraci toolů,
- dependency injection,
- bootstrap konfigurace,
- logování do stderr a případně do souboru.

Zásady:
- minimální logika v `Program.cs`,
- stdout pouze pro MCP protokol,
- žádné business rozhodování v host vrstvě.

### 3.2 Configuration and target catalog
`TargetCatalog` načítá definice targetů:

- hostname / IP,
- SSH port,
- remote user,
- sudo policy,
- host key verification,
- allow-listed remote roots,
- paths na stacks / state / secrets,
- výchozí timeouty,
- docker a traefik konvence.

`SecretResolver` řeší tajné hodnoty výhradně z env proměnných nebo z explicitně povolených secret souborů na remote hostu.

### 3.3 Security and policy layer
Obsahuje:
- `HostKeyVerifier`
- `PathGuard`
- `CommandPolicy`
- `SecretRedactor`

#### HostKeyVerifier
- porovnává fingerprint nebo known_hosts entry,
- odmítá spojení při mismatch,
- loguje bezpečné diagnostické info bez úniku citlivých hodnot.

#### PathGuard
- dovolí zápis a čtení jen pod allow-listed roots,
- hlídá normalizaci cest,
- zakazuje traversal a symlink bypass.

#### CommandPolicy
- nepřijímá volné shell stringy od klienta,
- skládá povolené příkazy ze strukturovaných DTO,
- centralizuje whitelist parametrů.

#### SecretRedactor
- rediguje:
  - private key content,
  - swarm key,
  - DB hesla,
  - DNS provider tokeny,
  - auth headery.

### 3.4 Tool layer
Tool layer je veřejné API serveru pro Codex.  
Každý tool:

- validuje input DTO,
- předává řízení koordinátoru,
- vrací strojově čitelnou response envelope,
- nikdy neobchází policy vrstvu.

Každý mutující tool musí vracet:
- `correlationId`,
- `target`,
- `kind`,
- `status`,
- případně `operationId`,
- srozumitelný `summary`,
- doporučené next steps.

### 3.5 Coordination layer
`TargetCoordinator` je centrální orchestrátor.

Řeší:
- serializaci mutujících operací per target,
- jemnější stack-level locky pro traefik/app stacky,
- rozhodnutí sync vs async execution,
- audit trail,
- navázání validačních kroků po deployi,
- rollback orchestrace.

#### Locking pravidla
- read-only operace mohou běžet paralelně,
- mutující operace se serializují per target,
- shared infra stack (Traefik) má samostatný lock,
- app stack deploy nesmí současně rollbackovat stejný stack.

### 3.6 SSH transport abstraction
Rozhraní `ISshTransport` poskytuje:

- `ExecuteAsync`
- `UploadAsync`
- `DownloadAsync`
- `ReadTextAsync`
- `WriteTextAsync`
- `EnsureDirectoryAsync`
- `StatAsync`
- `DeleteAsync`
- `GetHostFingerprintAsync`

MVP implementace:
- `SshNetTransport`

Budoucí rozšíření:
- `OpenSshCliTransport`

Důvod abstrakce:
- testovatelnost,
- možnost fallback backendu,
- oddělení MCP logiky od SSH detailů.

### 3.7 Remote command execution modes

#### Inline mode
Použije se pro:
- rychlé audity,
- read-only příkazy,
- `docker compose ps`,
- `docker network inspect`,
- malé write operace.

Výhody:
- jednoduchý request-response flow,
- menší overhead.

#### Detached remote job mode
Použije se pro:
- `docker compose up -d` s build/pull,
- bootstrap hostu,
- delší restart stacku,
- některé rollbacky,
- čekání na certifikační proces.

Remote job wrapper na hostu vytvoří adresář:

```text
/opt/candoitall/.mcp-state/jobs/<operationId>/
```

Obsah job dir:
- `request.json`
- `command.txt`
- `status.json`
- `stdout.log`
- `stderr.log`
- `pid`
- `started-at.txt`
- `ended-at.txt`
- `exit-code.txt`

Výhody:
- lze se znovu připojit po restartu MCP serveru,
- logy a exit status žijí nezávisle na jednom SSH session,
- Codex může bezpečně pollovat stav.

### 3.8 Remote file deployment model

#### Recommended pattern
- zapisovat stack artefakty do revisioned struktury,
- aktualizovat „current“ symlink nebo kopii až po validaci,
- zálohovat původní verzi před přepisem.

Příklad:
```text
/opt/candoitall/stacks/myapp/
  revisions/
    20260320T181500Z/
    20260320T190240Z/
  current/
    docker-compose.yml
    .env
    traefik/
```

Pro MVP je akceptovatelné i jednodušší:
- write to current,
- backup do `.mcp-state/backups/<stack>/<timestamp>/`.

### 3.9 Domain services

#### TargetAuditService
- SSH test,
- `uname`, `lsb_release`, disk, paměť,
- `sudo -n true`,
- docker a compose verze,
- port occupancy,
- přítomnost base dirs.

#### RemoteFileService
- bundle write,
- backup,
- read/stat,
- restore poslední zálohy.

#### DockerService
- ensure network,
- ensure volume,
- docker info / version.

#### DockerComposeService
- validate,
- apply,
- ps,
- logs,
- down,
- exec,
- rollback.

#### TraefikService
- deploy infra stack,
- dashboard validation,
- router visibility,
- middleware expectations,
- ACME readiness.

#### CertificateService
- DNS preflight hints,
- cert check,
- HTTPS probe,
- staging vs production safety.

#### PostgresService
- container health,
- `pg_isready`,
- connection diagnostics,
- optional future SQL smoke.

#### IpfsService
- Kubo status,
- check for `swarm.key`,
- check bootstrap list,
- peer count,
- RPC reachability only from internal origin,
- guard against public API exposure.

#### ValidationOrchestrator
Skládá vyšší úroveň kontrol:
- target ready,
- stack ready,
- public HTTPS ready,
- internal dependency ready.

### 3.10 Wait and probe engine
`WaitEngine` a `ProbeEngine` zajišťují:

- polling remote job stavu,
- polling compose health,
- local HTTP probe,
- remote HTTP probe přes `curl`,
- certificate probe,
- quiet-period waits nad logy.

Důležité:
- klient nemá používat `sleep`,
- čekání je server-side a korelované.

### 3.11 Persistence and observability
Lokální perzistence:
- operation journal,
- server logs,
- volitelně cache posledních response snapshots.

Remote perzistence:
- detached job state,
- revision backups,
- diagnostické artefakty.

Observabilita:
- correlation ID,
- operation ID,
- target name,
- stack name,
- sanitized stderr/file logs.

## 4. Doporučené rozdělení projektu

```text
src/
  CanDoItAll.Mcp.SshOps/
    Hosting/
    Configuration/
    Models/
    Tools/
    Coordination/
    Security/
    Transport/
    RemoteJobs/
    Domain/
      Targets/
      Files/
      Docker/
      Traefik/
      Certificates/
      Postgres/
      Ipfs/
      Validation/
    Observability/
    Utilities/
tests/
  CanDoItAll.Mcp.SshOps.Tests/
  CanDoItAll.Mcp.SshOps.IntegrationTests/
```

## 5. Lokální vs vzdálená validace

Architektura úmyslně podporuje dva originy validace:

### Local origin
Použití:
- veřejná doména a HTTPS z pohledu klienta,
- cert chain validace,
- DNS resolution sanity check.

### Remote origin
Použití:
- interní backend endpointy,
- `http://app:8080/health`,
- `http://ipfs:5001/api/v0/version`,
- `pg_isready` v compose síti,
- situace, kdy lokální prostředí nevidí interní endpoint.

## 6. Anti-patterns, které architektura výslovně odmítá

- přímý shell passthrough jako primární API,
- host key acceptance „na první dobrou“ bez pinu v produkci,
- zapisování secret hodnot do response payloadu,
- veřejné vystavení PostgreSQL,
- veřejné vystavení Kubo RPC API,
- `docker compose up` bez předchozí validace a následného wait/probe kroku,
- rollback bez znalosti předchozí revize,
- klientské blind sleep místo server-side wait toolů.
