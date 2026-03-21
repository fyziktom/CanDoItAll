# ADR-007: shared Traefik edge router

## Status
Accepted

## Decision
Na hostu bude preferovaný shared Traefik stack na externí `proxy` síti.

## Why
- centrální TLS a routing,
- jednodušší správa více stacků,
- konzistentní ingress pattern.

## Consequences
- Traefik stack má vyšší kritičnost,
- potřebuje samostatný lock a opatrný rollout.
