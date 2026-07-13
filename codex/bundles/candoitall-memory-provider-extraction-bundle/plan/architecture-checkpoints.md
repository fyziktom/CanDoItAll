# Reopened C# Architecture Checkpoints

## Plan status

- Status: `COMPLETED - SB40 TERMINAL CHECKPOINT PASSED`
- Historical SB01-SB34 completion does not satisfy these checkpoints.
- Checkpoint A0 is an audit record. A1 authorizes repair implementation against the target boundaries; it does not authorize bundle closure.
- A later checkpoint may not be marked complete using evidence produced before the repair commit unless it is explicitly a baseline artifact.

## Checkpoint table

| ID | Status | Checkpoint | Required exit evidence | Blocks |
| --- | --- | --- | --- | --- |
| A0 | Complete (audit only) | Current-state inventory | `architecture/00-csharp-current-state-inventory.md`, baseline tests, CodeAnalytics snapshot IDs, partial/dependency inventory. | Architecture decision |
| A1 | Complete | Boundary, dependency, pattern, and testability decisions | Architecture files 01-04 and `reviews/csharp-architecture-gate.md`; SB36-SB40 completed against those decisions. | Production repair |
| A2 | Complete | Generic application and selection repair | Fail-closed explicit selection, allowed-provider enforcement, owner authorization, non-partial facade/collaborators, Application DI ownership, SourceGateway abstraction project, source/dependency audits, and 196-pass final Memory aggregate. | Agent integration repair |
| A3 | Complete | Typed agent settings and invocation planning | Typed model/codec/editor, three modes, ordered aliases/bindings, parser/planner negatives, strict visible validation, 46 component tests, and desktop/narrow browser proof. | Runtime fan-out/UI proof |
| A4 | Complete | Agent runtime integration and multi-provider execution | Dedicated `AgentFramework.Memory`, bounded stable-order fan-out, required/optional semantics, sanitized prompt transformation, untrusted-memory framing, 22/22 direct tests, and real contributor-handler-driver-ledger proof. | Transport/external proof |
| A5 | Complete | Transport, profile, and worker integrity | HTTP/MCP collaborators, strict env/header binding, response caps, lossless secret-reference UI mapping, truthful capabilities, and PostgreSQL owner/token-fenced phase leases. | External conformance |
| A6 | Complete | External CognitiveMemory security and protocol conformance | Authn/authz, project isolation, access/redaction policy, limits, truthful manifest, dependency-free Protocol v1 contracts, zero cross-root references, 59/59 isolated tests, and live main-driver process proof. | UI/final system proof |
| A7 | Complete | UI and composition closure | Base composition guards, 46 focused components, and 5/5 desktop/narrow Playwright scenarios for provider and agent memory settings passed. | Final review |
| A8 | Complete | Final architecture and quality gate | Full affected tests, CodeAnalytics/dependency review, live conformance, partial/secret audits, lease disposition, independent red-team review, proof/traceability, and completed-stage validator passed. | Bundle completion/merge |

## A2 - Generic application and selection repair

Required sequence:

1. Add failing tests for `FallbackBehavior.Deny`, agent allowlist enforcement, registry-order independence, and cross-owner operation access.
2. Put allowed provider IDs and explicit selection intent into typed selection inputs.
3. Make implicit fallback impossible unless policy explicitly allows and defines it.
4. Decompose `MemoryOperationHandler` into cohesive top-level handlers behind the compatibility facade.
5. Replace capability partials for event worker and retention projection with cohesive collaborators.
6. Move generic application DI registration to Memory Application; Persistence registers persistence only.
7. Remove the Application-to-AgentFramework dependency by rehoming/adapting source contracts.

Progression gate:

- all A2 focused tests pass;
- no behavior is implemented through a new handwritten partial;
- no forbidden project edge remains in the projects changed by A2;
- status/cancel/feedback authorization fails closed.

## A3 - Settings and invocation planning

Required sequence:

1. Define typed invocation mode/provider binding/alias/failure policy in the Models-owned boundary.
2. Implement strict validation and a documented migration from legacy metadata.
3. Add pure directive parser and invocation planner.
4. Wire workspace catalog load/save and the typed agent editor model.
5. Prove no-provider-call behavior for disabled/explicit-without-directive cases.

