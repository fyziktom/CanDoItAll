# API documentation writing standard — adoption proposal

## Goal

A developer seeing only an operation and its schemas in Swagger can construct an appropriate request, understand the response and choose the correct next action. IntelliSense and generated OpenAPI must convey the same semantics.

The canonical vocabulary is in `glossary/api-domain-glossary.json`. Its IDs make review comments unambiguous; clients do not need to know those IDs. Do not expose internal architecture jargon where a brief business definition works better.

## Operation description contract

Use a short action-and-object summary, followed by meaningful remarks. Include applicable sections rather than a mandatory wall of boilerplate on every read.

| Question | Required content |
|---|---|
| What does it do? | Domain action, target, whether it reads, creates, updates, replaces, starts, acknowledges or observes |
| When should I use it? | Supported use and nearest confusing alternative |
| What must exist first? | Source operation for IDs, required state, dependencies and owner-read preconditions |
| Under what authority? | Actual HTTP authentication/policies/scopes; extra lifetime/lease requirements separately |
| What do I send? | Path/query/header/body semantics and content type; route/body ID consistency |
| What happens on success? | Response meaning and identity; whether work is complete, accepted, running or committed with warnings |
| What can fail? | Actual status, error envelope, meaningful stable codes and domain causes |
| What do I do next? | Readback/polling/reconciliation; when correction or retry is safe and when it is not |

Do not call an HTTP handler an 'agent tool' because the same application service powers both. Do not describe an accepted asynchronous job as a completed operation. Avoid claims of atomicity or idempotency unless the owner and tests support them.

### Good operation summary

`Update a canonical project task using the state previously read by the caller.`

The longer description should explain the read/modify/write sequence, the exact identity and admission, what current/proposed pairs mean and how a stale edit is rejected. Merely saying 'Updates a task' is insufficient for this complex operation.

## DTO type description contract

State the business role of the object, which request/response it belongs to, the identity it represents and the important cross-field rules. Mention its lifecycle or snapshot semantics where relevant. A nested DTO should make sense when Swagger opens it directly under Schemas.

A class name ending in AgentInput does not require the HTTP description to call the caller an agent. Use 'Task update request' in prose while retaining the existing C# and schema names.

## Serialized property contract

Every owned serialized member gets a real description. Apply this to internal types and generated record properties as well as ordinary public properties.

For a property, determine these facts from the source before writing:

- Business meaning and entity/owner it refers to.
- Wire representation: opaque string key, GUID, enum token, timestamp, date, number, object, JSON string, JSON value, array or map.
- Source of its value: client choice, owner read, capability/options endpoint, calculated output, receipt or transport-generated reference.
- Presence, null, omission, empty and default semantics; do not infer them from one C# annotation alone.
- Units, origin and limits: hours, bytes, percentages, page origin, currency, encoding, bounded counts and accepted normalization.
- Scope and interactions with other fields: matching IDs, current/proposed pairs, discriminators, conditional requirements and collection replacement.
- Visibility/sensitivity and intended request/response direction.

Not every field needs all seven explanations. Every applicable fact that could change client behavior does.

### Concrete examples

**Bad:** `The ID.`  
**Good:** `The string identifier of the canonical task returned in the project structure. It must exactly match taskId in the route; do not send the task title or a GUID wrapper object.`

**Bad:** `Current progress.`  
**Good:** `The progress value from the task state the caller last read. Send -1 for an untracked value or a value from 0 through 100; this is a precondition, not the requested new progress.`

**Bad:** `Optional cost basis.`  
**Good:** `The expected-cost basis from the task state the caller last read. This JSON member is required; send null when the owner returned no basis. Do not omit it or construct a substitute basis.`

**Bad:** `Effort in the selected unit.`  
**Good:** `Expected effort stored in hours. This value remains in hours when expectedEffortUnit is ManDays; the selected unit controls conversion for input and display, not this property's stored unit.`

These examples are supported by P05, P06, P08 and P18. They are writing examples, not an authorization to alter validation or wire shape.

## Enums and discriminated objects

Describe the enum's purpose and the meaning of each supported member. Match actual JSON tokens, casing and whether integers are accepted. A C# ToString result is not proof of a transport token. For flags, distinguish a bitmask from a single choice. For unions, explain the discriminator and each variant's conditional fields.

Do not add a new enum value, a default or a string converter as a documentation shortcut. If an emitted schema contradicts the reader, fix the schema with an explicit metadata-repair test and record the structural delta.

## Dates, quantities and money

State whether a value is an instant, date-only, duration or recurring rule; preserve the endpoint's actual timezone handling. Document inclusive/exclusive bounds only when verified. Do not assume every DateTimeOffset is normalized the same way or invent a global user timezone.

Separate effort from elapsed schedule time and money amounts from rates. Identify rate denominator and currency. A three-letter-format validator is not necessarily an ISO-4217 membership validator. Unknown amount, unavailable source and accepted zero remain distinct.

State whether text limits count bytes, UTF-16 code units or another measure. A body byte cap is not a field character cap.

## Collections and paging

Explain what total counts cover, stable sorting, page origin, defaults, limits and the conditions for obtaining the next page. A cursor is not an integer offset. State whether a failed read differs from an empty collection, and whether null/omission/empty list means preserve, clear or reject for a write.

Do not copy one endpoint family's pagination defaults onto another. Always document required filters and whether tag matching is any/all only after reading the query implementation.

## Errors, security and partial effects

Document each family's actual success and error envelopes. `ApiErrorResponse.Errors` and the Project Structure `error.errorCode` object are not the same schema. Preserve typed failure information without exposing raw exception details or secrets.

A HTTP 4xx/5xx response does not universally prove no write occurred. Explain explicit committed warnings, recovery references, retained identities and safe readback where the owner supplies them. Do not map every thrown exception to 'safe to retry'.

HTTP authentication, bearer scopes, project lifetime admission, lease coordination and runtime tool grants are separate. Preserve redaction, read-only restrictions and disclosure policy. Do not document an authorization bypass as a recommended alternative path.

## Examples and links

Provide complete valid examples for ordinary scenarios and stateful recipes for server-issued preconditions. Generate raw JSON examples with the real serializer or from accepted fixtures, then validate them through HTTP. Never put illustrative ellipses or `${...}` placeholders into an example advertised as executable JSON.

Use synthetic records. Do not publish actual tokens, confidential notes, user filesystem paths or real chat content. A neutral example may contain explicit nulls where required. Validate enum casing and date offsets.

Link related operations and useful terminology, but keep the local description sufficient. No bare 'see documentation' placeholders.

## Canonical XML and exceptions

Use summary/remarks/param/returns/response/example tags as supported by the actual pipeline. Annotate real serializable properties and the actual route handler, not only a registration helper. Document meaningful ToRequest/mapping boundaries where they transform units, IDs or requiredness.

Manual foreign-protocol schemas may need descriptions at their schema-builder owner when no CLR DTO exists. Record that exception and make its source non-duplicative. Framework/external types may use their authoritative XML or a reviewed boundary description; do not edit vendored code casually.

## Review rubric

A reviewer should reject an entry when it could be pasted onto any unrelated DTO unchanged, when it hides a conditional requirement, when its example bypasses an owner read, or when it promises more than the implementation. Read the operation and nested schema in isolation as a new integrator would. Coverage and word counts cannot certify these properties.
