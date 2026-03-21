# ADR-009: PostgreSQL and IPFS internal only

## Status
Accepted

## Decision
PostgreSQL a IPFS RPC budou defaultně dostupné jen na interních Docker sítích.

## Why
- minimalizace attack surface,
- jasnější topologie,
- nižší riziko nechtěné veřejné expozice.

## Consequences
- validace musí umět zjistit, zda došlo k publikování portů,
- app musí používat interní Docker DNS jména.
