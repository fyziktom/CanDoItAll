# Validation and acceptance plan

## Four independent proof layers

1. **Source:** meaningful XML comments exist on actual handlers and exposed DTO members, using adopted terms.
2. **Document:** the generated OpenAPI has those descriptions at the correct operation/property/reference locations and correctly represents the runtime wire contract.
3. **Rendered UI:** Swagger displays the descriptions and examples when an integrator expands operations and models.
4. **Consumer behavior:** an independent raw-JSON client can execute the documented sequence and understand the response/errors.

Passing a build or adding XML comments proves none of the later layers automatically. A source-only review of this pack is not an API acceptance run.

## Focused development loop

First establish baseline source/build identity and a local document export. Add one vertical slice and targeted pipeline tests. Use current repository test entry points, exact/bounded filters and `--list-tests` with expected discovery before execution. Test assemblies must be rebuilt for the source being claimed; no stale `--no-build` evidence.

Annotation batches may be grouped by domain and shared DTO closure. If a shared DTO or transformer changes, include affected consumers. Do not run the entire suite for each batch. Name any real broad-gate trigger such as Directory.Build.*, root graph, cross-cutting registration, serializer behavior or shared test infrastructure. Perform a broad final check once at the stable named checkpoint when repository policy requires it.

## XML/export tests

Use unique meaningful sentinel descriptions in test-owned types to prove exact propagation. Cover named handlers, AsParameters, internal wire records, positional record properties, cross-project DTOs, JsonPropertyName aliases, nullable/required combinations, generics, inheritance, enums/value wrappers and custom schema replacements. A class-level description cannot substitute for a field-role description.

Confirm the current AddOpenApi overload is intercepted/configured, evaluated XML output is present and all required reference XML sources are included. Repeat from clean publish output. Keep serializer property naming, required sets, defaults, constraints and JSON null handling stable unless a metadata mismatch is deliberately repaired.

Add a negative test: delete/blank a required description in a generated test document and ensure the guard fails at the correct pointer. Detect broken refs and duplicate operation IDs. Do not introduce permanent quotas on the number of DTOs or components.

## Semantic fixture set

| Fixture | Acceptance |
|---|---|
| Canonical task update | Plain IDs, exact preconditions/admission, explicit nullable cost basis, valid reschedule and negative cases work over actual HTTP |
| Effort quantities | Hours remain hours with ManDays display unit; examples do not double-convert |
| CRM query | Declared page origin/default/limit and filters match actual query behavior; synthetic empty/normal pages |
| CRM relationships | Documented full replacement and empty-list behavior match the owner; no mistaken patch example |
| Simple Chat DTO | Definition vs conversation vs turn/message/transcript/concurrency identities remain distinct; null and enum representations correct |
| Errors | Common errors-array, Structure error object and any other family-specific forms map to actual output |
| Custom shared-provider schema | Existing subset restrictions preserved; nested field descriptions survive final transforms; unsupported upstream fields not advertised |
| Dynamic JSON / file / events | Correct wire type/content type and unsupported capabilities stated; no invented JSON envelope for streams |
| Security | Swagger claims follow current endpoint/group policies; tool availability never grants HTTP authority |

For a DTO with changed metadata, run positive and negative payloads that exercise the claimed binding rules. InMemory test providers do not prove PostgreSQL transactional behavior; use existing appropriate boundary fixtures when asserting persisted outcomes.

## Swagger Playwright scenarios

Start a source-identified host using synthetic data and proper credentials. Do not change production auth for documentation convenience.

Open Swagger and expand a task PUT, a CRM query, a response object and the relevant nested models. Assert actual useful text for current/proposed fields, the nullable-required explanation, owner ID/admission origin and effort unit. Expand a cross-assembly record and a custom-schema object. Check visible model property descriptions, not only operation summaries or an HTTP response from the JSON endpoint.

Use a safe Try-it-out path or equivalent browser raw-JSON action against synthetic state and verify canonical readback. Capture and inspect a few readable screenshots showing the operation and nested model text. Detect page errors and relevant resource failures. Do not suppress every aborted request or every Blazor request globally; scope expected navigation aborts precisely.

A screenshot of a collapsed endpoint list is not proof of field documentation. A model answering 'success' is not required or sufficient for this task: the main acceptance path is the documented HTTP contract, not a broad agent run.

## Structural-diff guard

Compare both:

- **Entry product export vs final product export:** normally prose/examples change, plus explicitly reviewed metadata fixes.
- **Old SharedInfo snapshot vs final export:** may include already completed task-wire repairs and other prior product changes; separate them from this run's scope.

Preserve route/method and existing operation IDs, properties, formats, required sets, enums, union branches, media types, security and actual response semantics. A new schema/status may be a legitimate metadata repair if it matches an already existing runtime response; record and test that distinction. Do not silently approve all changes because the request is called 'documentation'.

The provided helper deliberately preserves schema property names such as `description`, `summary`, `example` and `title`, and it does not remove arbitrary nested keys. It is a review aid, not a semantic compatibility oracle or full OpenAPI validator.

## Final closure report

Report the current real denominators and meaningful coverage status by family; excluded items and reasons; exact symbol/description mapping for the hard cases; glossary adoption; task examples and rejection proofs; actual tests and source revisions; Swagger evidence; approved structural deltas; both document hashes; SharedInfo provenance/validator results; and signed commit IDs for both repositories.

A missing runnable environment, protected port or credentials is a specific blocked proof, not permission to fabricate results. Keep source comments, XML generation, raw document coverage, semantic review, rendered Swagger and SharedInfo synchronization as separate pass/fail/blocked dimensions.
