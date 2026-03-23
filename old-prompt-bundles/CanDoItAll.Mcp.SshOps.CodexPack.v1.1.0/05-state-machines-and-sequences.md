# Stavové automaty a sekvence

## 1. Target state machine

```mermaid
stateDiagram-v2
    [*] --> Unknown
    Unknown --> Verified: target_test success
    Unknown --> Error: host key/auth failure
    Verified --> Ready: target_audit passes
    Verified --> Degraded: audit warns
    Ready --> Busy: mutating operation starts
    Busy --> Ready: operation success
    Busy --> Degraded: partial success / validation warning
    Busy --> Error: failed operation
    Degraded --> Busy: remediation operation
    Degraded --> Ready: validation passes
    Error --> Busy: retry / rollback
    Error --> Verified: manual reset
```

## 2. Operation state machine

```mermaid
stateDiagram-v2
    [*] --> Accepted
    Accepted --> RunningInline
    Accepted --> RunningDetached
    RunningInline --> Succeeded
    RunningInline --> Failed
    RunningDetached --> Succeeded
    RunningDetached --> Failed
    RunningDetached --> CancelRequested
    CancelRequested --> Cancelled
    CancelRequested --> Failed
    RunningDetached --> TimedOut
```

## 3. Stack deployment state machine

```mermaid
stateDiagram-v2
    [*] --> Preflight
    Preflight --> FilesPrepared
    Preflight --> Failed
    FilesPrepared --> Validated
    FilesPrepared --> Failed
    Validated --> Applying
    Applying --> WaitingContainers
    Applying --> Failed
    WaitingContainers --> WaitingHealth
    WaitingContainers --> Failed
    WaitingHealth --> WaitingPublicProbe
    WaitingHealth --> Failed
    WaitingPublicProbe --> Succeeded
    WaitingPublicProbe --> Degraded
    Failed --> RollbackEligible
    RollbackEligible --> RolledBack
    RollbackEligible --> ManualIntervention
```

## 4. ACME issuance state machine

```mermaid
stateDiagram-v2
    [*] --> RouterVisible
    RouterVisible --> ChallengeReachable
    RouterVisible --> Failed
    ChallengeReachable --> PendingOrder
    ChallengeReachable --> Failed
    PendingOrder --> CertificateIssued
    PendingOrder --> RateLimited
    PendingOrder --> Failed
    CertificateIssued --> HttpsValidated
    CertificateIssued --> Failed
```

## 5. IPFS private swarm state machine

```mermaid
stateDiagram-v2
    [*] --> SwarmKeyPresent
    SwarmKeyPresent --> RepoInitialized
    SwarmKeyPresent --> Failed
    RepoInitialized --> PublicBootstrapRemoved
    PublicBootstrapRemoved --> PrivatePeersConfigured
    PrivatePeersConfigured --> DaemonReady
    DaemonReady --> PeerConnectivityValidated
    DaemonReady --> SingleNodeReady
    PeerConnectivityValidated --> Succeeded
    SingleNodeReady --> Succeeded
```

## 6. Sekvence – první bootstrap hostu

```mermaid
sequenceDiagram
    participant Codex
    participant MCP
    participant SSH
    participant Host

    Codex->>MCP: target_test(target)
    MCP->>SSH: connect + host key verify
    SSH->>Host: auth + verify
    Host-->>SSH: ok
    SSH-->>MCP: ok
    MCP-->>Codex: verified

    Codex->>MCP: target_audit(target)
    MCP->>SSH: gather OS/docker/sudo/ports info
    SSH->>Host: audit commands
    Host-->>SSH: audit data
    SSH-->>MCP: structured audit
    MCP-->>Codex: audit result

    Codex->>MCP: host_bootstrap_prepare(target, mode=full)
    MCP->>SSH: create remote job
    SSH->>Host: detached bootstrap script
    Host-->>SSH: operationId
    SSH-->>MCP: operation accepted
    MCP-->>Codex: operationId

    Codex->>MCP: operation_wait(operationId)
    MCP->>SSH: poll status files
    SSH->>Host: read status/logs
    Host-->>SSH: succeeded
    SSH-->>MCP: succeeded
    MCP-->>Codex: bootstrap done
```

