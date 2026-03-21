> **Revize 1.1.0 – shared contract rule**  
> Obecná response envelope z této kapitoly má být implementovaná ve shared projektu `CanDoItAll.Mcp.Core.Contracts`.  
> Současně se má stejná envelope nebo její kompatibilní superset backportovat i do `CanDoItAll.Mcp.DotNetWatch`, aby oba servery používaly jeden společný wire-level pattern.

# Tool contracts

## 1. Obecná response envelope

Všechny tooly vrací strukturu v tomto duchu:

```json
{
  "ok": true,
  "target": "prod-eu1",
  "tool": "compose_apply",
  "correlationId": "c_01JQ...",
  "operationId": "op_01JQ...",
  "status": "accepted",
  "summary": "Compose apply started in detached mode.",
  "data": {},
  "diagnostics": [],
  "nextSuggestedTools": [
    "operation_wait",
    "compose_ps",
    "http_wait"
  ]
}
```

### Povinné vlastnosti
- `ok`
- `tool`
- `correlationId`
- `status`
- `summary`

### Volitelné vlastnosti
- `target`
- `operationId`
- `data`
- `diagnostics`
- `nextSuggestedTools`

## 2. Standardní error codes

- `TargetNotFound`
- `TargetNotConfigured`
- `HostKeyMismatch`
- `AuthenticationFailed`
- `SudoRequired`
- `DockerNotInstalled`
- `ComposePluginMissing`
- `PathNotAllowed`
- `RemotePathMissing`
- `OperationBusy`
- `OperationNotFound`
- `ValidationFailed`
- `Timeout`
- `CertificateNotReady`
- `RateLimitLikely`
- `IpfsPublicBootstrapDetected`
- `IpfsSwarmKeyMissing`
- `RollbackRevisionNotFound`

## 3. Tool groups

### A. Target and connectivity
1. `targets_list`
2. `target_test`
3. `target_audit`
4. `host_bootstrap_prepare`

### B. Files and revisions
5. `fs_apply_bundle`
6. `fs_read_text`
7. `fs_backup_path`
8. `fs_restore_backup`

### C. Docker and Compose
9. `docker_network_ensure`
10. `docker_volume_ensure`
11. `compose_validate`
12. `compose_apply`
13. `compose_ps`
14. `compose_logs`
15. `compose_exec`
16. `compose_down`
17. `stack_rollback`

### D. Validation
18. `http_probe`
19. `http_wait`
20. `cert_check`
21. `postgres_ready`
22. `ipfs_status`
23. `ipfs_private_validate`

### E. Long-running operations
24. `operation_status`
25. `operation_wait`
26. `operation_logs`
27. `operation_cancel`

### F. Break-glass
28. `dangerous_raw_exec` (disabled by default)

---

## 4. targets_list

### Purpose
Vrátí seznam nakonfigurovaných targetů a jejich capability summary.

### Request
```json
{}
```

### Response data
```json
{
  "targets": [
    {
      "name": "prod-eu1",
      "host": "prod-eu1.example.com",
      "useSudo": true,
      "allowedRoots": ["/opt/candoitall", "/etc/traefik"],
      "capabilities": {
        "bootstrap": true,
        "compose": true,
        "rollback": true,
        "rawExec": false
      }
    }
  ]
}
```

---

## 5. target_test

### Purpose
Ověří SSH konektivitu a identitu hostu bez změny stavu hostu.

### Request
```json
{
  "target": "prod-eu1"
}
```

### Semantics
- otevře SSH spojení,
- ověří host key,
- ověří key-based auth,
- přečte základní banner a user identity.

### Response data
```json
{
  "verified": true,
  "remoteUser": "deploy",
  "fingerprintSha256": "SHA256:...",
  "authenticationMethod": "publickey"
}
```

---

## 6. target_audit

### Purpose
Přečte připravenost hostu pro deployment.

### Request
```json
{
  "target": "prod-eu1",
  "includePorts": true,
  "includeDocker": true,
  "includeDisk": true
}
```

### Audit obsahuje
- OS a kernel
- sudo non-interactive readiness
- docker version
- compose plugin version
- aktivní porty 80/443/22/4001
- volné místo
- přítomnost base directories
- existenci `proxy` network
- dostupnost `curl`, `tar`, `bash`

### Response data
```json
{
  "os": {
    "distribution": "Ubuntu",
    "version": "24.04"
  },
  "sudo": {
    "available": true,
    "mode": "sudo -n"
  },
  "docker": {
    "installed": true,
    "version": "29.2.1",
    "composeVersion": "v2.38.0"
  },
  "ports": [
    {"port": 80, "occupied": false},
    {"port": 443, "occupied": false},
    {"port": 4001, "occupied": false}
  ],
  "warnings": []
}
```

