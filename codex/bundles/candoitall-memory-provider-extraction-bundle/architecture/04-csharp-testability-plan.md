# C# Testability Plan

## Purpose and status

This plan defines the seams and adversarial proof required before the reopened bundle can close. Passing the historical tests is not sufficient because several tests construct runtime tags/contexts that production does not provide.

All failure paths must return typed results or deliberate exceptions at configuration boundaries. Tests must not validate silent fallback.

## Required isolated seams

| Seam | Direct test subject | Required substitutes |
| --- | --- | --- |
| Settings codec/migration | Parse, validate, serialize, migrate legacy memory settings. | JSON strings and immutable typed fixtures only. |
| Directive parser | Extract exact aliases and query text according to the command grammar. | None. |
| Invocation planner | Settings + parsed directives -> immutable provider plan or typed rejection. | Immutable provider-binding fixtures. |
| Provider selector | Selection policy + catalog -> exact provider/rejection. | In-memory catalog entries; no DI container. |
| Multi-provider orchestrator | Provider plan -> calls/diagnostics/provider-labelled merged context. | Fake one-provider operation client/handler and deterministic scheduler if concurrency is used. |
| Runtime context mapper | MAF runtime/context intent -> protocol workspace/execution/owner context. | Real context records/builders, not string tag dictionaries alone. |
| Query handler | Authorization, explicit selection, invocation, persistence, typed exception translation. | Driver, store, selector, authorization policy, clock/id generator only when observable. |
| Operation control policy | Status/cancel authorization by provider/requester/agent/session/workflow ownership. | Recorded operation fixtures. |
| HTTP request/response mapping | Complete protocol context and error mapping. | DTO fixtures; fake `HttpMessageHandler` for I/O behavior. |
| MCP request/response mapping | Complete protocol context/tool mapping and error mapping. | Fake MCP client port. |
| Provider configuration codec | Safe round-trip and validation. | JSON extension fixtures and fake credential reference; never real secrets. |
| External access policy | Record review/access/redaction plus actor/project context -> allow/deny/redact. | Domain fixtures; no web host. |
| External API authorization | Endpoint authentication, project claim enforcement, request/rate limits. | `WebApplicationFactory`, test authentication scheme, isolated database. |

If an extracted class cannot be tested without constructing the original broad module/service graph, the extraction has not established a useful boundary.

## Invocation-mode matrix

| Mode | Prompt | Expected calls |
| --- | --- | --- |
| `Disabled` | No directive | No provider call and no legacy workspace-memory attachment. |
| `Disabled` | `/mem:memory1` | No provider call; typed `MemoryDisabled` rejection visible to the caller. |
| `ExplicitDirective` | No directive | No provider call. |
| `ExplicitDirective` | `/mem:memory1` | Invoke only the provider bound to `memory1`. |
| `ExplicitDirective` | `/mem:memory1 /mem:memory2` | Invoke both in deterministic binding/directive order and label both results. |
| `Automatic` | No directive | Invoke only bindings marked for automatic context. |
| `Automatic` | `/mem:memory2` | Use the explicit provider set for this turn; do not also add every automatic provider unless the settings model explicitly defines additive behavior and tests prove it. |
| Any enabled mode | Unknown/ambiguous/disallowed alias | No guessed provider; typed rejection. |

The parser suite must include casing, leading/trailing whitespace, multiple directives, duplicated directives, invalid alias characters, `/mem:` with no alias, a directive inside quoted/code text, similar text such as `/memory:`, and normal prose containing the token. The accepted grammar and whether directives remain in the provider query must be asserted, not inferred.

## Provider-selection negative tests

- `FallbackBehavior.Deny` with no explicit/assigned/default provider returns `NoProviderSelected` and makes zero driver calls.
- An agent allowlist excludes an otherwise default or first-compatible provider.
- A requested provider outside the allowlist is rejected before health/driver invocation.
- Disabled, unhealthy, capability-incompatible, workspace-incompatible, and unknown providers have distinct typed outcomes where the public contract distinguishes them.
- Registry insertion order never changes the selected provider.
- Duplicate aliases and duplicate provider instance bindings are rejected during settings validation or have an explicit documented rule.
- A configured two-provider agent cannot leak a third registered provider into its plan.
- Provider selection never falls back from an auth/configuration failure to a different provider unless an explicit policy names that behavior and the audit record captures it.

## Multi-provider behavior tests

- Two successful providers receive the same typed requester/workspace/execution context and produce deterministic provider-labelled sections.
- Cancellation reaches every active call and no merge occurs after cancellation.
- Timeout/error diagnostics identify the alias and provider ID while masking endpoint credentials and source content.
- Explicitly requested provider failure fails that explicit request; a success from another provider cannot hide it.
- Automatic best-effort behavior is exercised only when the typed settings explicitly enable it, and failures appear in typed diagnostics/logs.
- Parallel implementation, if chosen, is bounded and merge order remains the plan order rather than completion order.
- Duplicate memory from the legacy workspace provider is absent when generic provider memory is configured.

