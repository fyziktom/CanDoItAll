# C# Architecture Gate - Reopened Repair

## Decision

- Review date: `2026-07-12`
- Bundle state: `COMPLETED`
- Decision to begin repair implementation: `PASS`
- Decision to enter SB40 terminal validation: `PASS`
- Decision to close bundle or merge as architecture-complete: `PASS WITH EXPLICIT NON-BLOCKING FOLLOW-UPS`

The initial SB35 readiness decision below is preserved as the repair baseline. SB40 now supplies the full-suite, browser, real agent seam, live external process, distributed-lease, CodeAnalytics/static, and red-team evidence required for final acceptance.

## Review basis

- Main repository CodeAnalytics snapshots: `snap-20260712092446-ac8ce1a7`, `snap-20260712092631-7f3b2a52`.
- External repository snapshot: `snap-20260712092333-cdf936e2`.
- Main memory tests: 98 passed, 2 failed (`CP001`, `CP002` base-composition dependency guards).
- Focused AgentFramework memory/MAF tests: 45 passed, with a production-context coverage gap.
- External CognitiveMemory tests: 28 passed, without authentication/access/project/cross-driver coverage.
- Source/project/dependency/partial-class inspection in both repositories.
- Components MCP transport was unavailable; local BaseLib usage was inspected, but catalog validation remains open.

## Blocking findings

### [P0] External recall boundary is not authorized

The native service accepts memory requests without effective endpoint authentication/authorization and does not apply the declared cognitive-memory access policy before returning candidates. Project identity supplied by the caller is not safely bound to authenticated claims, and malformed project identity can fall toward global behavior.

Required correction:

- authenticate `/memory/*` calls using the same credential contract configured in the main driver;
- authorize project/tenant/actor scope from trusted identity;
- reject malformed/mismatched scope;
- apply explicit review/access/redaction policy before result mapping;
- add negative cross-project and restricted-data tests.

This finding is not deferrable because external memory may contain sensitive agent/user/project data.

### [P0] Provider selection can violate explicit agent intent

The registry can choose the first enabled compatible provider without honoring deny-fallback semantics, and the selection boundary does not enforce the complete agent provider allowlist. `/mem:<alias>` and invocation modes do not exist.

Required correction:

- enforce allowed provider IDs and fallback policy inside typed selection;
- introduce typed settings, provider aliases, strict validation, directive parser, and invocation planner;
- add zero-call negative tests and deterministic multi-provider behavior;
- never change providers after auth/configuration failure unless an explicit tested policy allows it.

### [P1] Runtime identity is incomplete and differs between tests and production

The tool provider reads magic memory tags that production composition does not populate. Context contribution and transport request mapping lose session/workflow/process/project identity; HTTP/MCP factories manufacture empty project context.

Required correction:

- map the real MAF runtime/context intent through one typed boundary;
- carry the same identity through tool, contributor, workflow, operation ownership, HTTP, MCP, and external authorization;
- prove the production composition path and reject malformed scope.

### [P1] Operation lifecycle lacks ownership authorization

Status/cancel operations are addressed by GUID without a fail-closed requester/agent/session/workflow owner check. A fresh selection outcome can also be confused with the recorded provider operation state.

Required correction:

- record immutable operation owner/provider context;
- authorize status/cancel/feedback before disclosure or mutation;
- test cross-agent/session/workflow denial and original-provider binding.

### [P1] Generic/agent/application ownership is reversed or blurred

`Memory.Application` depends on `AgentFramework.Core`; reusable runtime integration lives inside a broad Razor module; Persistence registers application services; base composition still imports the removed native module.

Required correction:

- remove `Memory.Application -> AgentFramework.Core` by rehoming generic source contracts and adapting MAF outward;
- create a dedicated AgentFramework Memory integration owner;
- move application DI to Application and keep Persistence registration local;
- remove native module reference/discovery from base composition.

### [P1] Capability-grouping partials conceal god services

Application handler, HTTP/MCP drivers, event worker, retention store, and catalog memory behavior use handwritten partials for capability grouping. Large tool/context/workflow/settings/UI services combine policy, mapping, I/O, and orchestration.

Required correction:

- use cohesive top-level handlers/mappers/invokers/planners/policies with narrow constructors;
- preserve a facade only for compatibility;
- add a source/architecture guard against new prohibited partials;
- prove behavior directly at the extracted seams.

### [P1] Manifests/composition advertise unproven behavior

