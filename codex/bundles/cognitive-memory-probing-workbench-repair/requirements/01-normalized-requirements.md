# Normalized Requirements

| Id | Requirement | Source | Owner |
| --- | --- | --- | --- |
| R-001 | The Cognitive Memory page must include a Dialogue Workbench that can start a project-scoped probe session and ask arbitrary user questions. | `inputs/00-original-request.md` | `subbundles/02-02-dialogue-workbench-ui-and-validation` |
| R-002 | A probe answer must show the recalled context sections, source refs, warnings, and recall trace metadata needed to judge why memory answered that way. | `architecture/15-interactive-memory-probing.md` | `subbundles/02-02-dialogue-workbench-ui-and-validation` |
| R-003 | The user must be able to mark an answer correct/important, incorrect, wrong-scope, needs-source, or corrected using typed feedback actions plus free-text notes/correction text. | `architecture/15-interactive-memory-probing.md` | `subbundles/01-01-probing-feedback-repair-core`, `subbundles/02-02-dialogue-workbench-ui-and-validation` |
| R-004 | Probe correction feedback must create a review-gated repair candidate that can add or update canonical memory only after explicit review approval. | `inputs/00-original-request.md` | `subbundles/01-01-probing-feedback-repair-core` |
| R-005 | Probe feedback must never directly mutate active canonical truth. | `architecture/15-interactive-memory-probing.md` | `subbundles/01-01-probing-feedback-repair-core` |
| R-006 | Failed or corrected probe turns must be able to create durable regression tests linked to the probe turn and replayable through recall. | `architecture/16-probing-regression-and-calibration-loop.md` | `subbundles/01-01-probing-feedback-repair-core` |
| R-007 | Validation must use the loaded AI Tap/Faucet and Curacao Glass realistic project memories. | `inputs/00-original-request.md` | `subbundles/02-02-dialogue-workbench-ui-and-validation` |
| R-008 | Browser-visible workbench proof must include desktop and narrower viewport checks, with readable trace/source/correction controls. | `candoitall-bundle-workflow` proof rules | `subbundles/02-02-dialogue-workbench-ui-and-validation` |
