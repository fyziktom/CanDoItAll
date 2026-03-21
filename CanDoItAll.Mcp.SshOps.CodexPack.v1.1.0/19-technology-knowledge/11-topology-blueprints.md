# Topology blueprints

## Blueprint A: single-host staging
- 1 Ubuntu host
- Traefik stack
- .NET app stack
- PostgreSQL container
- IPFS Kubo container
- public ports: 80, 443
- optional private swarm only within same host or lab peers

## Blueprint B: multi-host private IPFS
- 1 app host
- 1 additional IPFS peer host
- controlled routing for port 4001 between peers
- API still internal only
- bootstrap list includes only controlled peer multiaddrs

## Blueprint C: shared infra + multiple apps
- shared Traefik
- multiple app stacks attached to same `proxy`
- isolated `backend` networks per app or per bounded context
- one or more dedicated private IPFS stacks depending on trust boundaries
