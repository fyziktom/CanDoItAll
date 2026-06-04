# Raw User Request

Codex already implemented and pushed the previous hardening/refactoring bundle. The request is to inspect how it was incorporated, identify what Codex skipped, weakened, or left risky, and prepare a follow-up bundle. The user expects a senior C# architecture review, a senior QA inspection, and a final ZIP bundle suitable for Codex execution.

## Preserved Literal Requirements

| Raw note | Preserved requirement |
| --- | --- |
| “Podívej se na to jak to zapracoval.” | Review the current pushed implementation, not only the previous plan. |
| “Určitě něco přeskočil nebo vynechal…” | Actively search for skipped scope, weak proof, and omitted hardening. |
| “...nebo je potřeba ještě něco dalšího refaktorovat.” | Identify additional refactoring needed before more features. |
| “Proveď důkladnou kontrolu…” | Include source-level and proof-level review, not just a summary. |
| “...navrhni followup bundle.” | Produce a structured executable bundle with subbundles, gates, and QA review. |

## User Context Carried Forward

- The repository is private; current target is `fyziktom/CanDoItAll`, branch `development`.
- Previous bundle must follow the bundle skills under `codex/skills/bundles`.
- Token/cost mismatch against OpenAI billing remains a high-priority concern.
- Five domain-distinct app-generation examples are required, but they must prove the generic process path rather than hardcoding the examples.
- Final package must be a ZIP after senior QA inspection.
