# API coverage inventory plan

## Sources and denominator

The historical JSON seed contains the 21 route families and counts reported by S03. It is not a current coverage report. Generate a fresh inventory from the actual final host and its EndpointDataSource/ApiDescription records, then traverse every request/response schema and reference. Compare source-registered operations intentionally omitted from OpenAPI separately.

Do not select only the Web/Api directory: ProjectStructureAgentApi.cs is outside it, and file/storage/runtime routes may be registered by other extensions. Do not select only public C# types: internal wire DTOs are present. Do not include every public helper just to inflate the denominator.

## Per-operation inventory row

Record method, route, operationId (or explicitly absent), tags/audience, handler symbol and project, input reader/serializer, request/response CLR types or manual schema owner, actual status/media types, headers, auth policy, prerequisites, description source, final document JSON pointers, example fixture and test proof. Mark an entry **reviewed**, **missing**, **blocked** or **intentionally excluded with reason**.

## Per-type/member inventory row

Record fully qualified CLR identity and assembly, wire schema identity/JSON pointer, role (request/response/shared), property JSON name, source property/constructor parameter, glossary concept IDs, null/presence/default rules, enum/format/units, sensitivity, XML source ID, final description pointer and proof. Distinguish class description from property-use description.

Include arrays/items, maps and typed additional properties, allOf/oneOf/anyOf, discriminators, generic pages, inherited fields and reusable header/parameter/response components. Resolve refs without infinite recursion. Missing/external refs need explicit handling, not silent omission. Treat binary/media-type and SSE/manual schema paths as first-class inventory entries.

## Seed implementation hotspots

| Area | Verified entry points | Main documentation concern |
|---|---|---|
| Generation | ApiServiceCollectionExtensions.cs, ApiEndpointRouteBuilderExtensions.cs | Native generator, existing transformers, both document routes |
| Tasks/Structure | ProjectStructureAgentApi.cs; ProjectStructureTaskUpdateAgentInput.cs | Named handlers; current/proposed preconditions; string keys; admission |
| CRM/HR | Api/CrmHrApi.cs; Api/CrmHrApiContracts.cs | Internal classes, AsParameters, privacy and complete replacements |
| Simple Chats | Api/LlmChatApiContracts.cs | Positional records, cursor pages, definition/transcript/concurrency distinctions |
| General API errors | Api/ApiEndpointResults.cs | Errors array, unwrapped success values, actual HTTP response metadata |
| Structure JSON | ProjectStructureHttpJsonContract.cs | Owner-specific input/response schema changes and supported read sources |
| Shared Providers | Api/SharedProviderOpenApiSchemas.cs and registered operation transformers | Inline foreign-protocol subset, constraints, fields that have no CLR DTO |
| Other current route families | Discover from runtime and exact source | Complete documentation; no assumption that a group inherits another group's auth/paging/schema rules |

This table identifies inspected hotspots, not a statement that all other files are deficient or reviewed.

## API skills

Enumerate every maintained `codex/skills/candoitall-api-*/SKILL.md` in SharedInfo plus the `_candoitall-api-shared` support package and referenced route appendices, examples and partner migration notes. The review directly read Project Structure and the shared support package, not every skill's full content. Record the disposition of all skills, including unchanged ones whose contracts still match the regenerated document.

## Coverage gates

At closure all supported operations and owned serialized members must have accurate descriptions or a narrowly reviewed external/structural exception. Exclusions need a concrete owner, reason and scope. Generated anonymous labels, broad 'framework type' exclusions and components skipped because they are internal are not acceptable.

A presence linter cannot assess purpose, unit correctness, safe retries or equivalent terminology. Use focused semantic review and public-seam examples in addition to numeric coverage.
