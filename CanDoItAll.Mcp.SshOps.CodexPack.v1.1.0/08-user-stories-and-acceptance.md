# User stories and acceptance criteria

## Epic A – Connectivity and identity

### US-A1
**Jako Codex** chci ověřit, že se na target připojím přes SSH klíč a že znám jeho identitu,  
**abych** neprovedla mutaci na podvrženém nebo špatném hostu.

**Acceptance**
- `target_test` selže při host key mismatch.
- `target_test` selže při chybějícím nebo nevalidním private key.
- response obsahuje fingerprint summary a remote user.
- neproběhne žádná mutace hostu.

### US-A2
**Jako Codex** chci audit připravenosti hostu,  
**abych** před deployem viděla, jestli je tam Docker, compose plugin, volné porty a sudo režim.

**Acceptance**
- `target_audit` vrátí OS, docker, compose, sudo a port summary.
- výstup obsahuje warnings i blockers.
- audit nerozbije existující stack.

## Epic B – Host bootstrap

### US-B1
**Jako operátorka** chci umět připravit čistý Ubuntu host pro Docker-based deployment,  
**abych** nemusela ručně přednastavovat každý server.

**Acceptance**
- `host_bootstrap_prepare` umí nainstalovat Docker z oficiálního repo, pokud je to povolené.
- umí vytvořit base directories a `proxy` network.
- výsledkem je stav validovatelný přes `target_audit`.
- při chybě je k dispozici `operation_logs`.

### US-B2
**Jako QA** chci, aby bootstrap nevyžadoval interaktivní `sudo`,  
**aby** automation neselhala na password promptu.

**Acceptance**
- pokud `sudo -n` není dostupné, tool vrátí `SudoRequired`.
- žádný tool nikdy nečeká na interaktivní vstup.

## Epic C – Remote files and revisions

### US-C1
**Jako Codex** chci nahrát více deployment souborů najednou,  
**abych** mohla zapsat celý compose bundle v jednom kroku.

**Acceptance**
- `fs_apply_bundle` umí zapsat víc souborů.
- všechny cesty jsou validované proti `allowedRoots`.
- tool umí vytvořit backup před přepsáním.

### US-C2
**Jako operátorka** chci umět obnovit předchozí verzi deployment souborů,  
**abych** měla rychlou cestu k návratu po chybě.

**Acceptance**
- existuje explicitní backup identifikátor nebo revision ID.
- `fs_restore_backup` nebo `stack_rollback` obnoví známou verzi.
- po rollbacku lze provést validační probe.

## Epic D – Docker and Compose

### US-D1
**Jako Codex** chci validovat compose konfiguraci před nasazením,  
**abych** nechytala YAML a env chyby až při `up`.

**Acceptance**
- `compose_validate` vrátí `valid=true/false`.
- při nevalidní konfiguraci se deployment neprovádí.
- diagnostics obsahují stderr summary.

### US-D2
**Jako Codex** chci spustit stack a bezpečně čekat na výsledek,  
**abych** nepoužívala blind sleep.

**Acceptance**
- `compose_apply` může vrátit detached `operationId`.
- `operation_wait` dovede zjistit success/fail.
- `compose_ps` po apply vrací stav služeb.

### US-D3
**Jako operátorka** chci mít přístup ke stack logům,  
**abych** mohla rychle diagnostikovat failure.

**Acceptance**
- `compose_logs` vrací logy služby nebo celého stacku.
- logy jsou redigované.
- logy jsou dostupné i po async operaci.

### US-D4
**Jako Codex** chci umět rollbacknout stack na poslední známou dobrou revizi,  
**abych** rychle opravila neúspěšný deploy.

**Acceptance**
- `stack_rollback` najde vhodnou revizi.
- vrací `operationId`.
- po rollbacku lze spustit validační sekvenci.

## Epic E – Traefik and TLS

