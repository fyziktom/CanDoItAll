# Structured Input

## User Requirements

- REQ-01: Add a Cognitive Memory module tab dedicated to searching quality clusters.
- REQ-02: Cluster search must use server-side paging and filtering and must not load all clusters, keys, members, memories, or sources into the UI.
- REQ-03: Cluster search must expose operationally useful fields: search text, key family, readiness, risk, cluster identity, counts, key previews, and member/evidence previews.
- REQ-04: The UI must stay large-screen only and must not introduce new medium/small viewport behavior.
- REQ-05: Prepare and maintain a detailed XLSX checklist for validation tracking.
- REQ-06: Validate against clean Cognitive Memory storage using PostgreSQL and Qdrant when available.
- REQ-07: Transfer project/project-structure/files/data source truth into the validation profile when a supported transfer path exists.
- REQ-08: Run ingestion, clustering/dreaming, approvals, probes, and recall validation using API endpoints rather than direct memory table writes.
- REQ-09: Capture troubles, blockers, and evidence gaps as first-class bundle records.
- REQ-10: Produce a follow-up architecture bundle for improvements that are not safe to finish in this pass.

## Success Definition

The implementation is acceptable only when cluster search is implemented with bounded data access, tests/build pass, a large-screen browser proof exists, the validation workbook exists, and any environment or architectural blockers to full clean PostgreSQL/Qdrant validation are recorded with a concrete follow-up bundle.
