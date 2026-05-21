# Implementation Prompt

You are implementing this bundle for CanDoItAll Cognitive Memory. First read `README.md`, `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md`, and `plan/01-phase-plan.md`. Then execute subbundles strictly in order.

Do not mark any subbundle completed by only adding tests, reports, or source assertions. Every behavior claim must have failing-first proof, production source changes, passing tests, anti-stub audit, and portable proof artifacts. If a report says `embedding-backed`, the production code must inject/use an embedding or ranker provider. If a report says `Czech/diacritic`, production code and tests must include Czech text with diacritics and diacritic-insensitive matching while preserving original text. If a report says `claim-specific`, the code must map exact supporting evidence for each claim, not all record-level evidence.

Before final closure, copy or move the bundle/repo to another path and run completed-stage validation there. No proof manifest may contain machine-specific paths.
