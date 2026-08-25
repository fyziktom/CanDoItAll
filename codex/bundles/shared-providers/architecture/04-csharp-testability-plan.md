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

## SB00 realized seams — 2026-08-24

SB00 added the two exact characterization lanes declared by its test-selection contract:

| Lane | Discovery | Result | Seam protected |
| --- | ---: | --- | --- |
| `SharedProviderArchitectureCharacterizationTests` | 8 | 8 passed | canonical EF ownership, dependency direction, connector registration, wire isolation, outer mapping, UI delegation |
| `SharedProviderRuntimePathCharacterizationTests` | 6 | 6 passed | effective OpenAI/Azure/ComfyUI mapping, real manifest registry, integrated gateway precedence, custom-endpoint driver behavior |

Both lanes are deterministic, use no live provider, and preserve the production graph. SB00 also
captured a before/after CodeAnalytics comparison with 11 projects, 23 direct references, and zero
project-level cycles. The test filters and transcripts are owned by the SB00 proof manifest.

The remaining seams in this document are implementation contracts for their owning downstream
subbundles, not claims of already completed behavior. A change to either SB00 test selection,
provider registry, mapper, or project graph reopens the decision lock.

## SB01 realized seams — 2026-08-24

- `SharedProviderProtocolContractTests`: exactly 12 discovered and 12 passed, proving strict
  serialization, canonical revision stability/sensitivity, version rejection, capability
  coherence, base-path preservation, immutable snapshots, and forbidden-field absence.
- `SharedProviderRoutingModelIdTests`: exactly 10 discovered and 10 passed, proving deterministic
  IDs, duplicate-name separation by publication, full-digest shape, strict malformed-input
  rejection, catalog-backed resolution, and private identifier/URI non-disclosure.
- `SharedProviderAccessContextTests`: exactly 10 discovered and 10 passed against the real Web
  host, proving absent/valid/invalid/multiple/oversized handling, default-value rejection,
  concurrent scope isolation, status-page re-execution safety, and that a forged reference does
  not authorize a request.

The owning Unit and Integration solutions build in Release with zero warnings and errors. No
live provider, external network, browser, multi-instance, or broad test lane ran.

## SB02 realized seams — 2026-08-24

| Lane | Discovery | Result | Seam protected |
| --- | ---: | --- | --- |
| `SharedProviderStateModelTests` | 18 | 18 passed | pure publication/source/import/audit transitions, invalid states, stable identity, truthful completeness |
| `SharedProviderPersistenceIntegrationTests` | 14 | 14 passed | real PostgreSQL mapping/migration/uniqueness, multi-context identity, two-profile propagation, rollback, reconciliation, audit, post-commit observers |
| `SharedProviderDeletionReferenceIntegrationTests` | 6 | 6 passed | both production deletion paths, typed references, database `Restrict`, valid unreferenced deletion |

The final stale-token test verifies persisted after-state, and the propagation test uses two
imports with different aliases/enabled intent. This prevents transaction/"all imports" claims
from resting on exception existence or a single-row fixture. Builds, EF pending-model, anti-stub,
and credential/content scans pass. No broad, network, browser, or multi-instance lane ran.

SB04 later extended the SB02-owned invocation schema with operation-aware image usage. That
materially invalidated the original state/persistence proof, so the exact SB02 lanes were rerun
after the schema freeze: 18/18 state, 14/14 PostgreSQL persistence, 6/6 deletion/reference, and
EF pending-model/no-drift all pass. The original SB02 PASS remains historical; its proof contains
the additive downstream-invalidation/restored-trust record and the migration-deployment
assumption.

## SB04 realized seams — 2026-08-25

| Lane | Discovery | Result | Seam protected |
| --- | ---: | --- | --- |
| `SharedProviderRelayPolicyTests` | 24 | 24 passed | exact per-surface wire policy, capability intersections, routing containment, error/usage mapping, and metadata-only transitions |
| `SharedProviderOpenAiCompatibilityIntegrationTests` | 22 | 22 passed | real Web/Workspace/PostgreSQL routing, secret resolution, audit, hosted recovery, image target resolution, and deterministic neutral dispatch |
| `SharedProviderStreamingIntegrationTests` | 12 | 12 passed | incremental SSE, split UTF-8, terminal usage, cancellation/disposal, overall/idle timeout, sanitized transport failure, and safe headers |
| `ProviderUsageAggregationTests` | 7 | 7 passed | image-only usage projection/aggregation and rejection of non-positive or mixed token/image contributions |

The PostgreSQL compatibility lane uses the production Web and Workspace application services,
catalog/secret/current-state persistence, invocation finalization, hosted recovery worker, and
image target resolver. Its dispatcher is a deterministic neutral fake; it is not a live provider
network call. The recovery worker uses a 10-second default startup delay, one-minute default
interval, batch 100 (maximum 1000), and a stale threshold of maximum relay timeout plus five
minutes. The test-only internal schedule uses 100 ms startup/interval and proves that a row older
than maximum timeout plus six minutes is finalized while a row only three minutes beyond the
maximum timeout and terminal rows remain unchanged, polling for at most 20 seconds.

All three owning Release builds complete sequentially with zero warnings/errors. The exact lanes
use no live provider, browser, broad aggregate, or multi-instance deployment.

## SB05 realized seams — 2026-08-25

| Lane | Discovery | Result | Seam protected |
| --- | ---: | --- | --- |
| `SharedProviderSourceUriPolicyTests` | 18 | 18 passed | canonical URI/TLS/private policy, special-use addresses, DNS rebinding, actual named handlers, URI logging, and strict catalog client |
| `SharedProviderReconciliationTests` | 22 | 22 passed | deterministic add/replace/retire/reactivate/missing/refresh plans, stable IDs/local intent, concurrency, and typed outcomes |
| `SharedProviderSourceSyncIntegrationTests` | 16 | 16 passed | real PostgreSQL, secret resolution, HTTP, source lifecycle, ETag, identity pinning, selection, retirement, recovery, and post-commit observers |

The real integration lane proves disable short-circuits before secret/HTTP and synchronization
resumes after re-enable. Replacement selection retains both import/profile rows and retires the
deselected import with the same IDs. Transient/auth/404/schema/trust failures do not mark missing;
conditional requests are suppressed until source and every selected import return to authoritative
state, preventing a stale 304 recovery trap.

Unit and Integration Release builds pass sequentially with zero warnings/errors. No broad, browser,
multi-instance, runtime-connector, UI, paid-provider, or live-provider lane ran.

## SB06 realized seams — 2026-08-25

| Lane | Discovery | Result | Seam protected |
| --- | ---: | --- | --- |
| `SharedProviderRuntimeProfileMaterializerTests` | 18 | 18 passed | graph identity, availability, purpose/transport, cached integrity, capability intersection, and safe projection |
| `SharedProviderRuntimeProjectionIntegrationTests` | 16 | 16 passed | real PostgreSQL loading, catalog projection, raw OpenAI and MAF SDK routing, exact models, hardened clients, context isolation, and source-audio exclusion |
| `SharedProviderHybridSelectionTests` | 10 | 10 passed | personal/shared coexistence, explicit selection, stable identity, unavailable retention, and no fallback |

The post-repair Unit and Integration builds pass sequentially with zero warnings/errors. Supporting
seams pass feature/audio policy and UI selection 16/16, concrete drivers 54/54, and personal voice
behavior 29/29. Source-managed speech-to-text and text-to-speech fail before credential resolution
or HTTP dispatch, while an explicit ineligible persisted voice ID stays unselected. No broad,
browser, Playwright, multi-instance, paid-provider, or live-provider lane ran.