---

## 7. host_bootstrap_prepare

### Purpose
Volitelně připraví čistý nebo částečně připravený Ubuntu host.

### Request
```json
{
  "target": "prod-eu1",
  "mode": "docker-and-layout",
  "installDockerFromOfficialRepo": true,
  "createBaseDirectories": true,
  "createProxyNetwork": true,
  "enableDockerOnBoot": true,
  "executionMode": "auto"
}
```

### Notes
- vyžaduje root nebo `sudo -n`,
- používá detached mode, pokud je operace delší,
- nesmí přidávat uživatele do `docker` group bez explicitního povolení v settings.

### Response
- většinou `accepted` s `operationId`,
- po dokončení má být doporučeno spustit `target_audit`.

---

## 8. fs_apply_bundle

### Purpose
Zapíše více souborů najednou do povolených remote roots.

### Request
```json
{
  "target": "prod-eu1",
  "bundle": [
    {
      "path": "/opt/candoitall/stacks/myapp/docker-compose.yml",
      "encoding": "utf8",
      "content": "services:\n  app: ...",
      "mode": "overwrite",
      "backupBeforeWrite": true,
      "permissions": "0640"
    },
    {
      "path": "/opt/candoitall/stacks/myapp/.env",
      "encoding": "utf8",
      "content": "APP_HOST=app.example.com\n",
      "mode": "overwrite",
      "backupBeforeWrite": true,
      "permissions": "0600"
    }
  ]
}
```

### Rules
- všechny cesty musí projít `PathGuard`,
- server může bundle odmítnout, pokud překročí size limit,
- při `backupBeforeWrite=true` vznikne revision backup metadata.

### Response data
```json
{
  "written": 2,
  "backupsCreated": 2,
  "revisionId": "rev_20260320T191500Z"
}
```

---

## 9. fs_read_text

### Purpose
Přečte textový soubor z remote rootu.

### Request
```json
{
  "target": "prod-eu1",
  "path": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "maxBytes": 65536
}
```

### Response data
```json
{
  "path": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "content": "services:\n  app: ...",
  "truncated": false
}
```

---

## 10. fs_backup_path

### Purpose
Vytvoří explicitní backup cesty nebo adresáře.

### Request
```json
{
  "target": "prod-eu1",
  "path": "/opt/candoitall/stacks/myapp",
  "label": "before-manual-fix"
}
```

### Response data
```json
{
  "backupId": "b_01JQ...",
  "storedAt": "/opt/candoitall/.mcp-state/backups/myapp/20260320T192010Z"
}
```

---

## 11. fs_restore_backup

### Purpose
Obnoví dříve vytvořený backup.

### Request
```json
{
  "target": "prod-eu1",
  "backupId": "b_01JQ..."
}
```

### Notes
- vhodné pro menší file rollback,
- pro stack rollback se preferuje `stack_rollback`.

---

## 12. docker_network_ensure

### Purpose
Zajistí existenci docker network.

### Request
```json
{
  "target": "prod-eu1",
  "name": "proxy",
  "driver": "bridge",
  "internal": false
}
```

### Response data
```json
{
  "name": "proxy",
  "created": false,
  "exists": true
}
```

---

## 13. docker_volume_ensure

### Purpose
Zajistí existenci docker volume.

### Request
```json
{
  "target": "prod-eu1",
  "name": "myapp-postgres-data"
}
```

### Response data
```json
{
  "name": "myapp-postgres-data",
  "created": true
}
```

---

## 14. compose_validate

### Purpose
Spustí `docker compose config` a vrátí výslednou validaci.

### Request
```json
{
  "target": "prod-eu1",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "workingDirectory": "/opt/candoitall/stacks/myapp"
}
```

### Response data
```json
{
  "valid": true,
  "normalizedConfigPreview": "services:\n  app: ...",
  "warnings": []
}
```

### Rule
Bez úspěšného `compose_validate` nesmí Codex pokračovat na `compose_apply`, pokud nejde o explicitní emergency mode.

---

## 15. compose_apply

### Purpose
Aplikuje stack přes `docker compose up -d`.

### Request
```json
{
  "target": "prod-eu1",
  "stackName": "myapp",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "workingDirectory": "/opt/candoitall/stacks/myapp",
  "pull": true,
  "build": false,
  "removeOrphans": true,
  "executionMode": "auto",
  "postWaitPolicy": {
    "waitForHealthyServices": ["app", "postgres"],
    "timeoutSeconds": 900
  }
}
```

