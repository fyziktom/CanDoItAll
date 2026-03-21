# Implementation roadmap

## Přehled fází

Roadmapa je nově rozdělená na dvě velké části:

1. **Shared foundation stream**
2. **SSH Ops stream**

Bez uzavření shared foundation streamu se SSH stream nesmí rozběhnout dál než ke scaffoldingu.

---

## Fáze 0 – Repo discovery + current-state audit

### Cíl
Pochopit solution `CanDoItAll`, ověřit skutečný stav `CanDoItAll.Mcp.DotNetWatch` a potvrdit kandidáty na extrakci.

### Deliverables
- discovery report nad solution,
- audit aktuálního `CanDoItAll.Mcp.DotNetWatch`,
- potvrzená shared candidate inventory,
- potvrzené dependency boundaries.

### Exit criteria
- existuje seznam typů a helperů k extrakci,
- existuje seznam typů, které musí zůstat server-specific,
- existuje rozhodnutí o názvech a umístění shared projektů.

---

## Fáze 1 – Create shared MCP foundation

### Cíl
Vytvořit nové projekty:

- `src/CanDoItAll.Mcp.Core`
- `src/CanDoItAll.Mcp.LocalRuntime`

### Deliverables
`CanDoItAll.Mcp.Core`:
- common contracts,
- response envelope,
- error model,
- correlation / operation / server ID helpery,
- mutation gate,
- cursorované log abstractions,
- file log persistence,
- secret redaction,
- generické async operation primitives,
- shared HTTP/TLS probe helpery.

`CanDoItAll.Mcp.LocalRuntime`:
- process supervisor,
- command runner,
- process tree terminators,
- managed process wrappers,
- stale process registry.

### Exit criteria
- oba projekty buildí,
- mají základní testy,
- neobsahují server-specific doménovou logiku,
- dependency rules jsou zdokumentované.

---

## Fáze 2 – Refactor existing CanDoItAll.Mcp.DotNetWatch

### Cíl
Přepojit existující `CanDoItAll.Mcp.DotNetWatch` na shared foundation.

### Deliverables
- `CanDoItAll.Mcp.DotNetWatch` referencuje `CanDoItAll.Mcp.Core`,
- `CanDoItAll.Mcp.DotNetWatch` referencuje `CanDoItAll.Mcp.LocalRuntime`,
- původní duplikované helpery jsou odstraněné nebo ztenčené na adaptéry,
- tool contracts zůstávají kompatibilní.

### Exit criteria
- dotnetwatch buildí,
- nevznikla regrese v tool contractu,
- startup behavior je stabilní,
- stale process cleanup a app lifecycle se nezhoršily,
- shared kód už není kopie, ale reálný zdroj pravdy.

---

## Fáze 3 – DotNetWatch regression gate

### Cíl
Zachytit regresi ještě před začátkem SSH implementace.

### Deliverables
- regression checklist report,
- contract snapshot porovnání,
- log/operation/wait smoke scénáře,
- explicitní seznam zbylých technical debt položek.

### Exit criteria
- green regression gate,
- žádný blocker v shared foundation,
- shared contracts jsou stabilní.

---

## Fáze 4 – Server skeleton for CanDoItAll.Mcp.SshOps

### Cíl
Postavit minimální SSH MCP host už na shared foundation.

### Deliverables
- `Program.cs`
- options binding
- basic logging
- references na `CanDoItAll.Mcp.Core`
- `targets_list`
- `target_test` placeholder

### Exit criteria
- server se spustí jako stdio MCP server,
- registruje tooly,
- stdout není znečištěný,
- používá shared response envelope a shared error model.

---

## Fáze 5 – SSH transport and security baseline

### Cíl
Implementovat `ISshTransport`, host key verification a secret resolution.

### Deliverables
- `SshNetTransport`
- `HostKeyVerifier`
- `SecretResolver`
- remote root path policy
- target catalog

### Exit criteria
- `target_test` funguje,
- `HostKeyMismatch` je spolehlivě detekovaný,
- private key není logovaný,
- SSH vrstva nepřináší duplicity proti shared foundation.

---

## Fáze 6 – Remote files and remote job runner

### Cíl
Vyřešit file deployment a detached operace.

### Deliverables
- `RemoteFileService`
- `fs_apply_bundle`
- `fs_read_text`
- `fs_backup_path`
- `RemoteJobRunner`
- `operation_*` tooly

### Exit criteria
- dlouhá operace vrací `operationId`,
- logy detached jobu jsou čitelné,
- MCP restart neznemožní čtení stavu.

