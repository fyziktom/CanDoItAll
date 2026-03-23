# ADR-003: mandatory host key pinning for production

## Status
Accepted

## Decision
Produkční targety musí používat pinned host key verification.

## Why
- bez pinningu je riziko MITM,
- Codex workflow nesmí spoléhat na interaktivní potvrzování host key.

## Consequences
- rotace host key musí být zdokumentovaná,
- onboarding nového hostu vyžaduje bezpečné získání fingerprintu.
