# XML comments to OpenAPI: implementation design and proof

## Observed stack

P01–P03 show native Microsoft.AspNetCore.OpenApi generation with Swagger UI as a consumer. P02 registers existing Project Structure, workflow external-response and shared-provider transformers. P04 suppresses CS1591 and does not itself enable XML output. Keep these facts separate from evaluated build properties, which were not obtained by the reviewer.

The official references establish the native mechanism, named-handler requirement for compiler XML on Minimal APIs, and transformer ordering. [F01–F03] The rest of this document is the proposed verification and integration plan for this repository, not a claim that its current build already implements it.

## 1. Evaluate, do not guess, the build closure

Inspect the effective GenerateDocumentationFile, DocumentationFile, project references, package references, analyzer inputs and AdditionalFiles of Web and all projects declaring exposed DTOs or actual handlers. Include live Components/FileTools sibling resolution. Record exactly which project emits each needed XML file.

Set XML generation at deliberate API-owning project boundaries or a reviewed opt-in build convention. Verify Debug/Release and publish behavior; never rely on stale XML in bin/obj or a local developer-only path. Avoid toggling a repository-wide documentation warning policy that creates unrelated work.

## 2. Keep the source generator attached

Use the supported AddOpenApi registration at the real application build site. Do not replace the observed options overload with a dynamic document-name wrapper without testing interception. Inspect the generated code or a sentinel description in the final JSON to demonstrate that XML transformation was registered.

For referenced projects/packages, verify their documentation is available to generation. Use deliberate compile-time XML inputs if required. Do not build a runtime directory scan over arbitrary XML files as the default solution. Ensure sibling source and package mode assumptions are explicit; no package snapshot may silently replace the live source the product normally builds.

## 3. Document actual handlers

Convert selected inline lambdas to named handlers first. Preserve route names, tags, filters, return type behavior, service/parameter binding attributes, cancellation, custom JSON readers and all existing endpoint metadata. Validate the representative endpoint before converting an entire file.

A XML summary on MapCrmHrApi or MapProjectStructureAgentApi will not describe every route it registers. Do not add summaries to unused wrapper methods just to satisfy a source scan.

## 4. Prove DTO/property propagation

Create a test matrix with a property description on each of:

- A same-assembly ordinary DTO property.
- An internal wire DTO.
- A positional record property documented through its constructor parameter.
- A DTO in Workbench and a nested DTO in a different referenced project.
- A nullable reference/value type, a collection item and a generic page item.
- An inherited member and an enum/value-object wrapper.
- A schema property produced/replaced by an existing custom transformer.

For positional records and inherited external docs, inspect emitted XML and final schema behavior before choosing a pattern. If the chosen generator cannot map a particular property's XML, use a narrowly justified symbol/JsonTypeInfo-aware enrichment path consuming the same canonical XML. Do not infer member identity solely from a short schema name or concatenate arbitrary class/property names: collisions and JSON aliases matter.

A type description does not automatically describe its use as `currentExecution`, `proposedExecution` or another role. Preserve per-property descriptions at reference sites where role semantics differ.

## 5. Preserve custom transformation semantics

P17 replaces some request properties and rewrites content metadata; P16 produces inline schemas. Inspect all these paths after XML enrichment and after final reference extraction. Test that valid existing descriptions survive and that new property descriptions are not overwritten.

Do not introduce an unqualified blanket transformer that fills every blank from a type name. If two layers set a description, state the owner and precedence and test it. Keep structural metadata such as required sets, union alternatives, constraints and media types unchanged unless repairing a verified mismatch.

## 6. Declare correct response shapes

Where generic IResult/anonymous branches obscure schema inference, use correct existing typed result metadata or minimal response contract typing with unchanged serialized shape. XML `<response>` text is not enough to establish an absent response schema/status. Add tests for success, validation and relevant conflict/not-found/authorization outcomes. Never declare ProblemDetails if the route sends another envelope.

For manually streamed or binary outputs, document the real content type, event/stream contract, response headers and completion behavior. Do not create a JSON DTO pretending to be file bytes or a successful terminal event that the transport does not send.

## 7. Document generation and the UI independently

After a clean build, capture both `/openapi/v1.json` and `/swagger/v1/swagger.json` from the same source-identified host. Compare content and record any expected deterministic normalization separately. Check the generated document's version, references and description coverage.

Then open `/swagger` with Playwright: expand a documented route, an `[AsParameters]` input, a request-body object, referenced nested models and a response/error model. Prove visible, helpful descriptions, not just an HTTP 200 from the JSON route. Retain selected screenshots and inspect them. No renderer replacement is needed merely to show existing OpenAPI descriptions.

## Success conditions

The pipeline must work from clean publish output without development-only source files, absolute paths or old bin/obj state. Coverage must target the real exposed graph, not only public C# declarations. A future documentation change should alter the generated description through the canonical source; a missing comment should fail a focused guard rather than silently disappear.