## Identity, ownership, and isolation tests

- Production `RuntimeCapabilityComposer` supplies the same typed identity asserted by tool and contributor integration tests; tests do not add undocumented tags.
- Project/workspace, workflow/run/node, process/run/step, agent, requester, session, correlation, policy, and budget fields round-trip through HTTP and MCP factories.
- Missing project scope remains explicitly absent; malformed project input is rejected and cannot map to global memory.
- A caller authorized for Project A cannot query Project B by changing the envelope.
- An agent/session cannot read status, cancel, or submit feedback for another owner operation even when it knows the operation GUID.
- A recorded operation remains bound to its original provider; status does not report a false success after a new selection failure.

## Configuration and shallow-implementation tests

These tests are designed to catch code that compiles but does not implement the feature:

- Agent editor load/save/reload round-trips mode, provider aliases/IDs, automatic flags, capabilities, scopes, and failure policy through the real workspace catalog.
- Malformed settings JSON surfaces a validation error; it does not return default settings.
- Editing one common provider field preserves endpoint, tool map, auth reference, selection tags, and unknown extension fields.
- UI/view models and captured logs do not contain raw secret values.
- MCP registration resolves a configured MCP driver through production composition, not only a test service collection.
- Every advertised manifest capability has an executable contract test. Unsupported feedback/event/source/async capabilities are absent or return typed `Unsupported` before work is accepted.
- Hosted event/ingestion/outbox tests prove start, cancellation, lease/idempotency/retry, and durable completion. If those tests do not exist, the capability remains disabled.
- Reflection/source architecture tests reject new handwritten partial declarations outside the allowed categories and reject the old partial file names.
- Facade tests assert delegation, while collaborator tests assert behavior. A source scan showing smaller files is not behavioral proof.

## External Cognitive Memory policy tests

- Unauthenticated `/memory/*` calls return `401`; callers without the project claim/policy return `403`; health liveness is the only deliberately public endpoint unless documented otherwise.
- The API key/credential mechanism sent by the main driver is actually validated, and invalid/expired/missing credentials fail predictably.
- Approved/public/project-safe records can be recalled only in authorized scope.
- rejected, retired, restricted, redacted, or human-review-pending records are denied/redacted according to an explicit policy matrix before response mapping.
- Actor, agent, requester, tenant, session, and project data cannot be forged by the envelope when authenticated claims disagree.
- Rate limit and request-size tests cover the memory endpoints.
- PostgreSQL startup/readiness verifies migrations/schema; in-memory service and worker share the configured store in integration tests.
- Manifest tests assert only implemented capabilities and routes.

## Cross-layer test sequence

1. Failing-first characterization tests for fallback, directive absence, context loss, ownership, composition native reference, external auth/access, and extension-data loss.
2. Direct unit tests for settings/parser/planner/selector/mapper/handlers/mappers/policies.
3. Memory project test suite.
4. Focused AgentFramework/MAF unit and component tests.
5. External CognitiveMemory domain/application/service tests.
6. Cross-repository contract test using the real main `HttpMemoryProviderDriver` against an external `WebApplicationFactory` host.
7. Composition smoke tests: zero provider, HTTP only, MCP only, and two providers.
8. Real-agent end-to-end tests for automatic and explicit modes, including two-provider and denied-provider scenarios.
9. Browser/component proof of agent settings and provider configuration using the persistent app/watch loop if UI is changed.
10. CodeAnalytics/dependency/partial-class gates and full affected solution test runs.

Run builds/tests that share output directories sequentially to avoid the file-lock interference observed during the baseline audit.

## Baseline and exit targets

| Scope | Baseline | Exit target |
| --- | --- | --- |
| Main memory tests | 98 pass, 2 architecture failures (`CP001`, `CP002`). | All pass; failures removed by deleting base native references, not by weakening assertions. |
| Focused memory AgentFramework/MAF tests | 45 pass. | Existing tests pass plus production-context, invocation-mode, directive, multi-provider, and legacy-duplication tests. |
| External CognitiveMemory tests | 28 pass. | Existing tests pass plus auth/access/project isolation/manifest/cross-driver integration. |
| CodeAnalytics | No project cycle, but forbidden edges/hot spots remain. | No project cycles, no forbidden edges, no handwritten capability partials, bounded dependency fan-in/fan-out with documented exceptions. |
| Components catalog validation | MCP transport unavailable. | Re-run Components MCP when available, or record a named owner/date and prove UI exclusively reuses locally evidenced BaseLib components before closure exception is accepted. |

## Proof artifacts required

- Exact command transcripts with exit codes and commit SHA for both repositories.
- Test result files or untruncated summaries naming all new suites.
- Dependency reference before/after output and CodeAnalytics snapshot IDs.
- Anti-stub scans for forbidden partials, native references, plaintext secret fields, and unsupported manifest capabilities.
- Browser screenshots/interaction transcript for agent memory settings and provider transport editing.
- Cross-repository request/response trace with sensitive values masked.
- An independent architecture review after implementation; the author of the refactor cannot be the only reviewer.