### Behavior
- rozhodne sync vs async,
- založí detached job pro dlouhé operace,
- uloží command summary,
- vrátí `operationId` pokud je async.

### Response data
```json
{
  "stackName": "myapp",
  "executionModeResolved": "detached",
  "operationId": "op_01JQ...",
  "backupRevisionId": "rev_20260320T191500Z"
}
```

---

## 16. compose_ps

### Purpose
Přečte stav služeb ve stacku.

### Request
```json
{
  "target": "prod-eu1",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "workingDirectory": "/opt/candoitall/stacks/myapp"
}
```

### Response data
```json
{
  "services": [
    {
      "name": "app",
      "state": "running",
      "health": "healthy"
    },
    {
      "name": "postgres",
      "state": "running",
      "health": "healthy"
    }
  ]
}
```

---

## 17. compose_logs

### Purpose
Vrátí logy stacku nebo služby.

### Request
```json
{
  "target": "prod-eu1",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "service": "app",
  "tail": 200,
  "sinceSeconds": 600
}
```

### Response data
```json
{
  "service": "app",
  "lines": [
    "info: Listening on http://0.0.0.0:8080",
    "info: Health endpoint ready"
  ],
  "redacted": true
}
```

---

## 18. compose_exec

### Purpose
Spustí omezený příkaz v kontejnneru služby.

### Request
```json
{
  "target": "prod-eu1",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "service": "postgres",
  "command": ["pg_isready", "-U", "appuser", "-d", "appdb"],
  "timeoutSeconds": 30
}
```

### Rules
- příkaz se přijímá jako pole argumentů, ne shell string,
- pro citlivé nebo destruktivní commandy platí whitelist / denylist policy.

---

## 19. compose_down

### Purpose
Zastaví stack.

### Request
```json
{
  "target": "prod-eu1",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "removeOrphans": true
}
```

### Response
- vhodné jen pro explicitní maintenance nebo rollback scénáře.

---

## 20. stack_rollback

### Purpose
Obnoví poslední známou dobrou revizi stacku.

### Request
```json
{
  "target": "prod-eu1",
  "stackName": "myapp",
  "strategy": "last-known-good",
  "executionMode": "auto"
}
```

### Behavior
- najde vhodnou revizi,
- obnoví soubory,
- provede `compose_apply`,
- vrátí `operationId`.

### Response data
```json
{
  "restoringRevisionId": "rev_20260320T181500Z",
  "operationId": "op_01JQ..."
}
```

---

## 21. http_probe

### Purpose
Ověří HTTP(S) endpoint.

### Request
```json
{
  "target": "prod-eu1",
  "origin": "local",
  "url": "https://app.example.com/health",
  "expectedStatuses": [200, 301, 302],
  "timeoutSeconds": 20,
  "allowInsecureTls": false
}
```

### Origin values
- `local` – probe z prostředí MCP serveru
- `remote` – probe z cílového hostu

### Response data
```json
{
  "origin": "local",
  "url": "https://app.example.com/health",
  "statusCode": 200,
  "durationMs": 481,
  "tls": {
    "valid": true,
    "commonName": "app.example.com"
  }
}
```

---

## 22. http_wait

### Purpose
Čeká, dokud HTTP endpoint nesplní podmínky.

### Request
```json
{
  "target": "prod-eu1",
  "origin": "remote",
  "url": "http://app:8080/health",
  "expectedStatuses": [200],
  "timeoutSeconds": 180,
  "pollIntervalSeconds": 5
}
```

### Response
- `status=ready` nebo `status=timeout`

---

## 23. cert_check

### Purpose
Potvrdí stav TLS certifikátu a základní ACME readiness.

### Request
```json
{
  "target": "prod-eu1",
  "domain": "app.example.com",
  "origin": "local"
}
```

### Response data
```json
{
  "domain": "app.example.com",
  "certificateReady": true,
  "issuer": "Let's Encrypt",
  "notAfter": "2026-06-15T10:12:31Z",
  "warnings": []
}
```

### Notes
- pokud Traefik používá souborové ACME storage, může server číst metadata z remote hostu,
- pokud to není bezpečně dostupné, stačí TLS probe a issuer summary.

---

## 24. postgres_ready

### Purpose
Ověří PostgreSQL readiness.

