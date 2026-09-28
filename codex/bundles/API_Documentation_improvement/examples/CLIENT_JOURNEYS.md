# Client journey fixtures to produce and validate

These are source-grounded fixture specifications, not untested runnable payloads. The final implementation must supply complete valid JSON examples derived from the running contract.

## Journey A — update a canonical task from an independent client

Create a synthetic project/task through supported endpoints in a controlled test profile. Read structure including metadata. Extract the returned task node ID, title, progress, start/end, exact task edit state and expectedProjectAdmission. Build an update with unchanged current/proposed values except a permitted schedule change. Explicitly include currentCostBasis, even when null. Use plain string IDs in the body and route. Send literal JSON using JavaScript fetch or a comparable non-domain-object client. Verify canonical readback.

Use P07's CreateReschedule source as evidence of how the product test obtains its edit state, but do not publish a client example that requires ProjectStructureTaskEditStatePolicy or another internal C# helper. The final documentation must expose the actual JSON extraction path and each nested field's meaning.

Retain a machine-readable fixture of the successful request/response, redacted and synthetic, together with its source version. Derive a wrong-ID, missing-member and stale-state example from it and verify actual errors. Do not hard-code an invented admission object.

## Journey B — distinguish identity records and replacement

Create two synthetic party records through the CRM API. Read the response IDs rather than inferring them from names/external codes. Read relationships, prepare the complete intended list and submit the replacement. Verify that its documented empty-list and omission behavior matches the owner. Keep party ID, relationship ID and account/profile concepts distinct.

## Journey C — understand a nested schema directly in Swagger

Open the task input, currentEstimate and currentExecution properties without consulting external Markdown. A developer should be able to explain preconditions, units, nulls and authority from the visible descriptions. Repeat with a Simple Chat conversation to distinguish DefinitionId, DefinitionRevision, TranscriptRevision, EntryId, TurnId and ConcurrencyToken.

## Journey D — verify generated and manually built schema descriptions coexist

Open a shared-provider relay operation and compare the documented subset, casing, content types and constraints with its existing parser/schema owner. Ensure the documentation pass did not accidentally add unsupported upstream fields or erase inline descriptions. Use a deliberately rejected synthetic request only through appropriate authorized test infrastructure.
