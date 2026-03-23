# ADR-010: dual origin validation

## Status
Accepted

## Decision
Validace bude mít dva pohledy:
- remote/internal pohled z hostu,
- public/external pohled přes veřejný URL probe.

## Why
- některé chyby jsou vidět jen zvenku,
- některé jen zevnitř hostu.

## Consequences
- `http_probe` a další validační tooly musí mít jasně zvolený probe origin.
