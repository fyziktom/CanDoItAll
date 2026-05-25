# Structured Input

| Note id | Exact wording / source | Normalized intent |
| --- | --- | --- |
| N001 | “agent nedoda potrebne artefakty v procesech” | Process runtime must not trust agent compliance alone; it must verify required process artifacts. |
| N002 | “Manazer behu procesu kdyztak resi recovery artefaktu pokud nevzniknou” | Manager recovery is an accepted mechanism, but it must be evidence-bound and audit-visible. |
| N003 | “odstranili SQLite… máme nyní jen PostgreSQL” | No SQLite scope. Validate and migrate only PostgreSQL. |
| N004 | “neplést workflows a procesy” | Keep workflow execution and process orchestration semantically separate. |
| N005 | “Processes můžou jako roli dosadit i workflow, ale jsou jakoby nadtím” | Workflow-backed roles are executors/providers; Processes own finalization, artifact contracts, transition, and recovery. |
| N006 | “chybí artefakt, nebo že není dodržen nějaký formát” | Artifact validation must include presence, format/schema, evidence lineage, freshness, and allowed producer mode. |
| N007 | “process step skončil jen v tom, že dělal 5x retry” | Retry policy must avoid repeated identical attempts when the artifact failure is invariant and recoverable/blockable. |

## Assumptions

- Codex will execute this bundle inside the repository under `codex/bundles/process-artifact-reliability-hardening` or an equivalent folder.
- The latest `development` branch remains PostgreSQL-only during execution.
- The implementation agent can use current repository skills, tests, and local PostgreSQL development database.
- Workflows may emit AgentFramework execution outputs, but process artifact expectations are still validated by Processes.

## Validation Expectations

- Tests prove the direct AgentFramework path and workflow-backed role path use the same process-owned finalizer.
- Tests prove missing required artifacts and invalid artifact formats do not trigger blind repeated retries.
- Tests prove manager recovery cannot satisfy an artifact expectation without valid evidence or an explicit blocked diagnostic.
- Tests prove response text cannot satisfy evidence/deliverable artifacts unless the expectation mode allows narrative synthesis.
- Tests prove fuzzy manager fallback does not select an unrelated “lead” agent for artifact recovery.
- PostgreSQL-only build/test/migration checks are recorded.
