# Structured Input

## Core Objective

- Make agent preparation observable from the first operation stage and materially faster by using current immutable module snapshots and revisioned preparation data before deeper storage/runtime work.

## Success Criteria

- A caller receives a typed `Accepted` or `CapturingContext` activity before catalog/provider/session storage work begins.
- Floating and process-manager chats show truthful phase/status text throughout preparation, runtime composition, provider wait, tool work, and completion/failure.
- Current project-structure and process-run projections are captured atomically with revision/freshness metadata and are available to the agent without a duplicate deep query.
- Preparation reuses only immutable descriptors/snapshots; mutable runtime/provider/MCP resources remain execution-owned.
- Baseline and after measurements prove lower time-to-first-activity and lower or equal time-to-runtime-start on representative cold and warm paths.
- Backend tests prove ordering, per-stream isolation, cancellation/disposal, bounded retention, handler-failure isolation, stale-revision rejection, and safe concurrent reads.
- Existing module behavior and execution persistence remain correct.
- SharedInfo API/skill documentation describes the new contracts and future authorized SSE projection seam.

## Hard Constraints

- Backend subbundles and performance gate complete before UI implementation.
- Strongly typed stream, operation, phase, revision, and snapshot identifiers; no topic strings or cache keys.
- No shared mutable source-of-truth copy and no snapshot writes back to canonical storage.
- No parallel operations on one EF `DbContext` or on a storage service whose concurrency contract is not explicit.
- No live-agent instance pool in this initiative.
- Event publication on the UI-critical path must not depend on durable execution-log persistence.
- Errors are explicit, observable, logged with safe actionable state, and never hidden by fallback.
- SSE, MQTT, OPC UA, distributed transport, and broad caching are out of scope.

## Allowed Side Effects

- Product code, tests, bundle proof, repository docs, API docs, and relevant skills in `CanDoItAll.SharedInfo` may change only as owned by the seven subbundles.
- Test configuration may select `gpt-5.4-mini`; persisted user/provider configuration must not be rewritten as a side effect.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- CodeAnalytics snapshots recorded in `analysis/01-current-state.md`
- Existing agent framework, process workspace, project-structure context, persistence, component, and test sources inventoried in `inventories/01-scope-inventory.md`

## Input Coverage Signals

- Early frozen UI before run creation.
- Runtime-first project/process snapshots.
- Safe read-only parallelism and EF thread-safety.
- Prepared DI/runtime semantics without unsafe pooling.
- Typed shared event organization and future restricted SSE projection.
- Snapshot lifetime, invalidation, and source-of-truth safety.
- Backend-first sequencing and measured improvement.
- Cross-module generality, UI proof, SharedInfo updates, mini-model test, build, and port-5032 restart.

## Dependency And Sequencing Signals

- Current-state baseline and architecture contracts gate every code edit.
- Typed activity lifecycle gates preparation and UI consumption.
- Runtime preparation and module snapshot adapters gate meaningful backend measurement.
- The backend performance/concurrency gate blocks all UI edits.
- UI proof blocks documentation closure and host restart.

## Validation Expectations

- Use failing-first unit/component/integration tests and targeted architecture snapshots.
- Measure operation acceptance-to-first-activity, acceptance-to-run-created, runtime composition stages, and acceptance-to-provider-start for cold and warm representative paths.
- Include adversarial concurrent-update and stale-snapshot tests, not only happy-path reuse tests.
- Run targeted tests after each subbundle, then solution build and relevant suites.
- Validate both chat surfaces through Playwright at a named large desktop viewport and inspect normal, busy, failure, and approval states.

## Evidence Contract

- SB01 `Governed`: architecture snapshot, dependency map, baseline transcript/metrics, architecture-gate record.
- SB02 `Governed`: producer/consumer/lifecycle matrix, failing/passing stream tests, architecture snapshot, anti-stub proof.
- SB03 `Governed`: preparation revision/invalidation tests, cold/warm timings, disposal/lifecycle proof.
- SB04 `Behavioral`: project/process snapshot positive and stale/concurrent negative tests.
- SB05 `Governed`: before/after report, threshold assertions, concurrency/EF review, targeted build/test transcripts, backend go/no-go.
- SB06 `Behavioral`: component tests, Playwright actions/assertions, normal/busy/error/approval screenshots for floating and manager chats.
- SB07 `Governed`: documentation diff, SharedInfo validators/tests, `gpt-5.4-mini` real-agent transcript, solution build/test, port-5032 health proof, final architecture gate and closure manifest.

## UI Validation Strategy

- Primary surfaces: floating `AgentChatPanel` and Process Workspace Manager tab chat.
- Supporting activity detail remains inside the existing chat run-state area; no new dashboard, modal, or persistent stats panel.
- No list/editor restructuring is planned. Activity history may use the existing execution-log surface; transient current activity stays compact.
- Existing prompt textarea and dialog dimensions remain unless evidence shows clipping. The chat transcript remains the scroll owner; current activity must stay visible without adding nested scroll.
- Validate at `1920x1080` or the named maximized desktop viewport. BaseLib changes, if required, additionally validate small/medium/large.
- Follow `candoitall-components-mcp/references/compact-ui-composition.md` during SB06.

## Browser Validation Analytics

- SB06 and SB07 record route, `1920x1080` viewport, chat transcript scroll owner, floating panel normal/busy/error/approval states, process manager normal/busy/error states, exact Playwright actions/assertions, screenshot paths, clipped/overlapping/stale-status findings, and pass/fail in `reviews/01-execution-report.md`.

## Working Assumptions

- `AgentChatContextRegistry` remains the scoped canonical live-context capture mechanism.
- Existing process/project projections are authoritative read models for immediate prompt context but cannot write canonical data.
- Execution run/detail/log persistence remains canonical; `ExecutionEvent` and pre-run operational activity are projections with different retention semantics.
- The file-backed agent workspace store may serialize operations; concurrency is introduced only across independently safe collaborators.

## Primary Risks

- A generic event bus can become an unbounded service locator or leak unauthorized cross-module events.
- A “prepared agent” pool can accidentally retain credentials, provider clients, DbContexts, tool sessions, or stale capabilities.
- Snapshot freshness bugs can make stale data look canonical.
- Persist-before-publish can retain perceived latency even after status strings are improved.
- Parallel reads can corrupt scoped EF/file-store state or increase I/O contention.
- Per-stage persistence/event volume can become the next bottleneck.
