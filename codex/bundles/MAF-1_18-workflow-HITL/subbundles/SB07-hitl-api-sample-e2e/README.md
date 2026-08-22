# SB07 — Standalone Workflow HITL API Sample and Browser E2E

## Status

Implemented and technically validated — `GOVERNED_PROOF_INCOMPLETE`; final hashes and validators
pass, but no authentic failing-first test artifact exists

## Outcome

Deliver a small standalone Blazor SSR client that provisions and runs a real personalized
hobby workflow through the public CanDoItAll API, receives HumanInput attention through SSE,
answers through the governed response endpoint, searches a local file-backed SimWiki at most
three times, and terminates with a personalized article or explicit not-found message.

## Owned requirements

RQ-046 through RQ-054.

## Non-goals

- refactoring the CanDoItAll collaboration page;
- adding a component library or interactive Blazor render mode;
- introducing another response mutation route;
- exposing raw workflow request/checkpoint/policy data;
- redesigning the persistence model, changing native checkpoint identity semantics, or
  expanding arbitrary-effect guarantees;
- production-grade persistence or multi-user hosting for the sample.

## Prerequisites

- SB05 public API and SB06 E2E foundation remain Proven as historical frozen evidence.
- Current-source in-memory persistence, continuation-event publication, response outcome
  mapping, PostgreSQL replay serialization, and native checkpoint-link uniqueness repairs
  pass the expanded focused selectors. `BG-SB07-01` is retained as invalidated by the final
  persistence/schema changes; replacement gate `BG-SB07-02` owns current-source closure.
- Live CanDoItAll Web, PostgreSQL, and Ollama services are reachable.
- An authenticated token with the exact response scope can be provisioned without placing it
  in browser code or durable proof.

## Reopen triggers

- live API shape contradicts the typed client contract;
- the workflow cannot resume a native HumanInput or governed HTTP-read approval;
- safe presentation requires exposing protected request context;
- the sample requires a source-project reference or browser-visible bearer token;
- focused or Playwright proof contradicts SB05/SB06 guarantees.

## Exact sources and discovery

- `src/App/CanDoItAll.Web/Api/WorkflowApiSafeProjection.cs`
- `src/App/CanDoItAll.Web/Api/WorkflowExternalResponseApiContracts.cs`
- `src/App/CanDoItAll.Web/Api/WorkflowRunEventsApi.cs`
- `src/App/CanDoItAll.Web/Api/WorkflowRunReadEndpoints.cs`
- existing API contract/integration tests
- standalone sample solution under `C:\programovani\dotnet\candoitall-sample-hitl-api`

## Implementation boundary

The sample owns its UI, files, HTTP endpoints, typed upstream client, provisioning, SSE proxy,
and process-local conversation state. Demonstrated product repairs add the bounded Web API
projection, provider-aware mutation mechanics for the already-supported InMemory profile,
committed-event publication after response continuation, truthful terminal outcome mapping,
request-row serialization for PostgreSQL response replay, and a filtered unique native
checkpoint-link tuple. No new product project/reference, endpoint family, or mutation service
was added. The additive migration
`20260822013043_AddWorkflowNativeCheckpointRequestUniqueness` preflights legacy duplicates and
fails visibly rather than deleting or rewriting them.

## Acceptance criteria

1. The sample builds on the repository-selected .NET 10 SDK and serves a static SSR page with
   pure HTML/CSS/JavaScript behavior.
2. Exactly twenty usable hobby article files are loaded, listed, read, and searched through
   typed SimWiki endpoints; malformed/duplicate files fail startup visibly.
3. Name submission starts an idempotent run of a stably resolved published definition and the
   browser displays the LLM greeting/hobby question.
4. Hobby submission answers the current native HumanInput request with its exact version and
   a stable idempotency key.
5. The server proxies authenticated run-specific SSE, handles `stream.gap` through canonical
   reload, and does not use a polling fallback.
6. Governed `http.fetch` approval waits are distinguished from HumanInput and answered through
   the existing response API; localhost access is explicitly scoped to the sample definition.
7. The workflow performs at most three searches and exposes direct-hit, later-hit, and explicit
   three-miss terminal outcomes through safe artifact readback.
8. Pending HumanInput presentation includes only a bounded prompt and response contract. Raw
   request context, policy, checkpoint data, credentials, and executor arguments remain absent.
9. Focused unit/HTTP tests and Release builds pass with non-zero discovered counts.
10. Playwright completes three varied conversations with clean page/console state and captured
    sanitized proof.

## Proof tier

Governed

## C# Architecture Impact

### Boundary Ownership