### Request
```json
{
  "target": "prod-eu1",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "service": "postgres",
  "database": "appdb",
  "user": "appuser",
  "timeoutSeconds": 120
}
```

### Preferred implementation
- `docker compose exec -T postgres pg_isready ...`
- nebo `docker exec` dle stack layoutu

### Response data
```json
{
  "ready": true,
  "service": "postgres"
}
```

---

## 25. ipfs_status

### Purpose
Vrátí základní stav Kubo node.

### Request
```json
{
  "target": "prod-eu1",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "service": "ipfs"
}
```

### Response data
```json
{
  "daemonReady": true,
  "peerId": "12D3KooW...",
  "apiReachable": true,
  "gatewayReachable": true,
  "swarmPeerCount": 0
}
```

---

## 26. ipfs_private_validate

### Purpose
Zvaliduje, že Kubo běží jako privátní swarm a neuniká veřejně.

### Request
```json
{
  "target": "prod-eu1",
  "composeFile": "/opt/candoitall/stacks/myapp/docker-compose.yml",
  "projectName": "myapp",
  "service": "ipfs",
  "expectedBootstrapPeers": [
    "/dns4/ipfs-bootstrap.internal/tcp/4001/p2p/12D3KooW..."
  ],
  "minimumPeerCount": 0
}
```

### Validace zahrnuje
- existence `swarm.key`,
- absence default public bootstrap peers,
- shoda bootstrap listu s očekáváním,
- Kubo RPC API není veřejně mapované,
- případně minimální peer count.

### Response data
```json
{
  "privateMode": true,
  "swarmKeyPresent": true,
  "publicBootstrapDetected": false,
  "bootstrapPeers": [
    "/dns4/ipfs-bootstrap.internal/tcp/4001/p2p/12D3KooW..."
  ],
  "warnings": []
}
```

---

## 27. operation_status

### Purpose
Vrátí snapshot detached operace.

### Request
```json
{
  "target": "prod-eu1",
  "operationId": "op_01JQ..."
}
```

### Response data
```json
{
  "operationId": "op_01JQ...",
  "state": "running",
  "startedAt": "2026-03-20T19:15:00Z",
  "endedAt": null,
  "exitCode": null
}
```

---

## 28. operation_wait

### Purpose
Čeká na dokončení detached operace.

### Request
```json
{
  "target": "prod-eu1",
  "operationId": "op_01JQ...",
  "timeoutSeconds": 900,
  "pollIntervalSeconds": 5
}
```

### Response
- `state=succeeded|failed|cancelled|timeout`

---

## 29. operation_logs

### Purpose
Vrátí logy detached operace.

### Request
```json
{
  "target": "prod-eu1",
  "operationId": "op_01JQ...",
  "stream": "stdout",
  "cursor": 0,
  "maxBytes": 32768
}
```

### Response data
```json
{
  "cursorStart": 0,
  "cursorEnd": 8120,
  "content": "Pulling traefik...\nCreating network proxy...\n",
  "redacted": true
}
```

---

## 30. operation_cancel

### Purpose
Pošle cancel signal detached operaci.

### Request
```json
{
  "target": "prod-eu1",
  "operationId": "op_01JQ...",
  "graceSeconds": 10
}
```

### Notes
- nejdřív `TERM`,
- pak `KILL`,
- výsledek je best effort.

---

## 31. dangerous_raw_exec

### Purpose
Break-glass tool pro manuální zásah.

### MVP status
- implementačně může existovat v návrhu,
- defaultně je vypnutý,
- aktivace jen per target a s explicitním nastavením.

### Request
```json
{
  "target": "prod-eu1",
  "command": ["bash", "-lc", "id"],
  "timeoutSeconds": 15
}
```

### Hard requirements
- audit flag,
- explicitní allow config,
- warning banner v response,
- redakce logů,
- denylist pro vysoce destruktivní patterns.

## 32. Doporučený orchestration pattern pro Codex

### Nový deploy stacku
1. `target_test`
2. `target_audit`
3. `fs_apply_bundle`
4. `compose_validate`
5. `docker_network_ensure`
6. `compose_apply`
7. `operation_wait`
8. `compose_ps`
9. `postgres_ready`
10. `ipfs_status`
11. `ipfs_private_validate`
12. `http_wait(origin=local)`

### Traefik/cert workflow
1. `compose_apply` pro infra stack
2. `operation_wait`
3. `http_probe` nebo `http_wait`
4. `cert_check`

### Recovery workflow
1. `compose_logs`
2. `operation_logs`
3. `stack_rollback`
4. `operation_wait`
5. `http_wait`
