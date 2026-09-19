# HTTP API documentation

The generated OpenAPI document is the published contract of the web host's HTTP API. Its prose comes from
the C# XML documentation of the route handlers and of every serialized request and response type, so the
same text appears in IntelliSense, in `/openapi/v1.json` and in Swagger UI. This page describes how that
pipeline works in this repository, the conventions it requires, and how it is validated.

The repository-family writing standard and the shared API vocabulary live in SharedInfo:
[API documentation standard](https://github.com/fyziktom/CanDoItAll.SharedInfo/blob/main/docs/standards/api-documentation.md)
and [API domain glossary](https://github.com/fyziktom/CanDoItAll.SharedInfo/blob/main/docs/architecture/candoitall-api-domain-glossary.md).
Use them when writing descriptions; this page covers only what is specific to this product.

## Pipeline

The document is produced by `Microsoft.AspNetCore.OpenApi` (`AddOpenApi` in
[`ApiServiceCollectionExtensions.cs`](../../src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs)) and
served on both `/openapi/v1.json` and `/swagger/v1/swagger.json`. Swagger UI is only a viewer of that
document. Do not add SwaggerGen or a second generator.

1. Every project that declares a handler or a serialized API type sets `GenerateDocumentationFile`. Its
   XML file is emitted next to the assembly.
2. The package's source generator runs in the Web project. It intercepts the `AddOpenApi` call, reads the
   Web project's own comments and, through the package's build targets, the XML files of every project
   reference. It then registers an operation and a schema transformer that apply those comments. A type
   from a NuGet package contributes no XML.
3. The product's own transformers run after the generated ones, in this order:
   - `OpenApiNullableTypeDescriptions` gives a component schema that was first generated from an optional
     value type (`Nullable<T>`) the XML description of `T`, using the same generated transformer.
   - `SharedProviderOpenApiSchemas` builds the shared-provider relay schemas, which have no CLR type.
   - `OpenApiExternalSchemaDescriptions` describes the few framework and Components types the API exposes
     (`ProblemDetails`, `JsonElement`, `IFormFile`, Gantt identifiers and gestures).
   - The Project Structure, workflow-response and shared-provider operation transformers adjust their
     operations. The Project Structure transformer inlines response schemas; it keeps the description of each
     use of a referenced type.
   - `OpenApiDescriptionText` normalizes all prose last: it joins wrapped lines, keeps paragraphs and list
     items, decodes XML entities and removes platform line endings, so the document is byte-identical on
     every build platform.

## Conventions the generator requires

- **Named handlers.** The compiler does not keep XML comments on lambdas. Every route handler is an
  `internal static` method of the class that maps it, documented with XML comments. `private` handlers are
  not visible to the generator. Keep the lambda's parameters, attributes, defaults and endpoint metadata
  unchanged when converting it.
- **Explicit tags.** A lambda's default tag is the application name, a method's is its class name. Every
  route group sets `WithTags`.
- **Operation text.** `<summary>` becomes the operation summary: one sentence. `<remarks>` becomes the
  description. Separate paragraphs with an empty `///` line and write list items as lines starting with
  `1.` or `-`. Do not use `<para>`, `<list>`, `<code>` or `<term>`: the generator flattens them.
- **Parameters.** Document route, query and header parameters and the request-body parameter with `<param>`.
  Never document service, `HttpContext` or `CancellationToken` parameters: every documented parameter that
  is not an HTTP parameter overwrites the request-body description. CS1573 is suppressed in the Web project
  for that reason.
- **Responses.** Declare every status the handler can return with `.Produces<T>(status)`, the family's error
  helper (`ProducesApiErrors`, `ProducesProjectStructureErrors`) or `ProducesProblem`, using the type and
  media type the handler really writes. Document each declared status with `<response code="...">`. A
  `<response>` tag never creates a response.
- **Types and members.** Only `<summary>` of a type, and `<summary>` plus `<value>` of a property, reach the
  schema. Put everything a client needs there; `<remarks>` on a type or property is for maintainers only.
  Document positional record members with `<param>` on the record.
- **Query containers.** Properties of an `[AsParameters]` type are documented on the type's properties.

## What a description must say

Follow the SharedInfo standard. In this repository in particular:

- Name the effective authority: with API authorization enabled the `/api` group accepts any valid bearer
  token, route groups and operations add specific scope policies, and Project Structure requires the `api`
  or `api.project-structure.write` scope. With authorization disabled (the development default) the routes
  are open.
- State the wire form of every enum: JSON integers unless a string converter is registered for that enum
  in `ApiServiceCollectionExtensions` or on the type. Project Structure responses use their own serializer,
  which writes `ProjectObjectType` as its symbol.
- Name the family's error envelope: the general `errors` array (`ApiErrorResponse`), the Project Structure
  `error` object, `ProblemDetails` for LLM Chats, and the shared-provider relay errors.
- Distinguish required, nullable, omitted and empty. Constructor parameters of request records appear as
  required in the schema even when the serializer would accept their omission; tell clients to send them.
- Describe identifiers by what they identify and which operation returns them. A node key is a string, not
  a GUID; a party is not a login account; an admission is not a lease.

## Validation

- `ApiDocumentationPipelineTests` prove the hard propagation cases: named handlers, `[AsParameters]`
  properties, positional records, internal and cross-assembly types, nullable members, inlined Project
  Structure responses and platform-independent text.
- `ApiDocumentationCoverageTests` fail when an operation, parameter, request body, response, component
  schema or property of the generated document has no description, except for the reviewed exclusions
  listed in the test.
- Structural changes to the document are reviewed separately from prose: compare the route, operation
  identifier, schema, required-member, enum, media-type and security metadata of the documents before and
  after a change, and justify every difference with runtime evidence.
