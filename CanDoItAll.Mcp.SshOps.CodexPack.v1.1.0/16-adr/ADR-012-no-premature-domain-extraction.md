# ADR-012: no premature domain extraction

## Status
Accepted

## Context
Při zavádění shared foundation hrozí opačný extrém:
že se do shared layer vytlačí příliš mnoho doménové logiky.

## Decision
Do shared layer se budou extrahovat jen stabilní cross-server primitives.

Server-specific logika zůstane lokálně, zejména:
- dotnet watch orchestrace,
- SSH transport a host key model,
- Docker / Traefik / PostgreSQL / IPFS doména.

## Consequences
### Positive
- shared layer zůstane malá a stabilní,
- menší riziko „God library“.

### Negative
- některé helpery zůstanou dočasně duplikované nebo podobné,
- část budoucí extrakce se odkládá.

## Rationale
Předčasná abstrakce škodí stejně jako nekontrolovaná duplicita.
Tato ADR vědomě volí střední cestu.
