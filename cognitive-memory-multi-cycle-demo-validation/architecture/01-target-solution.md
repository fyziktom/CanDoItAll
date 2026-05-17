# Target Solution

## Execution Architecture

The follow-up validation should run as an evidence-producing workflow around the existing Cognitive Memory APIs:

1. Prepare a fresh PostgreSQL database and launch the app against it.
2. Load staged source files through HTTP APIs and project structure operations.
3. Force ingestion and consolidation after each stage.
4. Inspect review items with candidate previews.
5. Record approval, rejection, duplicate, needs-changes, and contradiction decisions.
6. Run recall and AI chat probes.
7. Analyze stored memories backward against the XLSX source tracker.
8. Create repair subbundles when observed behavior is materially wrong.

## Data Model For Validation

- Source file identity is the primary tracking key.
- Project key and stage id must be included in idempotency keys and evidence paths.
- Every memory candidate must be checked against source locator, project id, and expected memory signals.
- Review decisions must be recorded with enough notes to explain why a candidate was approved or rejected.
- Chat probes must be scored against expected evidence, not just answer plausibility.

## API Boundaries

- Use Cognitive Memory developer APIs as the control surface.
- Use project structure APIs to create/update Markdown asset nodes.
- Do not directly write Cognitive Memory records except if a later repair subbundle adds or tests persistence code.
- Do not rely on SQLite for validation.

## Repair Loop

Execution must create repair subbundles when real-cycle evidence shows:

- poor chunking,
- wrong source locator,
- cross-project leakage,
- duplicate explosion,
- contradiction/supersession failure,
- useless approved memory,
- missing chat integration with memory,
- or vector/projection quality problems that can be fixed in code.

Repair subbundles are expected during this bundle. They are not a failure of preparation; they are the planned mechanism for discoveries that require implementation changes.

## Evidence Outputs

- JSON snapshots for status, ingestion operations, consolidation runs, review items, recall traces, and chat probes.
- Updated workbook or workbook-derived evidence mapping source files to observed candidates and memories.
- Browser screenshots for review UI decision flows.
- Execution report with stage-by-stage cycle analytics and final raw-note closure.