---

## Fáze 7 – Docker and Compose core

### Cíl
Zprovoznit docker-centric orchestration.

### Deliverables
- `docker_network_ensure`
- `docker_volume_ensure`
- `compose_validate`
- `compose_apply`
- `compose_ps`
- `compose_logs`
- `compose_down`

### Exit criteria
- lze nasadit jednoduchý whoami stack,
- validace compose odhalí chybný env input,
- `compose_apply` podporuje async flow.

---

## Fáze 8 – Traefik and TLS

### Cíl
Zprovoznit shared edge router a HTTPS workflow.

### Deliverables
- `TraefikService`
- `http_probe`
- `http_wait`
- `cert_check`
- examples pro infra stack

### Exit criteria
- whoami nebo demo app běží přes Traefik,
- certifikát lze vystavit a ověřit,
- dashboard není nechráněně veřejný.

---

## Fáze 9 – PostgreSQL and IPFS

### Cíl
Doplnit core datové služby.

### Deliverables
- `postgres_ready`
- `ipfs_status`
- `ipfs_private_validate`
- app stack example s PostgreSQL a Kubo

### Exit criteria
- stack .NET + PostgreSQL + IPFS lze validovat end-to-end,
- private swarm check umí odhalit chybný bootstrap list.

---

## Fáze 10 – Rollback, validations and hardening

### Cíl
Zavřít provozní smyčku.

### Deliverables
- `stack_rollback`
- validační orchestrátor
- redaction tests
- guardrail checks
- compatibility notes

### Exit criteria
- rollback funguje,
- existují smoke testy,
- existuje runbook.

---

## Fáze 11 – QA closure

### Cíl
Provést přísný review průchod nad shared foundation i SSH serverem.

### Deliverables
- self-review report
- remediation fixes
- updated docs
- final approval note

### Exit criteria
- checklisty jsou zelené,
- známá rizika jsou explicitně zdokumentovaná,
- final pack je konzistentní.

---

## Doporučené pořadí implementace

### Stream A – shared foundation
1. audit current dotnetwatch
2. `CanDoItAll.Mcp.Core`
3. `CanDoItAll.Mcp.LocalRuntime`
4. refactor `CanDoItAll.Mcp.DotNetWatch`
5. regression gate

### Stream B – SSH Ops
1. `targets_list`
2. `target_test`
3. `target_audit`
4. `fs_apply_bundle`
5. `operation_*`
6. `compose_validate`
7. `compose_apply`
8. `compose_ps`
9. `compose_logs`
10. `http_probe`
11. `http_wait`
12. `cert_check`
13. `postgres_ready`
14. `ipfs_status`
15. `ipfs_private_validate`
16. `stack_rollback`
17. `host_bootstrap_prepare`
18. `dangerous_raw_exec` only if explicitly approved

## Povinné milestone demo scénáře

### Milestone S1
- shared core buildí
- dotnetwatch kompilačně používá shared foundation

### Milestone S2
- dotnetwatch regression gate green
- žádná zjevná duplicita common helperů nezůstala

### Milestone A
- target test
- target audit

### Milestone B
- remote file write
- async remote job
- operation logs

### Milestone C
- whoami deploy přes compose
- `compose_ps`
- `http_wait`

### Milestone D
- Traefik + HTTPS

### Milestone E
- full app stack .NET + PostgreSQL + IPFS

### Milestone F
- rollback a failure injection

## Zakázané zkratky během implementace

- přeskočit shared foundation a rovnou stavět SSH tools,
- kopírovat helpery z dotnetwatch do SSH projektu,
- implementovat raw exec dřív než domain tools,
- používat shell stringy z klienta,
- přeskakovat host key verification,
- implementovat deploy bez detached job runneru,
- považovat běžící container za healthy bez explicitní validace,
- vystavit Kubo API veřejně „jen pro test“.

## Doporučené branch / PR chunks

- PR0 current-state audit + shared inventory
- PR1 `CanDoItAll.Mcp.Core`
- PR2 `CanDoItAll.Mcp.LocalRuntime`
- PR3 refactor `CanDoItAll.Mcp.DotNetWatch`
- PR4 dotnetwatch regression closure
- PR5 SSH skeleton + targets
- PR6 ssh transport + host verification
- PR7 remote files + jobs
- PR8 docker/compose
- PR9 traefik/tls
- PR10 postgres/ipfs
- PR11 rollback/validation/hardening
- PR12 docs/examples/final QA
