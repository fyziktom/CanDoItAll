# Assumptions And Risks

## Assumptions

- The source packs in `codex/bundles/input` are the authoritative raw inputs.
- The derived markdown source-truth files are the canonical validation baseline for this bundle.
- The local API base URL is `http://localhost:5032` unless the runner is invoked with another URL.
- PostgreSQL-backed Cognitive Memory is required for reliable validation.

## Critical Path Risks

- Weak extraction would contaminate all later validation because project nodes and recall probes are derived from extracted facts.
- A flat or shallow structure would not test parent/child memory behavior, so the loader parses headings into nested nodes.
- Review-decision mistakes can either approve duplicated noise or reject valid source-truth memories.
- Recall failures may come from ingestion, consolidation, review decisions, retrieval, policy filtering, or source locator handling; the analyzer must distinguish them before code repair.

## Validation Risks

- Some extracted PDFs/workbooks may omit formulas or pages beyond extractor limits; source-truth facts are based on the extracted content inspected for this bundle.
- Recall may legitimately paraphrase values; the analyzer uses required term checks as a minimum signal, not the full semantic comparison.
- If the local app is not running or not on PostgreSQL, API execution is blocked.
- External LLM/vector provider availability may affect consolidation and recall behavior.

## Reopen Triggers

- Reopen source extraction if a source-truth claim cannot be traced to an extracted file.
- Reopen structure loading if readback node counts are low, hierarchy depth collapses, or stage files are missing.
- Reopen review policy if duplicate project-node candidates dominate memory or valid external-file candidates are rejected.
- Reopen implementation only when repeatable evidence points to an app defect rather than malformed bundle data.
