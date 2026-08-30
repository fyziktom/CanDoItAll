# C# Boundary Map

| Responsibility | Current and target owner | Allowed repair |
| --- | --- | --- |
| Shared protocol validation / transport | Integration.SharedProviders.Http; contracts in Abstractions | Fix policy/rewrite/failure semantics; cache immutable allowlists. |
| Public HTTP/SSE outcome / OpenAPI | App.Web.Api | Emit safe terminal failure or abort; describe exact protocol fields/types. |
| Imported source runtime selection | App.Composition + existing URI policy port | Reuse canonical policy; no unsafe network flag promotion. |
| Capture policy and outcomes | History.Application + owning MAF decorators | Redaction syntax and typed failure mapping remain separate from DB. |
| Encryption / persistence / retention | History.Persistence | Bounded orphan cleanup with existing transaction/partition safeguards. |
| Catalog freshness and administration | Modules.AgentFramework.ProviderManagement | Cheap persisted freshness lookup; expensive projection on miss; target revalidation retained. |
| Wiring | Composition and existing registration extensions | Wire only any necessary small top-level collaborator; no business behavior moved into Program. |
| Documentation / reusable assets | Product docs / SharedInfo codex source packages | Product retains runbooks, SQL/proof; SharedInfo retains OpenAPI/API skills only. |

No new project, temporary bridge or contract move is justified. Existing abstractions suffice. If a pure timeout classifier or reusable destination classifier is needed, prefer one cohesive top-level type in its existing owner; tests must instantiate it without the old runtime.

Do not add nested services, runtime partial files, generic provider resolvers, service-location in core behavior, or a second source of truth for schema. If an extraction becomes necessary, moved logic must leave its old owner; record before/after size and negative delegation tests before widening scope.
