# Structured Input

## Core Objective

- Run a long-form, staged demo validation of Cognitive Memory using richer project sources and repeated forced memory cycles, then prove whether the system keeps useful, source-backed memories and returns them correctly in AI chat.

## Success Criteria

- At least four staged source waves are available and tracked.
- Each source file is referenced in `sample-data/trackers/cognitive-memory-demo-source-tracker.xlsx`.
- Each stage can be loaded through APIs and, where useful, as Markdown project-structure asset nodes.
- After each stage, execution captures pre-review and post-review snapshots, review decisions, duplicate decisions, and source-reference checks.
- The forced consolidation/dreaming cycle is run after each stage.
- Backward analysis compares memory records, candidates, summaries, chunks, and source locators against the XLSX tracker.
- AI chat probes ask project-specific questions and are scored against expected source evidence.
- Any discovered memory-system defect gets an on-the-fly repair subbundle before the final closure decision.

## Hard Constraints

- Use PostgreSQL for all execution proof.
- Do not seed directly into Cognitive Memory tables outside EF persistence tests.
- Load validation data through APIs and project structure surfaces.
- Keep sample data in this bundle, not in automated test code.
- Do not silently convert AI chat validation into direct recall validation unless chat API access is blocked and the blocker is documented.

## Allowed Side Effects

- Add loader scripts, cycle-observation scripts, analysis scripts, generated evidence, and repair subbundles.
- Modify Cognitive Memory implementation only through an explicit repair subbundle created from observed evidence.
- Extend the XLSX tracker during execution with observed memory IDs, source locators, decisions, and chat outcomes.

## Validation Expectations

- API proof for each stage.
- Browser proof for review queue workflows where the UI is used to approve or reject items.
- Structured JSON/CSV/XLSX evidence for memory quality, duplicates, source references, and chat scoring.
- Final bundle validator run at completion.

## Working Assumptions

- The previous bundle left Cognitive Memory API and review preview controls available.
- The execution agent can create a new PostgreSQL database for this multi-cycle run.
- The execution agent may need to discover or repair chat-agent integration if the current API does not expose project memory to chat cleanly.
