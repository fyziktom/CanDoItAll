# Structured Input

| Note id | Exact user/source wording | Normalized intent |
| --- | --- | --- |
| N001 | "codex už ten další balíček také dokončil" | Verify the actual implementation, not just the bundle completion claim. |
| N002 | "pushnul jsem to do process-hardening" | Inspect the current hardening branch; if exact branch missing, use the nearest existing hardening branch and report it. |
| N003 | "proveď důkladnou kontrolu a analýzu našich slabin nebo chyb" | Identify remaining runtime weaknesses, brittle behavior, and likely blockers. |
| N004 | "připrav další bundle" | Create another implementation-ready Codex bundle. |
| N005 | "agent ... začal i implementovat což byl až druhý krok" | Add runtime and definition guardrails against step scope drift. |
| N006 | "procesní jádro musí zůstat generické" | Avoid software-only core logic and keep domain-specific behavior in skills/templates/contracts. |
| N007 | "Hodně je tedy i na instrukcích, definicích kroků" | Add definition linting, explicit contracts, and template quality gates, not just runtime code. |
