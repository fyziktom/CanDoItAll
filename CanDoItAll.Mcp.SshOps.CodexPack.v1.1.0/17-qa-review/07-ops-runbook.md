# Ops runbook

## 1. Bootstrap nového hostu
1. Připrav DNS záznamy.
2. Získej a ověř host key fingerprint mimo MCP workflow.
3. Přidej target config s pinned fingerprintem.
4. Připrav SSH key env proměnnou.
5. Spusť `target_test`.
6. Spusť `host_bootstrap_prepare`.
7. Ověř Docker a Compose.
8. Vytvoř `proxy` network.
9. Nasaď Traefik stack.
10. Ověř HTTPS probe a cert.

## 2. Diagnostika failed deploye
1. `operation_status`
2. `operation_logs`
3. `compose_ps`
4. `compose_logs`
5. `http_probe`
6. Pokud je potřeba, `stack_rollback`

## 3. Diagnostika Traefik / cert problémů
- ověř DNS,
- ověř port 80/443 reachability,
- ověř resolver name v labels,
- ověř `acme.json` práva,
- začni staging resolverem,
- až pak přepni production.

## 4. Diagnostika PostgreSQL
- `compose_ps`,
- healthcheck status,
- volume permissions,
- env secrets,
- interní DNS `postgres`.

## 5. Diagnostika IPFS
- `ipfs_status`,
- `ipfs_private_validate`,
- kontrola `swarm.key`,
- kontrola bootstrap listu,
- kontrola peerů.

## 6. Rollback
1. Najdi poslední zdravou revision.
2. Spusť `stack_rollback`.
3. Ověř `compose_ps`.
4. Ověř `http_wait`.
5. Ověř `cert_check`, pokud se měnil ingress.

## 7. Housekeeping
- retence job logů,
- retence revision backupů,
- Docker image prune dle politiky,
- monitoring disk usage.
