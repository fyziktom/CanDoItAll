# Threat model

## Assets
- SSH private keys
- host identity / host keys
- Docker host control plane
- application secrets
- TLS certificates / ACME account
- PostgreSQL data
- IPFS private swarm key and data
- operation logs and backups

## Main threat classes
1. Credential leakage
2. MITM on SSH
3. Path traversal and arbitrary file write
4. Arbitrary command execution
5. Public exposure of internal services
6. Secret leakage through logs
7. Unsafe rollback / partial apply
8. Disk exhaustion and operational denial of service

## Primary mitigations
- env-based secret injection,
- host key pinning,
- allow-listed roots,
- domain-specific tools,
- raw exec opt-in only,
- log redaction,
- detached operation journal,
- validation and probe gates,
- internal-only networking for PostgreSQL and IPFS RPC.
