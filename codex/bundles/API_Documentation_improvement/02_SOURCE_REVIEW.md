# Source review and gap analysis

## Decision

This should be a documentation-contract completion run, not a new model/UI architecture exercise. Preserve the repaired task wire boundary. Stabilize terminology, establish one reliable XML-to-OpenAPI path, then document the complete current endpoint/schema closure and regenerate SharedInfo from that same build.

The following findings are source-derived unless explicitly labeled otherwise. Evidence IDs resolve in `evidence/sources.json`.

## Verified findings

### D01 — The generator and viewer are different components

The Web project targets net10.0 and references Microsoft.AspNetCore.OpenApi 10.0.4, Microsoft.OpenApi 2.10.0 and Swashbuckle.AspNetCore.SwaggerUI 10.2.0. Registration calls `AddOpenApi`, adds custom transformers, and publishes the document through `MapOpenApi`. Swagger UI points at `/swagger/v1/swagger.json`. **Adding SwaggerGen-specific IncludeXmlComments is not a fix for this observed stack.** [P01–P03]

The native XML facility is the starting point; the selected framework version must prove the actual result. Official documentation describes XML-enabled projects/references, compile-time inputs and named handlers for lambda-based endpoints. [F01, F03]

### D02 — XML output is not established by the inspected build files

Neither the inspected Web project nor root Directory.Build.props sets GenerateDocumentationFile. Root props suppresses CS1591. This supports a build-configuration gap to investigate, but is not proof of the final evaluated property: nested/imported targets, build arguments and sibling projects must be checked. Do not claim that no XML is generated anywhere until evaluated MSBuild output proves it. [P01, P04]

Enabling output only in Web would still leave API DTOs/handlers declared elsewhere dependent on their own XML emission and generator inputs. The task-update DTO is a concrete cross-assembly acceptance case. [P05]

### D03 — Many operation handlers have no documentable method target

CRM operations and task-update registration use inline lambdas; route names and tags do not explain their contracts. A summary on a Map method or route-registration class cannot substitute for operation documentation. Refactoring a lambda to a named handler must preserve its binding and metadata, not change its business logic. [P06, P10, F03]

### D04 — A public-C#-only scan would miss much of the real API

The CRM and Simple Chat wire inputs/responses include internal classes and internal positional records. CRM uses `[AsParameters]` query containers; Simple Chats expose nested types and enum converters. Their visibility to a remote client is determined by endpoint serialization, not the C# `public` keyword. [P09, P10, P13]

A property named `Schema`, `ModelParameterConfiguration` or `Summary` is actual business data. Tools comparing documentation must not accidentally delete such properties merely because their names resemble OpenAPI metadata keys.

### D05 — Native generation is already augmented with owner-specific schemas

Project Structure, workflow external responses and shared-provider relays register operation/schema transformers. Project Structure can replace schema properties; Shared Providers builds inline protocol schemas with real descriptions and constraints. A naive global transformer or blanket overwrite can lose valid descriptions, per-property meaning or wire restrictions. Inspect the **final** post-transform document. [P02, P16, P17]

Preserve the current strict relay subset; do not advertise every upstream OpenAI field as supported. No new upstream capabilities are authorized by this documentation work.

### D06 — Task-update repair is present, documentation lags

The inspected development source uses ProjectStructureTaskUpdateAgentInput for PUT task update. IDs are plain strings, nested schedule changes map through ToRequest, and route/body equality is checked using ordinal equality. The body contains owner-read preconditions and an explicit nullable JsonRequired cost-basis member. A matching project admission is required by the HTTP handler. [P05, P06]

The PostgreSQL integration test source creates a canonical task, submits a JSON update, expects WorkItemNotFound for a missing ID and verifies a successful schedule readback. That test source was read, not executed by this reviewer. [P07]

The attachment's claim that the patch was uncommitted was true as a report at its own time; it is not the current source state. Its SharedInfo follow-up is consistent with the old capture still described in SharedInfo. [E01, S03]

### D07 — Semantics cannot be generated from names

| Concrete example | Meaning supported by source | Documentation hazard |
|---|---|---|
| CurrentProgressPercent | -1 untracked or 0..100 | Globally claiming every percentage is 0..100 |
| ProposedProgressPercent | 0..100 | Allowing -1 because the current value supports it |
| ExpectedEffortHours | Hours even when selected unit is ManDays | Multiplying/dividing twice in a separate UI |
| CurrentCostBasis | JSON presence required, null allowed | Omitting the member because it is nullable |
| ExpectedProjectAdmission | Nullable C# property, operationally required here | Calling it optional for HTTP mutations |
| ExpectedCostCurrencyCode | Three ASCII letters required when a cost exists | Claiming validation against an ISO currency registry |
| ExternalCode | Business reference, non-unique index in inspected model | Treating it as a guaranteed idempotency key |
| PublicContacts | Mapper sets IsPublic=true | Treating a data flag as anonymous HTTP authorization |

Sources: [P05, P06, P08, P09, P11, P18].

### D08 — Errors, paging, enums and security are not globally uniform

The common API helper returns an `errors` array envelope, while the task/Project Structure boundary returns an `error` object with an errorCode. Generic Result<T> success is unwrapped rather than exposing the internal Result container. [P14, P06, P07]

CRM query defaults are page-index based; Simple Chats returns cursor-based pages. Selected enums have camel-case string converters, while the Project Structure boundary and manual relay schemas have other explicit handling. Do not introduce a global string-enum converter or a uniform error envelope while writing descriptions. [P09, P13, P17, P16]

Authorization is endpoint/policy dependent. Some groups inherit authentication; specific policies require exact scopes, while others accept general-or-specific scope. Document the actual applied group and operation policy, not a product-wide guess. [P02, P03, P06]

### D09 — SharedInfo's published snapshot is a historical branch contract

S03 records source commit 160616c8256594257d00612b4e7dbadd567c024e, a September 15 capture with 289 paths, 321 operations and 518 schemas. Those figures are reported by its README, not independently counted here. The observed product development is newer. [S03]

Its snapshot manifest, README, route sets, API skills and partner migration reference must be regenerated/reconciled together. The current validator checks provenance and pins the canonical server URL. Do not capture an arbitrary older server merely because it owns port 5032, or falsify server/provenance fields to pass. [S03, S05]

### D10 — Some guidance needs semantic review, not only route parity

The Project Structure skill suggests an HTTP fallback when a requested runtime tool is unavailable. The current runtime-tool documentation explicitly distinguishes HTTP authorization from agent grants. Reword fallback guidance so a separate authorized operator/client may use its own allowed HTTP surface, but a denied/unavailable agent tool never implies permission to bypass runtime policy. This is a documentation consistency issue, not a request to change the policies. [S04, P15]

The SharedInfo owner-map snapshot-status paragraph still refers to pre-merge module work. Refresh historical framing to the actual captured source without rewriting history or asserting all branches are identical. [S02]

## What this review does not prove

No live description-coverage percentage, working Swagger UI, clean build result, current source-generated XML content or complete DTO inventory was measured. The large historical JSON artifact was identified, but connector retrieval returned empty content and byte download failed; this pack does not pretend to have parsed it. The supplied helper is tested on synthetic OpenAPI fixtures, not that product snapshot.

The glossary's 107 entries are reviewed starting vocabulary with source anchors. It deliberately separates preferred prose from wire names and owner-specific details. Opus must complete terminology for additional types discovered during the full inventory rather than guess.
