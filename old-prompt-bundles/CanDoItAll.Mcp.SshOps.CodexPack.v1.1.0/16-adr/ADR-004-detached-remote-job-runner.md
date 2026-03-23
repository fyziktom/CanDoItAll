# ADR-004: detached remote job runner

## Status
Accepted

## Decision
Dlouhé mutující operace poběží jako detached job na remote hostu s persistentním operation journalem.

## Why
- image pull, compose up, restart stacku a cert issuance mohou trvat déle,
- klient může být restartovaný nebo odpojený,
- potřebujeme resumable workflow.

## Consequences
- přidáváme remote state a cleanup politiku,
- tooly musí vracet `operationId`.
