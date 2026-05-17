# Implementation Prompt

Use this bundle as the durable state for the multi-cycle Cognitive Memory demo validation.

Before executing any stage:

- Confirm PostgreSQL is active with `GET /api/cognitive-memory/database/selection`.
- Use a fresh database for this bundle.
- Open `sample-data/trackers/cognitive-memory-demo-source-tracker.xlsx` and `sample-data/source-manifest.json`.
- Load data only through APIs and project structure surfaces.

For each stage:

1. Load only that stage's files.
2. Force ingestion and consolidation/dreaming cycle.
3. Capture status, ingestion, consolidation, snapshot, and review candidate evidence.
4. Inspect candidate previews before decisions.
5. Approve useful source-backed memories; reject duplicates/noise/wrong-source candidates.
6. Update the tracker or produce analysis evidence that maps source rows to candidates and memory records.
7. Run recall probes before moving to the next stage.

After all stages:

- Run AI chat probes from the tracker.
- Score answers against expected source evidence.
- Create on-the-fly repair subbundles for discovered memory defects.
- Rerun affected cycle/chat probes after repairs.

Do not close the bundle if a source file is untracked, a stage lacks API evidence, or a chat failure is left as vague residual risk.