## 7. Sekvence – deploy aplikačního stacku

1. `fs_apply_bundle`
   - zapíše compose artefakty,
   - případně udělá backup předchozí verze.

2. `compose_validate`
   - spustí `docker compose config`,
   - odhalí syntaktické chyby, chybějící env proměnné nebo špatné merge.

3. `docker_network_ensure`
   - zajistí existenci `proxy` a případně dalších sítí.

4. `compose_apply`
   - spustí detached job pro `docker compose up -d --remove-orphans`,
   - vrátí `operationId`.

5. `operation_wait`
   - čeká na dokončení remote jobu.

6. `compose_ps`
   - přečte stav služeb.

7. `http_wait`
   - ověří interní health appky.

8. `postgres_ready`
   - ověří DB readiness.

9. `ipfs_status` + `ipfs_private_validate`
   - ověří Kubo daemon a privátní konfiguraci.

10. `http_wait(origin=local)`
   - ověří veřejnou doménu přes Traefik.

## 8. Sekvence – Traefik + certifikát

```mermaid
sequenceDiagram
    participant Codex
    participant MCP
    participant SSH
    participant Host
    participant LE as Let's Encrypt

    Codex->>MCP: compose_apply(traefik stack)
    MCP->>SSH: detached compose up
    SSH->>Host: start traefik stack
    Host-->>SSH: operationId
    SSH-->>MCP: accepted
    MCP-->>Codex: operationId

    Codex->>MCP: operation_wait(operationId)
    MCP->>SSH: poll status
    SSH->>Host: read status
    Host-->>SSH: success
    SSH-->>MCP: success
    MCP-->>Codex: apply finished

    Codex->>MCP: http_wait(origin=local, url=http://domain)
    MCP-->>Codex: challenge reachable

    Codex->>MCP: cert_check(domain)
    MCP->>SSH: inspect acme file or probe https
    SSH->>Host: read cert state
    Host-->>SSH: cert ready
    SSH-->>MCP: cert info
    MCP-->>Codex: certificate issued
```

## 9. Sekvence – IPFS private swarm init

### Single-host režim
- `swarm.key` je přítomný,
- Kubo repo je inicializované,
- bootstrap list neobsahuje veřejné bootstrap peers,
- Kubo API je přístupné pouze interně,
- veřejný swarm port 4001 nemusí být publikovaný.

### Multi-host privátní swarm
- `swarm.key` je shodný na všech uzlech,
- default bootstrap peers jsou odstraněny,
- bootstrap list ukazuje jen na kontrolované peers,
- 4001 TCP/UDP je routovatelný mezi uzly,
- volitelně je nastaven `AppendAnnounce`.

## 10. Sekvence – rollback

```mermaid
sequenceDiagram
    participant Codex
    participant MCP
    participant SSH
    participant Host

    Codex->>MCP: stack_rollback(target, stack)
    MCP->>SSH: resolve last good revision
    SSH->>Host: inspect revision metadata
    Host-->>SSH: revision selected
    SSH-->>MCP: revision selected

    MCP->>SSH: detached restore + compose up
    SSH->>Host: restore backup files and restart stack
    Host-->>SSH: operationId
    SSH-->>MCP: accepted
    MCP-->>Codex: operationId

    Codex->>MCP: operation_wait(operationId)
    MCP->>SSH: poll status
    SSH->>Host: read status/logs
    Host-->>SSH: success
    SSH-->>MCP: success
    MCP-->>Codex: rolled back
```

## 11. Důležité provozní pravidlo

Každá sekvence s mutací hostu musí končit jedním z těchto stavů:

- `Succeeded`
- `SucceededWithWarnings`
- `FailedRollbackRecommended`
- `RolledBack`
- `ManualInterventionRequired`

Nikdy ne „možná je to hotové“.  
To je pro Codex rozhodovací past a vede k chaosu.
