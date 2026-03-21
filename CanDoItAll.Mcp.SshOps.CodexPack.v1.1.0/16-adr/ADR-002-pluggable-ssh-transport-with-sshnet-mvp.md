# ADR-002: pluggable SSH transport with SSH.NET MVP

## Status
Accepted

## Decision
Transportní vrstva bude abstrahovaná přes `ISshTransport`, MVP backend bude `SshNetTransport`.

## Why
- lepší testovatelnost,
- možnost budoucího přepnutí na OpenSSH CLI backend,
- oddělení SSH detailů od orchestrace.

## Consequences
- je třeba mapovat backend-specific výjimky,
- fake transport bude snadný pro integration testy.
