# Compatibility matrix

## Operating systems
- Ubuntu 24.04 LTS: primary target
- Ubuntu 22.04 LTS: supported target
- Older Ubuntu versions: not guaranteed for MVP

## Runtime and libraries
- .NET 10: required
- Official MCP C# SDK: current stable line at implementation time
- SSH.NET: current stable line at implementation time

## Remote platform
- Docker Engine with Compose plugin: required
- Traefik v3.x: preferred
- PostgreSQL 16/17 containers: supported
- Kubo/IPFS 0.38.x line: preferred baseline

## Out of scope for MVP
- Non-Ubuntu Linux distributions
- Podman
- Swarm / Kubernetes orchestration
- Windows SSH targets
