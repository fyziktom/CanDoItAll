# Definition API documentation coverage repair

Status: source authored in the managed engine checkout on 2026-10-16; no build, discovery, test, schema generation, portability scan or database action performed by this repair owner. Main Stable source remains untouched. Parent owns serialized validation.

The original main-checkout `p02bc-stable-qualified/CanDoItAll.Tests.Integration.trx` contains two failing rows of `ApiDocumentationCoverageTests.Every_operation_and_schema_of_the_family_is_described`: **43** documentation gaps under `/api/project-structure` and **250** under `/api/projects`. Targeted namespace-aware XPath read those two ErrorInfo messages; the retained TRX and failures were not rewritten. These failures are not resolved merely by the separately prepared structural schema assertions.

The new definition DTOs and operations lacked descriptions for parameters, request/response knowledge, types, members and integer enum values. The repair adds one Web-owned metadata path in two cohesive files:

- `ProjectDefinitionOpenApiDescriptions.cs` explicitly maps CLR `Type` and `nameof` members. It describes coherent reads, original expectations, full/scalar commands, child submitted indexes, historical acceptance, unresolved/closed-absence lookup states and byte-limit meanings. No arbitrary type/member name receives automatic filler text. Common field descriptions are shared only across the known shapes that use them.
- `ProjectDefinitionOpenApiOperations.cs` identifies the seven named Projects routes and the two exact Project Structure handler methods, without renaming routes or operation IDs. It describes each request/parameter/status and preserves the distinction between a compatibility save and a protected durable command. Existing summaries/long descriptions remain owned by their handlers, except the previously missing context description.

`ProjectDefinitionOperationOpenApiSchemas.cs` invokes the descriptions after its existing shape construction. `ApiServiceCollectionExtensions.cs` registers the operation transformer after the epoch transformer. No DTO, converter, handler, writer, authorization, receipt schema or response status was changed. The generic `ApiEndpointDocumentation` was deliberately not expanded: its general retry/no-effect advice would misdescribe an earlier unconfirmed definition attempt.

This explicit boundary metadata is the reviewed source owner for these missing descriptions. It follows the shared API documentation standard's source-owned metadata alternative and the user's no-new-XML-comments instruction. Existing descriptions are preserved unless the same member needs this reviewed use-specific description. New typed descriptions include current response semantics only; the future H2 Rejected-503 union is not advertised before implementation.

No new mirror tests were added for prose. Existing coverage remains unchanged at **23 source-derived rows**: 22 fixed family InlineData plus one Fact, no dynamic provider or skip. Run the complete class because shared reachable types such as Error can be used by multiple families:

```text
FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiDocumentationCoverageTests
```

The separate current HTTP/schema candidate remains **49 rows**, not 50: `ProjectDefinitionHttpTests` 6 Facts + 8 InlineData = 14; `ProjectDefinitionHttpBodyTests` 8 + 6 = 14; `ProjectDefinitionHttpIntegrationTests` **5 + 10 = 15**; `ProjectOperationEpochHttpIntegrationTests` 3 Facts; `ProjectDefinitionOpenApiSchemaTests` 3 Facts. An earlier review message miscounted the five HTTP integration Facts as six; direct attribute recount corrected that mistake before execution and the H2 cards now retain 15. See [the unchanged 49-row selection](P02d-http-schema.md). These disjoint classes plus coverage give **72 expected rows** if selected together.

Required proof: direct Release Web/dependency build, build-backed discovery matching 23 and 49, full coverage and structural/serializer assertions, then the final complete portability-static procedure with reviewed deltas. Inspect the actual emitted descriptions in both definition families; source review does not prove transformer order, MethodInfo metadata selection or reference-description serialization. No generated SPA client may treat this source candidate as a validated schema yet.

## Reference annotation correction after the next checkpoint

Parent's retained `p02b-next-validation/api-documentation-coverage.result.json` records
**23 executed, 21 passed, 2 failed**, with **68** remaining Projects property-description
gaps and **1** Project Structure gap. The separately executed HTTP/schema selection
passed **49/49**. Coverage TRX SHA-256 is
`09229661DFD381ED92B61A245A195927B97BF508118B08B9EE18FD69E4EC72CC`.
These are results of the prior candidate, not proof of this correction.

The remaining failures are descriptions attached to reference objects. The retained
`schema-probe/openapi-with-response.json` declares **OpenAPI 3.1.1**. The installed
Microsoft.OpenApi 2.10.0 package identifies commit
`69f05b84f5f026ad18b3329bc70e4e72bd2539f2`; its
[reference serializer](https://github.com/microsoft/OpenAPI.NET/blob/69f05b84f5f026ad18b3329bc70e4e72bd2539f2/src/Microsoft.OpenApi/Models/OpenApiReferenceWithDescription.cs)
writes reference description overrides only for OpenAPI 3.1. That is a separate 3.0
compatibility concern, not proof of the current 3.1.1 failure's cause. The pinned ASP.NET Core 10.0.4
[schema service](https://github.com/dotnet/aspnetcore/blob/v10.0.4/src/OpenApi/src/Services/Schemas/OpenApiSchemaService.cs)
resolves components after schema transformers. Its reference-resolution path unwraps an
existing reference to its target and can replace property schemas with newly generated
component references. The earlier assignment therefore precedes the final reference
objects. Source review establishes this ordering; only the next actual emitted-document
run can confirm the correction. An earlier diagnosis incorrectly called the retained
document 3.0; this record explicitly corrects that claim.

The managed correction keeps the explicit Type/nameof description map and attaches its
typed descriptor to schema metadata. One Projects-owned document transformer applies
these property annotations after reference resolution. A referenced property becomes
`{ "description": "...", "allOf": [{ "$ref": "..." }] }`; its existing target is
preserved. Concrete properties and existing nullable unions retain their shape. Traversal
covers component schemas and their nested concrete schemas, avoids following references,
and uses object identity to avoid repeated traversal. No arbitrary property receives a
generated description, and no target schema is cloned or flattened. The registration
is immediately before the existing final description-text normalizer.

The structural schema test's local resolver now recognizes only the annotation wrapper:
exactly `description` and `allOf`, a nonempty description, and one reference-only branch.
Its existing required fields, array items, nullable shapes, numeric enum values and wire
assertions remain. The original-expectation case additionally asserts the actual emitted
admission wrapper, its description and its exact named reference. The generic coverage
checker is unchanged. Source counts remain **3 structural Facts**, **49 total HTTP/schema
rows**, and **23 documentation rows**; there is no new mirrored prose test.

This correction is source-only and separate from the unfinished W1b Workspace metadata.
No build, test, schema generation, scan or database action was run by its author. Parent
must refresh Web/Integration outputs and rerun the 23 documentation plus 3 structural
rows (26 directly affected rows), retaining the previous 46 unaffected HTTP rows, then
apply the normal final portability gate. Actual generator acceptance of the annotation
wrapper remains part of the later SPA generation proof.
