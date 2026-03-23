# Scope, non-goals and assumptions

## 1. In scope

### 1.1 Platform
- lokální MCP server běžící u Codexu / klienta,
- cílové servery typu Ubuntu,
- solution `CanDoItAll`,
- implementace v C# / .NET 10.

### 1.2 SSH and identity
- SSH key auth z env proměnných,
- host key pinning,
- non-interactive connect/test,
- podpora root loginu nebo neinteraktivního `sudo -n`.

### 1.3 Remote file operations
- zápis textových a menších binárních souborů,
- bundle write pro více souborů najednou,
- read/stat/backup/restore v povolených roots,
- revision backup compose artefaktů.

### 1.4 Docker operations
- audit Docker Engine a Compose pluginu,
- create/ensure networks a volumes,
- validate/apply/down/logs/ps/exec nad `docker compose`,
- detached remote jobs pro dlouhé operace.

### 1.5 Reverse proxy and TLS
- Traefik stack,
- router labels,
- dashboard hardening,
- ACME / Let's Encrypt,
- cert check a HTTPS validace.

### 1.6 Data and app services
- .NET containerized app,
- PostgreSQL v interní síti,
- IPFS Kubo v Dockeru,
- privátní IPFS swarm konfigurace.

### 1.7 Validation and rollback
- target audit,
- HTTP probe local/remote,
- PostgreSQL ready check,
- cert check,
- IPFS private validate,
- rollback poslední revize stacku.

### 1.8 Knowledge enablement
- technologické poznámky pro Codex,
- prompt pack,
- checklisty,
- QA review a remediation.

## 2. Out of scope for MVP

### 2.1 Full shell access
MVP nebude obecný vzdálený shell server.  
`dangerous_raw_exec` může existovat jako budoucí break-glass feature, ale v MVP je vypnutý.

### 2.2 Full configuration management
MVP nemá nahradit Ansible / Terraform / Pulumi.  
Bootstrapping Ubuntu je omezený na to nejnutnější:
- Docker install,
- adresáře,
- sítě,
- služby potřebné pro stack.

### 2.3 Kubernetes / Swarm / Nomad
MVP cílí na **Docker Compose na jednom hostu**.  
Docker Swarm, Kubernetes a jiné orchestrátory jsou mimo scope.

### 2.4 Full secrets platform
HashiCorp Vault, cloud secret managers nebo kompletní PKI nejsou součástí MVP.
Balík ale počítá s tím, že půjde později doplnit.

### 2.5 Full backup and disaster recovery
MVP neřeší kompletní DR proces pro PostgreSQL ani IPFS data.
Řeší jen:
- rollback compose artefaktů,
- bezpečnou manipulaci s deployment konfigurací,
- minimální provozní runbook.

### 2.6 Multi-instance Traefik HA
Traefik ACME storage v souboru není distribuovaná.  
MVP počítá se single-writer mode na jednom hostu.

### 2.7 Public IPFS mainnet participation
MVP je navržený pro **privátní IPFS swarm**.  
Veřejný mainnet bootstrap není cílový režim.

## 3. Assumptions

### A1 – Přístupové podmínky
- SSH port je dostupný,
- uživatel má buď root přístup, nebo `sudo -n`,
- key-based auth je nastavená,
- klient zná fingerprint host key nebo známý `known_hosts` záznam.

### A2 – DNS and HTTPS
- veřejné domény směřují na cílový host,
- port 80 a 443 jsou routovatelné,
- pokud se má použít HTTP challenge, port 80 nesmí být blokovaný,
- pokud se má použít wildcard nebo neveřejná doména, zvažuje se DNS challenge.

### A3 – Docker runtime
- host je kompatibilní s Docker Engine,
- Compose plugin je dostupný nebo jej lze doinstalovat,
- lokální topologie je pro MVP rootful Docker.

### A4 – App conventions
- aplikace má nebo dostane health endpoint,
- aplikace umí běžet za reverse proxy,
- konfigurace app umožní explicitně nastavit URL na PostgreSQL a Kubo RPC.

### A5 – IPFS expectations
- pokud je cílem skutečně distribuovaný privátní swarm přes více hostů,
  musí existovat bootstrap peers a odpovídající konektivita mezi uzly,
- single-node IPFS na jednom hostu je validní, ale neplní stejné cíle jako multi-node privátní síť.

## 4. Doporučená cílová topologie pro MVP

### 4.1 Single host / single edge / internal data services
- 1× Ubuntu host
- 1× Traefik stack
- 1× app stack
- interní `backend` network
- externí `proxy` network
- PostgreSQL a IPFS bez host port mappingu

Tato topologie je cílová pro první implementaci.

## 5. Deferred items

Po MVP lze doplnit:

- OpenSSH CLI backend,
- registry image publish/pull workflow,
- DNS challenge providers,
- remote tar upload velkých artefaktů,
- DB backup tools,
- Kubo cluster orchestrace,
- multi-target deployment promotion,
- CI staging smoke tests nad disposable VM.
