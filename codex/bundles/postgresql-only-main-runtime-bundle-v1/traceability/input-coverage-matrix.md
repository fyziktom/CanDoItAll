# Input coverage matrix

| User requirement | Covered by |
|---|---|
| Remove SQLite completely from main CanDoItAll | SB01, SB02, SB03, SB04, SB07 |
| Use branch development | All subbundles |
| Use repository bundle skills | README, COPY_PASTE_PROMPT, SB09 |
| Split into phases | plan/01-phase-plan.md, subbundles |
| Remove SQLite migrations and driver first | SB01 |
| Remove SQLite from UI next | SB03 |
| Remove limitations caused by SQLite | SB05 |
| Then process/workflow-specific changes | SB06 |
| Remove SQLite tests | SB04 |
| Consolidate PostgreSQL migrations into one | SB08 |
| Keep snapshot support deferred | SB07 |
| Be thorough and dependency-aware | analysis, traceability, subbundles, proof |
