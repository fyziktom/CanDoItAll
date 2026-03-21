# Konfigurační model

## 1. Cíl konfigurace

Konfigurace musí vyřešit čtyři věci:

1. jaké targety existují,
2. jak se na ně autentizovat,
3. co je na nich dovoleno,
4. jaké doménové konvence a timeouty se mají použít.

## 2. Doporučený root model

```json
{
  "server": {},
  "transport": {},
  "security": {},
  "defaults": {},
  "targets": []
}
```

## 3. Sekce `server`

```json
{
  "name": "CanDoItAll.Mcp.SshOps",
  "stateDirectory": ".mcp-state/sshops",
  "logDirectory": ".mcp-state/sshops/logs",
  "maxBundleBytes": 1048576,
  "allowDangerousRawExec": false
}
```

### Význam
- `stateDirectory` – lokální stav serveru
- `logDirectory` – lokální diagnostické logy
- `maxBundleBytes` – ochrana proti přehnaně velkým bundle payloadům
- `allowDangerousRawExec` – globální hard stop pro raw exec

## 4. Sekce `transport`

```json
{
  "backend": "sshnet",
  "connectTimeoutSeconds": 20,
  "commandTimeoutSeconds": 120,
  "uploadTimeoutSeconds": 120
}
```

### Poznámka
`backend=open-ssh-cli` je budoucí rozšíření.  
MVP používá `sshnet`.

## 5. Sekce `security`

```json
{
  "requireHostKeyPinningInProduction": true,
  "redactSecretsInLogs": true,
  "denyAgentForwarding": true,
  "denyPasswordAuthentication": true
}
```

## 6. Sekce `defaults`

```json
{
  "remoteStateRoot": "/opt/candoitall/.mcp-state",
  "stacksRoot": "/opt/candoitall/stacks",
  "secretsRoot": "/opt/candoitall/secrets",
  "allowedRoots": [
    "/opt/candoitall",
    "/etc/traefik"
  ],
  "composeApplyTimeoutSeconds": 900,
  "httpWaitTimeoutSeconds": 180,
  "certWaitTimeoutSeconds": 600,
  "postgresWaitTimeoutSeconds": 120,
  "operationPollIntervalSeconds": 5
}
```

## 7. Sekce `targets`

Každý target má vlastní konfiguraci:

```json
{
  "name": "prod-eu1",
  "host": "prod-eu1.example.com",
  "port": 22,
  "user": "deploy",
  "sudo": {
    "mode": "sudo-n",
    "command": "sudo -n"
  },
  "auth": {
    "privateKeyEnv": "CANDOITALL_PROD_EU1_SSH_PRIVATE_KEY_B64",
    "privateKeyPassphraseEnv": "CANDOITALL_PROD_EU1_SSH_PRIVATE_KEY_PASSPHRASE"
  },
  "hostKeyVerification": {
    "mode": "fingerprintSha256",
    "value": "SHA256:REPLACE_ME"
  },
  "paths": {
    "remoteStateRoot": "/opt/candoitall/.mcp-state",
    "stacksRoot": "/opt/candoitall/stacks",
    "secretsRoot": "/opt/candoitall/secrets",
    "allowedRoots": [
      "/opt/candoitall",
      "/etc/traefik"
    ]
  },
  "docker": {
    "composeCommand": "docker compose",
    "requiredNetworks": ["proxy"],
    "defaultLoggingDriver": "local"
  },
  "traefik": {
    "stackName": "infra-traefik",
    "composeFile": "/opt/candoitall/stacks/infra-traefik/docker-compose.yml",
    "acmeStoragePath": "/opt/candoitall/stacks/infra-traefik/acme/acme.json",
    "dashboardHost": "traefik.example.com",
    "resolverName": "le"
  },
  "validation": {
    "publicAppHost": "app.example.com",
    "defaultHealthPath": "/health",
    "certificateDomains": ["app.example.com", "traefik.example.com"]
  },
  "guards": {
    "allowBootstrap": true,
    "allowComposeExec": true,
    "allowRawExec": false
  }
}
```

## 8. Host key verification model

### 8.1 Supported modes
- `fingerprintSha256`
- `knownHostsEntry`
- `knownHostsFile` (future / optional)

### 8.2 Doporučení
- produkce: `fingerprintSha256` nebo `knownHostsEntry`
- staging: stejná pravidla, jen s volitelně snadnější rotací
- development: může existovat `accept-new` režim, ale pouze pokud je explicitně povolen a není default

### 8.3 Rotation
Doporučené je podporovat pole hodnot:

```json
{
  "mode": "fingerprintSha256",
  "values": [
    "SHA256:old",
    "SHA256:new"
  ]
}
```

Tím se usnadní řízená rotace host key.

## 9. Secret resolution rules

### 9.1 Nikdy ne z tool inputu
Tajné hodnoty nesmí přicházet v běžném tool requestu, pokud to není výjimečný file bundle use case s okamžitou redakcí.

### 9.2 Preferované zdroje
1. environment proměnné,
2. remote secret files pod `secretsRoot`,
3. budoucí external secret store.

### 9.3 Typické env proměnné
- SSH private key
- SSH passphrase
- Traefik ACME email
- DNS provider token
- PostgreSQL password
- IPFS swarm key

## 10. Remote job settings

```json
{
  "remoteJobs": {
    "root": "/opt/candoitall/.mcp-state/jobs",
    "defaultDetachedThresholdSeconds": 20,
    "retentionDays": 14,
    "gracefulCancelSeconds": 10
  }
}
```

## 11. Compose revision settings

```json
{
  "revisions": {
    "enabled": true,
    "keepLast": 20,
    "backupBeforeOverwrite": true
  }
}
```

## 12. Redaction settings

```json
{
  "redaction": {
    "patterns": [
      "BEGIN OPENSSH PRIVATE KEY",
      "POSTGRES_PASSWORD=",
      "IPFS_SWARM_KEY",
      "CF_DNS_API_TOKEN"
    ],
    "replaceWith": "***REDACTED***"
  }
}
```

## 13. Example minimal settings

Viz:
- `15-examples/candoitall.mcpserver.settings.example.json`

## 14. Konfigurační zásady

### Z1 – One target, one truth
Vše důležité o targetu je v jedné definici, ne rozptýlené mezi různými soubory.

### Z2 – No hidden secrets
V settings jsou jen reference na tajné hodnoty, ne jejich obsah.

### Z3 – Explicit guardrails
Pokud je něco nebezpečné, musí to být explicitně povolené per target.

### Z4 – Stable conventions
Traefik, stacks, state root a network names mají být předvídatelné a konzistentní.

## 15. Konfigurační validace při startu

Server musí při startu validovat minimálně:

- unikátní názvy targetů,
- přítomnost `privateKeyEnv`,
- přítomnost host key verification,
- existenci alespoň jednoho allowed rootu,
- validní timeout rozsahy,
- konzistenci `allowRawExec` mezi globálním a target-level nastavením.

Při chybě konfigurace server failne při startu.  
Nesmí se spustit „napůl“.