Standalone sample services own sample concerns. CanDoItAll Web owns safe transport projection.
Runtime owns continuation/event publication and Modules.AgentFramework owns provider-specific
persistence mechanics. The persistence module also owns the additive PostgreSQL uniqueness
migration and exact-constraint conflict classification. MAF adapter, authorization, native
identity meaning, and public mutation-route ownership are unchanged.

### Dependency Direction

The sample consumes CanDoItAll only through HTTP JSON/SSE and has no source-project reference.
The optional product edit stays Web-to-public-model; Web must not depend on EF entities or MAF
implementation types.

### Pattern Decision

Use a typed client façade for the real external HTTP boundary, a process-local conversation
coordinator for orchestration, and an unrolled workflow for the bounded retry. Do not add a
repository/service abstraction around trivial file reads or one-use presentation helpers.

### Testability Contract

Search scoring, file validation, JSON/result parsing, SSE parsing, and conversation transitions
must be directly testable. The safe projection needs direct mapper and HTTP serialization tests.
The real service/browser path remains required because mocks cannot prove API/SSE compatibility.

### Partial Class Policy

No new handwritten production partial class is allowed. Razor-generated partials are framework
artifacts and are not an ownership-splitting mechanism.

### Architecture Proof Required

- no new CanDoItAll project/reference or cycle;
- no product manager/facade growth outside the bounded Web mapper/DTO;
- no raw `RequestJson`, checkpoint, policy, or bearer value in public/browser output;
- no duplicate mutation endpoint or direct runtime-manager response call;
- sample has no project reference into the CanDoItAll checkout;
- direct tests cover every extracted decision-bearing collaborator.

## Focused validation

Follow `proof/VALIDATION-PLAN.md`, capture exact commands/counts under `proof/SB07`, and use
Playwright for direct-hit, retry-hit, and three-miss journeys. Treat any unexpected 2xx/empty
unknown-run response, malformed SSE frame, approval self-bypass, or hidden fallback as a defect.

## Invalidation keys

IK-13, IK-16, IK-17, IK-19 through IK-23.

## Broad-gate decision

The historical FG-01 remains immutable and is not relabeled as current proof. `BG-SB07-01`
records the first proportionate Wave C gate but is invalidated by the later replay-lock and
native-link schema repairs. It remains audit history and is not reused as current-source proof.

Replacement `BG-SB07-02` covers the standalone Release build and 61/61 sample tests; isolated
Release builds of Web, Unit, Integration, and the PostgreSQL migrations project at 0W/0E;
71/71 focused Unit tests; 64/64 focused Integration tests spanning the 50 Web-boundary and 14
PostgreSQL recovery facts; and three terminal Playwright journeys. The once-only full
Integration-project run is the named broad invalidation gate because a shared persistence
schema/migration changed. It passed 982/983 with zero failures and one declared opt-in live
Ollama catalog skip after 1h24m. The post-fix technical verifier, final hashes, and validators pass,
but the Governed failing-first requirement is unsatisfied. Historical FG-01 was
not rerun.

## Closure record

Implementation and focused technical validation are complete. Evidence under `proof/SB07` records the
standalone Release build and four affected product Release builds at 0W/0E; raw TRXs record
sample 61/61, Unit 71/71, and focused Integration 64/64; JavaScript syntax is valid; and three
Playwright conversations prove a first-attempt hit, a second-attempt hit, and an explicit
three-search miss through one run-specific EventSource per conversation. Canonical API
readbacks, response chains, safety/anti-stub scans, inspected desktop screenshots, and a
frozen-source final terminal run pass.

Authoritative PostgreSQL tests prove replay waits behind a request-row lock before incrementing
its replay state and prove two same-session native links cannot both commit. The independent
race review is Pass with follow-up. The later standalone review found configuration,
transport-reconciliation, operation-lifecycle, SSE-reconnect, and URI-redaction gaps; all are
repaired and the post-fix review passes. No durable failing-first test artifact exists; the
pre-fix findings are review evidence and the replay exception was a console-only reproduction.
No runtime workspace,
credential, connection material, raw request context, or checkpoint payload is admitted as
proof. BG-SB07-02 remains Pass for its product gate, while SHA-256 freeze and Governed closure
remain incomplete. SB07 is Implemented, not Proven.

## Residual risks

- The migration intentionally fails on pre-existing duplicate native tuples; operators must
  remediate such data before applying it. No dirty-data migration fixture or runbook is included.
- Link-conflict callers must roll back the PostgreSQL transaction after the known unique
  violation; both current callers do so, and future callers must preserve that contract.
- The replay test races lease renewal through the shared locked path. Other state mutations use
  the same request-before-operation lock order but are not each exercised as separate races.
- The InMemory mutation semaphore is process-local and remains test/development behavior, not
  evidence of multi-host atomicity.
