# ADR-008: ACME single writer and staging first

## Status
Accepted

## Decision
ACME storage bude single-writer a nové domény se nejdřív ověří přes staging resolver.

## Why
- nižší riziko rate limits,
- menší riziko poškození ACME storage,
- bezpečnější rollout.

## Consequences
- multi-instance Traefik bez sdíleného koordinovaného storage není v MVP,
- runbook musí řešit přepnutí staging -> production.