MCP is not wired through production composition. Event/ingestion/outbox workers have no verified hosted execution path. External feedback/source requests return `501`; operation status and UI RCL claims do not represent a working lifecycle.

Required correction:

- wire and contract-test supported transports/features;
- remove or report typed `Unsupported` for everything not implemented;
- require durable lifecycle proof before advertising async/event/ingestion behavior.

### [P1] Provider editing is destructive and secrets are not safely modeled

The provider management service edits only selected common extensions and can strip endpoint/auth/tool-map/selection configuration. Raw API keys can live in extension JSON.

Required correction:

- add typed per-driver configuration codecs/editors with unknown-field preservation;
- store/reference credentials through a secret/environment/credential reference;
- prove round-trip preservation and masked logs/UI.

## Architecture decisions accepted for implementation

1. Retain a generic protocol/driver strategy and one-provider application operation handler.
2. Add a dedicated `CanDoItAll.AgentFramework.Memory` boundary (or documented project-equivalent) for settings-to-invocation planning, multi-provider orchestration, runtime tools, context contribution, and workflow adaptation.
3. Put persisted typed agent memory settings in the Models-owned boundary, with only a dependency on memory abstractions.
4. Treat `/mem:<alias>` as a parsed typed directive, not a magic substring or provider ID.
5. Keep multi-provider fan-out at the agent integration boundary and label/merge results deterministically.
6. Split god partials into cohesive top-level collaborators; do not replace them with a deeper interface hierarchy or more projects without a dependency reason.
7. Use explicit transport request factories/response mappers/invokers and explicit configuration codecs.
8. Make external authentication, project authorization, and access/redaction policy part of the memory operation, not optional middleware documentation.

## Rejected repair shortcuts

- Adding `InvocationMode` while leaving tool/context/workflow paths to interpret it separately.
- Implementing `/mem` only in UI or only in the runtime tool description.
- Calling multiple providers by looping over all registry entries.
- Catching transport/settings exceptions and continuing with another/default provider.
- Merging partial files without extracting responsibilities.
- Moving the existing large classes into a new project unchanged.
- Weakening/removing `CP001`/`CP002` rather than removing the native composition edge.
- Treating a GUID, API key in JSON, or caller-supplied project ID as sufficient authorization.
- Advertising unsupported capabilities to preserve the historical manifest shape.
- Using passing mock tests as a substitute for the real main-driver/external-service seam.

## Implementation conditions

Before the first production repair phase closes:

- add failing-first tests for the behavior being repaired;
- keep changes within the owner/dependency map;
- record any target deviation in `03-csharp-pattern-selection-records.md` before using it;
- update `plan/architecture-checkpoints.md` with evidence, not assertion;
- run project reference and partial-class guards after each structural extraction;
- do not parallelize test builds that share outputs.

## Closure criteria

The architecture decision changes to `PASS` only when all of the following are evidenced:

- all A2-A8 checkpoints pass;
- main memory tests no longer have `CP001`/`CP002` failures;
- typed Disabled/Automatic/ExplicitDirective modes work through the real agent path;
- an agent can use two explicitly configured providers and cannot reach an unconfigured third provider;
- `/mem:<alias>` has positive and adversarial negative proof;
- project/workflow/process/session/agent/requester identity round-trips through HTTP and MCP;
- status/cancel/feedback ownership fails closed;
- external authentication, project authorization, access/redaction policy, rate/request limits, and truthful manifest pass integration tests;
- production composition supports zero providers, HTTP, MCP, and two providers without implicit native/Qdrant/mock fallback;
- prohibited handwritten partials and forbidden project edges are absent;
- Components MCP validation is rerun or a time-boxed non-functional UI catalog exception is accepted by a named owner;
- a reviewer independent from the implementation produces the final architecture/security review.

## Current gate conclusion

SB35-SB39 repaired the named ownership, routing, transport, and external-service implementation boundaries. SB40 then passed the full affected suites, real-host browser matrix, contributor-handler-driver-ledger seam, live main-driver/external process trace, final CodeAnalytics/dependency review, PostgreSQL worker-lease disposition, independent red-team review, and completed-stage validator.

