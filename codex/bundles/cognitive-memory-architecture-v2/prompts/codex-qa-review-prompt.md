# Codex QA Review Prompt

You are a strict senior C# architect and QA reviewer.

Review the implemented Cognitive Memory subbundle for correctness, durability, security, and architectural fit.

## Review Focus

1. Does it preserve raw source provenance?
2. Are generated summaries clearly derived and versioned?
3. Is Qdrant only a rebuildable projection?
4. Are existing CanDoItAll module patterns used?
5. Are RAG and semantic drivers wrapped instead of duplicated?
6. Are secrets protected before embedding/context injection?
7. Are semantically related but context-separated records kept separate?
8. Are workflow/MAF integrations typed and testable?
9. Are EF models configured correctly?
10. Are non-happy paths tested?
11. Does Epistemic Drive preserve vectors/evidence instead of scalar-only priority?
12. Are learning proposals approval-gated and source-grounded?
13. Do probing results update gap evidence without becoming automatic truth?
14. Do workbook phase rows, proof paths, handoff log, and `reviews/01-execution-report.md` agree?
15. Do corrections, stale refresh, probing, and learning outcomes preserve mutation authority, revision lineage, audit, and projection invalidation?

## Required Output

Produce a review report with:

- Pass/Fail summary.
- Critical issues.
- High-priority issues.
- Medium/low issues.
- Test coverage gaps.
- Suggested fixes.
- Approval decision.

## Non-Negotiable Failures

Fail the implementation if any of these occur:

- memory item without source ref,
- Qdrant used as sole source of truth,
- secret value embedded into vector payload/text,
- direct worker mutation of authoritative memory state,
- silent merge of context-separated topics,
- no negative tests for access/redaction/projection failure.
- Epistemic Drive implemented as only a final scalar score,
- external source study or high-impact memory update without required approval,
- learning-derived canonical/procedure record without source refs.
- workbook/execution-report mismatch for status, proof, blockers, or downstream permission,
- downstream phase started while prerequisite proof is blocked, reopened, or missing,
- active claim or belief state overwritten without revision lineage and audit.
