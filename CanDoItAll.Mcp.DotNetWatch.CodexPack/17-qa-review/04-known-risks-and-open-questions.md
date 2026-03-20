# Known risks and open questions

## 1. Repo-specific open questions

### OQ-1 — Přesný startup project path
Balík předpokládá jednu hlavní startup aplikaci, ale konkrétní path musí být potvrzená při repo discovery.

**Dopad:** bez potvrzení může být konfigurace serveru špatně.

### OQ-2 — Skutečný health endpoint
Balík počítá s health probe.  
Pokud CanDoItAll health endpoint nemá nebo je jiný, musí se upravit konfigurace nebo doplnit endpoint.

**Dopad:** `Healthy` wait by jinak musel spoléhat jen na slabší signály.

### OQ-3 — Test project inventory
Není zatím potvrzený přesný seznam test projektů a test runner konfigurace.

**Dopad:** `tests_run` default target může být potřeba upřesnit.

### OQ-4 — Package management model
Není potvrzené, zda solution používá central package management.

**Dopad:** jiný způsob přidání MCP SDK package referencí.

### OQ-5 — Browser tooling choice
Není potvrzené, zda UI validace poběží přes Playwright MCP nebo jiný browser tool.

**Dopad:** některé recommended defaults, zejména browser refresh policy, mohou být potřeba doladit.

## 2. Technická rizika

### R-1 — Binary lock contention
Aktivní watch session může kolidovat s build/test operacemi.

**Mitigace:** default `StopAndResume`, mutation lock, diagnostics.

### R-2 — Process tree kill nuance mezi platformami
Chování ukončení parent/child procesů se liší mezi Windows a Unix-like systémy.

**Mitigace:** platform abstraction + integration tests + compatibility matrix.

### R-3 — File watcher nuance v WSL, containers a network FS
`dotnet watch` může mít rozdílné chování podle prostředí.

**Mitigace:** volitelné `DOTNET_USE_POLLING_FILE_WATCHER`, manual test pass.

### R-4 — HTTPS dev cert / localhost TLS
Health probe může selhávat kvůli lokálním certifikátům, ne kvůli app logice.

**Mitigace:** explicitní localhost HTTPS policy + runbook.

### R-5 — Over-parsing CLI output
Příliš křehké parsování textových logů může vést k flaky diagnostice.

**Mitigace:** preferovat structured state, health probe a známé patterns; neodvozovat všechno z textu.

### R-6 — Agent discipline risk
I dobrý server selže workflowově, pokud ho klient obchází.

**Mitigace:** prompt pack, codex usage checklist, explicitní contract.

## 3. Acceptable follow-ups after MVP

Tyto věci nejsou blocker pro MVP:
- richer metrics endpoint
- operation cancel tool
- richer multi-app support
- direct browser helper tool
- full reattach to live processes after restart

## 4. Blockers vs non-blockers

### Blocker
- stdout contamination
- path escape
- stale cleanup killing wrong processes
- build/test without conflict policy
- missing wait semantics
- missing P0 validation coverage

### Non-blocking but important
- browser refresh policy tuning
- richer diagnostics categories
- optional MTP-specific enhancements
- macOS-specific polishing

## 5. Review guidance
Každý PR by měl explicitně uvést:
- zda zavádí nový risk,
- zda snižuje existující risk,
- nebo zda řeší některou open question.
