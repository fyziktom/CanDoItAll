# Prompt: PostgreSQL and private IPFS

Implementuj validaci a provozní tooly pro PostgreSQL a privátní IPFS.

Požadavky pro PostgreSQL:
- readiness check,
- interní connectivity pattern,
- žádná veřejná expozice defaultně.

Požadavky pro IPFS:
- Kubo v Dockeru,
- `swarm.key`,
- odstranění veřejných bootstrap peers,
- private peer list,
- API pouze interně,
- `ipfs_status`,
- `ipfs_private_validate`.

Dále:
- přidej knowledge docs / XML comments / README odkazy na rizika veřejné expozice IPFS API.
