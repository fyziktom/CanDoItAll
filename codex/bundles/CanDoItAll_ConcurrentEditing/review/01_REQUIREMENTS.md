# Kontrola požadavků zadání

Každá položka má konkrétní místo v balíku. Jde o pokrytí zadání v návrhu, nikoli dokončenou implementaci.

| ID | Požadavek | Podklady |
|---|---|---|
| REQ-01 | Hlavní Blazor UI a engine, nikoli jiné UI či nový workflow bundle | [00_START_HERE.md](../00_START_HERE.md); [02_ROLLOUT.md](../operations/02_ROLLOUT.md) |
| REQ-02 | Prověřit architekturu před plošnou implementací a odůvodnit malé prerequisite refaktory | [01_AUDIT_FINDINGS.md](../01_AUDIT_FINDINGS.md); [01_BOUNDARIES.md](../architecture/01_BOUNDARIES.md) |
| REQ-03 | Skutečná ochrana stale Save na serveru včetně alternate writerů | [02_WRITE_PERIMETER.md](../inventory/02_WRITE_PERIMETER.md); [01_TEST_STRATEGY.md](../tests/01_TEST_STRATEGY.md) |
| REQ-04 | CRM příklad vysvětlit konkrétním současným kódem | [01_AUDIT_FINDINGS.md](../01_AUDIT_FINDINGS.md); [01_SURFACES.md](../inventory/01_SURFACES.md); [sources.json](../evidence/sources.json) |
| REQ-05 | Proaktivní upozornění kdo/co změnil bez automatického přepsání formuláře | [03_CHANGE_OBSERVATION.md](../architecture/03_CHANGE_OBSERVATION.md); [01_IMPLEMENTATION.md](../pilot/01_IMPLEMENTATION.md) |
| REQ-06 | Sdílený typed human-friendly B/L/R merge pod úrovní modulů | [01_BOUNDARIES.md](../architecture/01_BOUNDARIES.md); [02_MERGE_PROTOCOL.md](../architecture/02_MERGE_PROTOCOL.md) |
| REQ-07 | Floating window, ruční rozhodnutí konfliktů, ne raw JSON dump | [02_MERGE_PROTOCOL.md](../architecture/02_MERGE_PROTOCOL.md); [04_OVERLAYS.md](../architecture/04_OVERLAYS.md) |
| REQ-08 | Neztratit dirty/raw inputs, EditContext, modal/route draft | [02_MERGE_PROTOCOL.md](../architecture/02_MERGE_PROTOCOL.md); [04_OVERLAYS.md](../architecture/04_OVERLAYS.md); [02_MANUAL_ACCEPTANCE.md](../pilot/02_MANUAL_ACCEPTANCE.md) |
| REQ-09 | Notifications nad modalem skutečně viditelné i interaktivní | [04_OVERLAYS.md](../architecture/04_OVERLAYS.md); [01_TEST_STRATEGY.md](../tests/01_TEST_STRATEGY.md) |
| REQ-10 | Tiché seznamy včetně create/delete a zachování filtrů/selection | [03_CHANGE_OBSERVATION.md](../architecture/03_CHANGE_OBSERVATION.md); [01_SURFACES.md](../inventory/01_SURFACES.md) |
| REQ-11 | Workflow/Process canvas explicitní pull jako ProjectStructure | [03_CHANGE_OBSERVATION.md](../architecture/03_CHANGE_OBSERVATION.md); [01_SURFACES.md](../inventory/01_SURFACES.md) |
| REQ-12 | Nejdříve Prompt Gallery list+modal pilot a nested reálné integrace | [01_IMPLEMENTATION.md](../pilot/01_IMPLEMENTATION.md); [02_MANUAL_ACCEPTANCE.md](../pilot/02_MANUAL_ACCEPTANCE.md) |
| REQ-13 | Rebuild 5032 a povinné zastavení do explicitního souhlasu | [01_EXECUTION_AND_5032.md](../operations/01_EXECUTION_AND_5032.md); [CODEX_PROMPT.md](../CODEX_PROMPT.md); [02_ROLLOUT.md](../operations/02_ROLLOUT.md) |
| REQ-14 | Výkon, bounded events/diffs, bez polling/wakeup storm | [06_PERFORMANCE.md](../architecture/06_PERFORMANCE.md); [03_CHANGE_OBSERVATION.md](../architecture/03_CHANGE_OBSERVATION.md) |
| REQ-15 | Ownership rozhodnutí nyní versus později, bezpečný actor/scope/auth | [05_AUTHORITY_AND_HISTORY.md](../architecture/05_AUTHORITY_AND_HISTORY.md); [03_RISKS_AND_OPEN_ITEMS.md](../review/03_RISKS_AND_OPEN_ITEMS.md) |
| REQ-16 | Undo/Redo připravit bez vynucení event sourcing; odlišit local/persisted | [05_AUTHORITY_AND_HISTORY.md](../architecture/05_AUTHORITY_AND_HISTORY.md) |
| REQ-17 | Konkrétní modulové plochy, owners, policy a vlny rollout | [surfaces.json](../inventory/surfaces.json); [01_SURFACES.md](../inventory/01_SURFACES.md); [02_ROLLOUT.md](../operations/02_ROLLOUT.md) |
| REQ-18 | Dopad na testy, nové cases a CodeAnalytics focused selection | [01_TEST_STRATEGY.md](../tests/01_TEST_STRATEGY.md); [01_EXECUTION_AND_5032.md](../operations/01_EXECUTION_AND_5032.md) |
| REQ-19 | Úklid zastaralých testů pouze s doložením; nevymýšlet deletions | [02_TEST_CLEANUP.md](../tests/02_TEST_CLEANUP.md) |
| REQ-20 | Před finálním ZIP review, korekce, recheck a integrita | [02_FINAL_REVIEW.md](../review/02_FINAL_REVIEW.md); [validate_package.py](../scripts/validate_package.py) |
| REQ-21 | Evidence aktuální SHA; nezaměnit historické reporty za provedené testy | [README.md](../evidence/README.md); [sources.json](../evidence/sources.json); [03_RISKS_AND_OPEN_ITEMS.md](../review/03_RISKS_AND_OPEN_ITEMS.md) |
| REQ-22 | Outcome safety: committed warning/unknown/third writer/delete/access loss | [02_MERGE_PROTOCOL.md](../architecture/02_MERGE_PROTOCOL.md); [02_WRITE_PERIMETER.md](../inventory/02_WRITE_PERIMETER.md); [01_TEST_STRATEGY.md](../tests/01_TEST_STRATEGY.md) |

Strojová mapa je v [requirements.json](requirements.json). Schválení pilotu zůstává neudělené; žádný checklist v tomto balíku jej nenahrazuje.
