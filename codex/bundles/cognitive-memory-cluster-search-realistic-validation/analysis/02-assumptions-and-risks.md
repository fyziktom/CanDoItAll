# Assumptions And Risks

## Working Assumptions

- The Blazor module should use the existing Review UI service boundary, not direct DbContext access from components.
- Cluster search can initially search cluster hash and cluster keys/display text; source/memory full-text search can be a follow-up if it requires larger query infrastructure.
- PostgreSQL/Qdrant validation should use public app APIs and supported database-transfer services, not direct Cognitive Memory table writes.
- A blocked clean-database validation is still useful if the blocker is proven and converted into an architecture follow-up.

## Critical Path Risks

- Local PostgreSQL or Qdrant may not be running or may not expose credentials.
- Provider credentials or local model availability may block real dreaming/probe cycles.
- Existing transfer services may transfer settings subsets but not complete project/project-structure source truth through an API.
- Realistic validation can produce too much evidence for chat context, so the workbook and execution report must remain authoritative.

## Validation Risks

- Browser proof can validate UI behavior but not long-term memory correctness.
- API status success does not prove provider-backed consolidation or Qdrant projection health.
- Project-source truth may include sensitive files; validation must avoid uploading excluded secrets or credentials.

## Reopen Triggers

- Any cluster-search query path that loads unbounded clusters, keys, members, memory records, or source items.
- Any UI list that omits paging for a potentially large collection.
- Any validation step that writes Cognitive Memory facts directly to database tables.
- Any discovered transfer gap that invalidates the clean-profile validation plan.
