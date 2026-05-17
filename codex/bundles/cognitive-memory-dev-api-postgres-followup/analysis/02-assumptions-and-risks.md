# Assumptions And Risks

## Assumptions

- The app is run in `Development` for database-profile creation and switching.
- Local PostgreSQL is available through the repository Docker Compose service or another PostgreSQL instance.
- The `candoitall` PostgreSQL role can create the dedicated smoke database.
- Project-structure APIs are the correct source-of-truth path for sample data.
- Semantic/RAG providers may be unavailable in the local environment; if so, projection and recall limitations must be recorded explicitly.

## Risks

- Large Cognitive Memory services make regression fixes harder because behavior is packed into broad orchestration classes.
- A direct web project reference to the CognitiveMemory module creates duplicate scoped CSS static-web-assets; the existing transitive reference through Composition is required.
- Consolidation with projection rebuild enabled can fail if `IRagDriver` is not registered.
- Recall can fail if embedding/ranking/projection providers are absent.
- Project asset snapshots include notes and storage references, not necessarily full source-file content after redaction; important source details are also copied into node notes to keep the smoke meaningful.

## Mitigations

- Keep the developer API thin and mapped directly to existing contracts.
- Keep PostgreSQL enforcement in the skill and smoke loader rather than breaking existing SQLite shape tests.
- Use a `developer-no-vector-projection` consolidation profile for relational ingestion/consolidation smoke when provider setup is not part of the task.
- Capture provider-unavailable errors as evidence instead of treating them as failures to report.

## Critical Path Risks

- PostgreSQL container or local PostgreSQL service may not be available.
- Runtime database switching can fail if the new database cannot be created or migrated.
- Cognitive Memory consolidation can expose provider dependencies during smoke.

## Validation Risks

- OpenAPI shape tests can pass while behavior fails, so PostgreSQL smoke is required.
- Recall can fail for missing semantic/RAG providers; that is acceptable only if explicitly recorded.
- Project-structure asset content may be redacted or summarized in snapshots, so node notes also carry important source details.

## Reopen Triggers

- Any behavior smoke evidence using SQLite.
- Any silent fallback around missing semantic/RAG providers.
- Any direct database seeding path replacing project-structure API loading.
- Any final response claiming original v2 bundle closure.
