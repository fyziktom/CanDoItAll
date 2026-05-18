# Current State

## Architecture Intent

The original architecture defines probing as a controlled memory maintenance conversation. It explicitly says probing is not normal chat and not a RAG front-end. It must create evidence, correction candidates, review items, regression tests, and learning signals without directly mutating truth.

Key planned capabilities:

- free dialogue and guided probing modes;
- answer plus source/trace/confidence explanation;
- user correction lifecycle;
- feedback outcome classes including confirmed, partially correct, incorrect, missing knowledge, wrong scope, too generic, and overconfident;
- Dialogue Workbench layout with question queue, dialogue, trace/source panel, and correction/review/regression controls.

## Live Implementation

The live implementation has the following useful foundation:

- `ICognitiveMemoryProbeService` supports session start, ask, feedback, and regression replay.
- Minimal API routes exist under `/api/cognitive-memory/probes/...`.
- probe session, turn, feedback, finding, regression test, and regression run records are persisted.
- the Cognitive Memory page displays a passive probe-session panel.
- review UI can approve consolidation candidates and apply them through `ICognitiveMemoryConsolidationCandidateApplicator`.

## Implementation Gap

The current system does not yet satisfy the user-facing probing goal:

- no UI exists to start a probe session or ask arbitrary questions;
- no natural answer sections/source refs are displayed as a conversation;
- feedback does not isolate confirmed vs incorrect facts beyond one generic action and text field;
- correction feedback can create a review item, but the review item is not linked to an applicable memory repair candidate;
- review approval only applies consolidation candidates, so approving probe correction review items does not repair memory;
- regression tests are expected-text checks, not full required/forbidden source/scope constraints;
- no realistic-project validation proves the flow with AI Tap and Curacao Glass.

## Current Validation Baseline

The realistic project memory validation bundle already loaded two source-truth projects into PostgreSQL and reached 10/10 recall quality:

- AI Tap: `a845e5c9-43b5-4885-b970-7a63474029c3`
- Curacao Glass: `76770384-d515-40ce-9924-78a4a59b4f86`
- Final evidence: `C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\validation\evidence\20260517-204808-post-repair-recall-20260517-223454\96-memory-quality-analysis.md`

This bundle will use those projects to validate probing behavior, not to re-test initial ingestion.