Progression gate:

- malformed configuration is visible and cannot silently default;
- aliases resolve deterministically and unknown/disallowed aliases fail;
- settings round-trip through the real catalog;
- no UI or tool class independently reimplements parsing/planning.

## A4 - Runtime integration and multi-provider execution

Required sequence:

1. Establish `CanDoItAll.AgentFramework.Memory` (or a documented equivalent dedicated project) as runtime owner.
2. Map real MAF runtime/context intent to typed memory context shared by tools, contributors, and workflows.
3. Execute immutable provider plans with bounded concurrency/sequential behavior and deterministic merge order.
4. Apply explicit-vs-automatic failure policy with visible diagnostics.
5. Gate or remove legacy workspace memory when generic provider memory is configured.
6. Shrink Module AgentFramework to UI/composition.

Progression gate:

- production composition, not fabricated test tags, supplies identity;
- one agent can use two configured providers and cannot reach a third;
- disabled, automatic, and explicit directive paths have end-to-end behavioral tests;
- Memory Application still executes one provider per request and remains agent-agnostic.

## A5 - Transport/profile/worker integrity

Required sequence:

1. Extract HTTP/MCP request factory, response mapper, and invoker collaborators; delete handwritten driver partials.
2. Propagate complete workspace/execution/policy/budget/owner context.
3. Add explicit production MCP registration and resolution test.
4. Introduce typed driver configuration codecs/editors that preserve unknown extension data and resolve secret references at the transport boundary.
5. Remove or disable capability advertisements without a real execution path.
6. Register real hosted workers only with proven durable processing/lifecycle.

Progression gate:

- project context no longer defaults to `None` when the runtime has a project;
- configuration round-trip is non-destructive and secrets are absent from UI/log evidence;
- HTTP/MCP malformed/auth/timeout/cancellation cases produce typed results;
- there are no `.Requests`, `.Responses`, `.Outbox`, or `.Apply` handwritten partials for the repaired types.

## A6 - External provider conformance

Required sequence:

1. Protect `/memory/*` endpoints with the configured authentication scheme and project authorization.
2. Reject malformed or unauthorized project identity; never reinterpret it as global.
3. Implement and apply the native access/review/redaction policy before candidates leave the application boundary.
4. Make the manifest truthful; remove inert MAF/unsupported route claims.
5. Prove rate/request limits and database migration/readiness.
6. Run the real main HTTP driver against the external service test host.

Progression gate:

- unauthenticated/unauthorized/cross-project calls are rejected;
- restricted/rejected/retired/redacted/review-pending data follows an explicit tested matrix;
- external Service has no dependency on the main HTTP client driver;
- sibling protocol references are reduced or recorded with an owner/removal condition.

## A7 - UI and composition closure

Required sequence:

1. Remove `CanDoItAll.Modules.CognitiveMemory` reference/import/discovery from base composition.
2. Prove zero-provider startup and explicit HTTP/MCP/two-provider profiles.
3. Add a dedicated agent Memory settings component using established BaseLib wrappers.
4. Prove provider editor changes preserve transport configuration.
5. Run component/browser validation and retry Components MCP catalog validation when available.

Progression gate:

- historical `CP001` and `CP002` pass without weakened assertions;
- no implicit native/Qdrant/mock provider starts in zero-provider mode;
- the UI edits the typed model and contains no provider-selection business logic;
- keyboard/validation/error states are included in browser evidence.

## A8 - Final gate

Required evidence:

- sequential full affected builds/tests in both repositories with zero unexpected failures;
- new CodeAnalytics snapshots and dependency/cycle reports;
- architecture tests/anti-stub scans for forbidden edges and partials;
- cross-repository protocol trace and real-agent E2E traces for all invocation modes;
- proof manifest tied to both repository SHAs;
- independent red-team architecture/security review;
- updated requirement/subbundle traceability with no unmatched user requirement.

Closure rule:

`reviews/csharp-architecture-gate.md` may move from `FAIL TO CLOSE` to `PASS` only after A2-A8 evidence exists. A deferral must name the owner, risk, deadline/removal condition, and why it does not invalidate provider choice, agent isolation, or external data safety. Security, project isolation, silent fallback, and base native coupling are not deferrable closure items.