### US-E1
**Jako Codex** chci nasadit Traefik jako sdílený edge router,  
**aby** všechny veřejné služby šly přes jednotnou proxy vrstvu.

**Acceptance**
- infra stack obsahuje Traefik s `exposedByDefault=false`.
- dashboard není veřejně otevřený bez ochrany.
- Traefik je připojený do `proxy` network.

### US-E2
**Jako Codex** chci ověřit, že veřejná doména routuje přes Traefik správně,  
**abych** odlišila síťový problém od problému v appce.

**Acceptance**
- `http_probe` nebo `http_wait` umí běžet z `origin=local`.
- response vrací status code, duration a TLS summary.

### US-E3
**Jako operátorka** chci potvrdit, že certifikát je skutečně vydaný,  
**abych** věděla, že deploy neskončil na self-signed fallbacku.

**Acceptance**
- `cert_check` vrací issuer a expiry.
- při chybě challenge nebo rate limitu vrací srozumitelnou diagnostiku.
- staging/prod rozdíl je popsán v docs a examples.

## Epic F – PostgreSQL

### US-F1
**Jako Codex** chci ověřit, že PostgreSQL je ready,  
**abych** nevyhodnotila běžící container jako plně připravenou DB.

**Acceptance**
- `postgres_ready` používá `pg_isready` nebo ekvivalent.
- respektuje timeout.
- vrací ready/fail a diagnostiku.

### US-F2
**Jako QA** chci, aby PostgreSQL nebyla veřejně mapovaná ven,  
**aby** se minimalizovala chyba v síťové expozici.

**Acceptance**
- validační dokumenty obsahují explicitní zákaz public port mappingu DB.
- examples používají interní network pattern.

## Epic G – IPFS Kubo private swarm

### US-G1
**Jako Codex** chci provozovat IPFS přes Kubo v Dockeru,  
**aby** aplikace používala jeden dlouho běžící node místo embedded instancí.

**Acceptance**
- docs a examples používají Kubo container.
- `ipfs_status` vrací peer ID a API readiness.

### US-G2
**Jako operátorka** chci, aby IPFS běžel v privátním swarm režimu,  
**aby** nebyl omylem napojen na veřejný bootstrap list.

**Acceptance**
- `ipfs_private_validate` ověří `swarm.key`.
- odhalí veřejné bootstrap peers.
- vrací explicitní varování při rozporu.

### US-G3
**Jako QA** chci, aby Kubo RPC API nebylo veřejně vystavené,  
**aby** nikdo z internetu neměl admin-level přístup.

**Acceptance**
- examples nepublikují port 5001 do internetu.
- validace dokáže upozornit na veřejný mapping.

## Epic H – Long-running operations and resilience

### US-H1
**Jako Codex** chci polling a logy pro dlouhé operace,  
**abych** bezpečně přečkala dlouhý build, pull nebo restart.

**Acceptance**
- detached operace mají `operationId`.
- `operation_status`, `operation_wait`, `operation_logs` fungují i po restartu MCP serveru.

### US-H2
**Jako operátorka** chci umět operaci zrušit,  
**abych** zastavila zjevně chybný deploy.

**Acceptance**
- `operation_cancel` pošle TERM a pak KILL.
- výsledek je auditovaný.

## Epic I – Governance and safety

### US-I1
**Jako vlastnice systému** chci, aby Codex nepoužíval přímé SSH mimo MCP,  
**aby** existoval jednotný audit trail.

**Acceptance**
- prompt pack to explicitně přikazuje.
- checklisty a review to kontrolují.
- návrh raw exec je defaultně vypnutý.

### US-I2
**Jako security reviewer** chci redakci citlivých hodnot v logách a odpovědích,  
**aby** deployment automation neukládala secret hodnoty do historie.

**Acceptance**
- secret patterny jsou konfigurovatelné,
- testy pokrývají redaction,
- examples neobsahují reálné tajné hodnoty.
