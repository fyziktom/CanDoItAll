# Requirement Traceability

| Requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R1 PostgreSQL-isolated runtime | `requirements/01-normalized-requirements.md` | `02-api-stage-loader-and-cycle-observation` | API database selection/status evidence | Must not reuse SQLite or ambiguous prior state. |
| R2 staged detailed demo corpus | `sample-data/staged-sources` | `01-staged-demo-corpus-and-trace-workbook` | Source count and manifest verification | 24 Markdown files across six projects and four stages. |
| R3 XLSX source traceability | `sample-data/trackers/cognitive-memory-demo-source-tracker.xlsx` | `01-staged-demo-corpus-and-trace-workbook` | Spreadsheet inspection/render verification | Workbook is the analysis backbone. |
| R4 API-only staged loading | `inputs/02-structured-input.md` | `02-api-stage-loader-and-cycle-observation` | Per-stage upload and ingestion JSON evidence | Direct DB writes are not acceptable. |
| R5 forced memory cycles | `plan/01-phase-plan.md` | `02-api-stage-loader-and-cycle-observation` | Consolidation run evidence per stage | Force the process to avoid waiting for scheduler timing. |
| R6 review and duplicate decisions | `requirements/01-normalized-requirements.md` | `03-review-approval-and-memory-quality-analysis` | Decision logs and review UI/API evidence | Approvals must use candidate previews. |
| R7 backward memory-quality analysis | `architecture/01-target-solution.md` | `03-review-approval-and-memory-quality-analysis` | Source-to-memory analysis artifacts | Check summaries, chunks, references, duplicates, vector/projection context where available. |
| R8 AI chat validation | `requirements/01-normalized-requirements.md` | `04-ai-chat-memory-validation-and-repair-loop` | Chat transcripts and scoring matrix | Direct recall-only fallback requires a blocker. |
| R9 on-the-fly repair subbundles | `plan/01-phase-plan.md` | `04-ai-chat-memory-validation-and-repair-loop` plus generated repair subbundles | Repair subbundle files and rerun proof | Discovered defects must not be hidden. |
| R10 closure evidence | `reviews/01-execution-report.md` | all subbundles | Final validator and execution report | Bundle cannot close with missing stage or chat evidence. |

## Raw Note Closure Plan

| Raw note | Requirement ids | Owning subbundle | Planned proof |
| --- | --- | --- | --- |
| Prepare more detailed data for demo projects | R2, R3 | 01 | Staged Markdown files and XLSX tracker |
| Observe multiple memory cycles | R4, R5 | 02 | Per-stage cycle evidence |
| Force ingest/dreaming cycle to speed it up | R5 | 02 | Consolidation run evidence after each load |
| Approve recommendations and duplicates | R6 | 03 | Review decision logs |
| Analyze backward if useful memories are kept | R7 | 03 | Memory quality analysis tied to tracker rows |
| Add emails as Markdown asset nodes | R2, R4 | 01, 02 | Stage 04 source files and project asset-node API proof |
| Reference all files in XLSX | R3 | 01 | Workbook inspection proof |
| Test via chat with AI agent | R8 | 04 | Chat transcript/scoring evidence |
| Create repair subbundles when discovered | R9 | 04 | Repair subbundle paths and rerun proof |
