# Normalized Requirements

## Functional Requirements

- FR-01: Add a `Cluster Search` tab to the Cognitive Memory page.
- FR-02: Allow search by cluster key/display text and cluster hash.
- FR-03: Allow optional filtering by key family, readiness, and risk.
- FR-04: Return cluster search results through the Review UI service with server-side paging.
- FR-05: Show cluster result identity, status, counts, key previews, and member/evidence previews.
- FR-06: Reset cluster-search page index when filters are applied or cleared.
- FR-07: Keep the existing Quality Operations tab intact.
- FR-08: Build a validation workbook with checklist rows for UI, API, storage, transfer, ingestion, clustering, dreaming, approvals, probes, and follow-up architecture.
- FR-09: Attempt clean PostgreSQL/Qdrant validation using supported app APIs.
- FR-10: Record blockers and propose architecture follow-up items where validation cannot complete.

## Non-Functional Requirements

- NFR-01: Large-screen-only implementation and proof.
- NFR-02: No medium/small-screen responsive tuning.
- NFR-03: Strongly typed query/filter contracts.
- NFR-04: No direct writes to Cognitive Memory truth tables.
- NFR-05: Bounded page sizes and bounded preview loading.
- NFR-06: Tests must cover server-side filtering/paging and component access to the tab.

## Out Of Scope

- Rebuilding the whole Cognitive Memory module page.
- Introducing a new full-text search engine.
- Shipping mobile/tablet layouts.
- Silently fabricating clean PostgreSQL/Qdrant proof when the environment is missing.
