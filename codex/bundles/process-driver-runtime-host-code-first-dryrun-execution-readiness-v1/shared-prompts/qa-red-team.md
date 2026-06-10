# QA / Red-Team Prompt

Try to prove the implementation is shallow:
- report-only proof,
- non-empty output only,
- bundle/proof churn without source movement,
- fallback lane selector,
- generic object dispatch,
- hidden driver DI discovery,
- skipped live test reported as pass,
- audit store only in-memory,
- sync wrapper used in production,
- execution-capable driver sneaking through.

Fail the bundle if any trap succeeds.