## Current Repair Gate Update

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Closed | Implicit provider selection and foreign operation access | `bundle://proof/SB36/manifest.md`; explicit allowlist/fallback policies, exact persisted-owner authorization, 196-pass final Memory aggregate, and real-seam proof. | None. |
| Closed | Agent modes, aliases, directives, multi-provider ownership | `bundle://proof/SB37/manifest.md` and `bundle://proof/SB40/manifest.md`; dedicated AgentFramework.Memory owner, 22/22 direct tests, real two-provider seam, and desktop/narrow browser proof. | None. |
| Closed with operational contract | Transport/profile/worker integrity | `bundle://proof/SB38/manifest.md`; response caps, secret references, truthful capabilities, provider editor proof, and PostgreSQL owner/token-fenced leases. | Providers remain idempotent for at-least-once replay after catastrophic lease expiry. |
| Closed | External service security and repository isolation | `bundle://proof/SB39/manifest.md`; final external snapshot `snap-20260712132721-cdf936e2`, zero cross-root references, build 0/0, 59/59 tests, and live main-driver proof. | None. |
| Closed | Terminal proof | `bundle://proof/SB40/manifest.md`, both final CodeAnalytics snapshots, 5/5 Playwright tests, final focused affected suites, production Mem0 bypass scan, and completed validator exit 0. | Non-blocking follow-ups remain explicitly listed in the manifest. |

### Dependency direction

- Memory Application references only Memory Abstractions and SourceGateway Abstractions.
- AgentFramework.Memory references Agent Framework Core/Models/Tooling/WorkflowExecutors and generic Memory Application/Abstractions, never module UI or native Cognitive Memory.
- External Cognitive Memory has nine repository-local projects; its Contracts project has zero project/package references and there is no project-level cycle.
- Final 88-project dependency scan reports zero project cycles. Refreshed CodeAnalytics confirms the repaired AgentFramework.Memory root/Context/Tools namespace SCC is gone; only the bounded cursor/cursor-exception same-project type cycle and the documented external Persistence module cycle remain, and neither crosses a project/repository boundary.

### Partial-class policy

The repaired main Memory, AgentFramework.Memory, and Modules.Memory target contains no prohibited handwritten capability-grouping partial. The external repository's only handwritten partial is the conventional `Program` declaration needed by `WebApplicationFactory`. Generated/Razor/EF exceptions remain governed by the recorded policy.

### Testability proof

The new selection/authorization services, directive/parser/planner/fan-out/merge services, HTTP/MCP mappers/invokers, provider codecs, workers, access policy, and wire contracts have focused tests at their own boundaries. SB40 adds full-system proof rather than inferring it from those focused suites.

### Closure decision

`PASS WITH EXPLICIT NON-BLOCKING FOLLOW-UPS` for release/merge closure. Every SB40 terminal artifact exists and the completed-stage validator passes.

## Final Closure Review

- Main CodeAnalytics: `snap-20260712151629-d5b70dcd`, 15 scoped projects, no blocking errors; the prior AgentFramework.Memory root/Context/Tools SCC is removed.
- External CodeAnalytics: `snap-20260712132721-cdf936e2`, 9 projects, no blocking errors.
- Static architecture: 227 repaired production C#/Razor files, zero prohibited partials, zero files over 220 lines; 88 production projects, zero project-reference cycles.
- Tests/runtime: final main build 0/0 in 28.30 seconds; Memory 196 passed plus one live-env skip; MAF 22/22; focused runtime/template/cleanup Unit 51/51; agent-binding Components 13/13; exact changed retirement Integration 2/2; post-namespace focused contributor/tool-provider tests 25/25 and joined seam 1/1; Playwright 5/5; external build 0/0 and tests 59/59; live conformance 1/1.
- Legacy bypass retirement: the prior Microsoft.Agents.AI.Mem0 package/template/configuration/attachment path was removed; the final production `src` token scan is zero. Guard-token literals remain intentionally in tests.
- Test qualification: a separate broad seed-instruction class still has 4 pre-existing stale assertions and is not represented as green. Its rebaseline remains with the Agent Framework seed-test maintainers and does not substitute for or invalidate the exact changed 2/2 retirement proof.
- Runtime red-team residuals: dormant AgentMemoryRecord compatibility APIs, the missing joined file-backed editor-save/runtime-dispatch test, explicit adapter work for transports beyond HTTP/MCP/NativeRemote, and stored rather than live picker health remain nonblocking only under the owners, risks, and removal conditions in `bundle://proof/SB40/manifest.md`.
- Independent fake-proof review: `bundle://proof/SB40/transcripts/red-team-closure.txt`.
- Final decision: no blocking architecture, selection, ownership, external-isolation, credential, response-size, partial-class, base-composition, or active legacy Mem0 bypass defect remains.
