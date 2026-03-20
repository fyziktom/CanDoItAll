# Threat model

## 1. Scope
Threat model pokrývá lokální stdio MCP server pro CanDoItAll workspace.

Neřeší:
- vzdálený multi-tenant deployment,
- internetově vystavený server,
- produkční workload.

## 2. Assets
Chráněná aktiva:
- integrita CanDoItAll workspace
- dostupnost lokálního dev prostředí
- správnost build/test/app lifecycle
- lokální tajná data v env a konfiguraci
- bezpečná hranice mezi managed a unmanaged procesy

## 3. Trust boundaries
- MCP client -> MCP server
- MCP server -> local filesystem
- MCP server -> child dotnet processes
- MCP server -> localhost health endpoints

## 4. STRIDE-style analysis

### S — Spoofing
Riziko:
- stale registry se odkáže na proces, který už server nevlastní

Mitigace:
- ukládat workspace root, owner ID, command metadata a server instance info
- před kill akcí ověřit, že proces odpovídá očekávanému kontextu

### T — Tampering
Riziko:
- klient zadá cestu mimo workspace
- klient zadá nebezpečný env overlay

Mitigace:
- `PathGuard`
- env whitelist
- structured tool inputs místo raw shell strings

### R — Repudiation
Riziko:
- po cleanupu nebude jasné, proč byl proces ukončen

Mitigace:
- audit trail v logu
- correlation IDs
- cleanup event records

### I — Information disclosure
Riziko:
- logy vrátí tokeny, hesla nebo connection strings
- config snapshot vrátí citlivé env hodnoty

Mitigace:
- log redaction
- redacted config snapshot
- explicitní secret-like key masking

### D — Denial of service
Riziko:
- nekonečně přibývající logy
- stuck wait bez timeoutu
- watch/build/test konflikt zablokuje workflow
- klient spamuje mutující operace

Mitigace:
- bounded ring buffer
- timeouts
- mutation lock
- preemption policies
- safe error outcomes instead of deadlock

### E — Elevation of privilege
Riziko:
- server by se dal použít jako obecný command executor

Mitigace:
- žádné raw command stringy
- jen pevně modelované dotnet CLI operace
- workspace/root restrictions
- restricted env overlay

## 5. Specific abuse cases

### AC-1 — Run arbitrary project outside workspace
**Mitigace:** reject normalized path outside workspace or allowed roots.

### AC-2 — Probe external URL via health system
**Mitigace:** localhost-only default, explicit config gate for anything else.

### AC-3 — Leak secrets through process logs
**Mitigace:** redaction before returning/storing logs.

### AC-4 — Kill unrelated process through stale cleanup
**Mitigace:** ownership verification, workspace match, command metadata validation, conservative skip.

### AC-5 — Break stdio protocol via accidental logging
**Mitigace:** stderr/file logging only, integration test.

## 6. Residual risk
Přijatelné residual risk pro MVP:
- lokální developer může vždy spustit vlastní unmanaged proces mimo server
- některé log redaction false negatives mohou zůstat, pokud pattern není známý
- WSL/container file watcher nuance nemusí být plně odstranitelné

Tyto residual risks musí být dokumentované, ne ignorované.

## 7. Security release gates
Release nesmí projít, pokud:
- lze obejít path guard
- stdout se kontaminuje
- cleanup může bez verifikace zabíjet cizí procesy
- redaction chybí nebo je zjevně neúčinná
