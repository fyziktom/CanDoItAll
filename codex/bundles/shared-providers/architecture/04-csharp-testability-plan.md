# C# testability plan

Required by the C# architecture bundle guard.

## Seams

| Seam | Test double | Real integration proof |
| --- | --- | --- |
| catalog representation | deterministic profile/publication builders | PostgreSQL HTTP integration |
| routing ID codec | pure unit vectors | model list plus invocation |
| access-context parser | table-driven values | middleware/header integration |
| publication policy | fake adapter descriptors | registered connector composition |
| source catalog client | scripted HTTP handler | central/client Docker HTTP |
| relay adapter | deterministic upstream HTTP server | upstream fixture container |
| streaming | scripted chunked/SSE stream | three-instance SSE |
| reconciliation | fake catalog snapshot + real clock | PostgreSQL sync and two clients |
| secret containment | fake resolver + serializer scan | Docker upstream capture/log scan |
| usage extraction | fixed JSON/SSE fixtures | invocation record after E2E |
| runtime projection | fake source/import/profile rows | local MAF call through central |
| UI view model | fake application service | Playwright against real instances |

## Test layers

### Pure unit tests

- value objects and normalization;
- routing ID codec collision/invalid input;
- public projection allowlist;
- capability intersection;
- supported-field validators;
- URI/network policy;
- sync state machine;
- error mapping;
- usage extraction/completeness;
- no-content audit mapping.

### PostgreSQL integration

- entity mappings, indexes, concurrency, migrations;
- publication/profile relationship;
- source/import uniqueness;
- reconciliation transaction and stable local ID;
- source identity mismatch;
- deletion/reference behavior;
- invocation record persistence/retention.

### Real Web host integration

- authorization scopes;
- catalog ETag/304;
- OpenAI/native error envelopes;
- model routing;
- body limits;
- streaming headers/chunks/cancellation;
- access context;
- OpenAPI operation/schema presence;
- redaction.

### Composition smoke

- adapter registry contains supported production connectors;
- mocks excluded from production publication;
- Workspace resolves abstraction while Composition supplies Http implementation;
- no missing DI service at startup.

### Three-instance Docker

- central, client-a, client-b each have independent database/data root;
- one deterministic upstream;
- selected import on each client;
- hybrid personal/shared profile;
- repeated sync;
- unpublish/outage/recovery;
- streaming/tools/structured/image;
- access context captured central and absent upstream;
- final running stack.

### UI

- bUnit/component tests for state and ownership;
- focused Playwright at the supported desktop viewport;
- screenshot inspection of normal and open-overlay states.

## Negative proof requirements

Every capability requires at least one meaningful failure:

- secret field cannot serialize;
- unpublished route cannot invoke;
- wrong scope denied;
- malformed/oversized access ref rejected;
- unsupported field rejected;
- built-in tool rejected;
- caller-controlled upstream URI ignored/rejected;
- model ID from another publication cannot cross-route;
- source identity mismatch blocks sync;
- outage does not delete or fall back;
- stream cancellation reaches upstream;
- imported remote-owned fields cannot be changed;
- zero usage is not substituted for missing usage.

## Determinism

- fixed clocks and GUID sources in unit/application tests;
- canonical JSON serialization for ETags;
- deterministic upstream responses and SSE chunks;
- no random test ordering assumptions;
- generated E2E credentials stored only in ignored artifacts;
- no external network dependency.
