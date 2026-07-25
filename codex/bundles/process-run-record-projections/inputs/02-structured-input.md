# Structured Input

## Core Objective

- Make historical process-run reads cheap, predictable, and reusable by persisting a complete compact record once and reserving canonical deep hydration for explicit drill-down.

## Success Criteria

- A completed, failed, cancelled, or escalated run has an idempotently upserted record with queryable scalar metadata, strongly typed hard facts, evidence completeness, and explicit structured-summary state.
- Historical list, summary, graph, and analytics paths query stored records without loading full runtime state or Agent Framework execution details per row.
- A manager-agent summary is produced asynchronously and failure is visible and retryable.
- Terminal project-structure summaries use the stored record.
- Processes APIs expose typed list, summary, and analytics contracts.
- The SharedInfo Processes API skill accurately documents the implemented API.
- Focused tests, build, migration/model checks, architecture gate, and both performance-review passes succeed.

## Hard Constraints

- Preserve Runtime -> no Projections dependency.
- Use scalar IDs and JSON value objects; do not introduce ORM navigations or join-heavy history reads.
- Do not put LLM work in runtime transactions, projection catch-up, or GET requests.
- Do not silently synthesize unavailable historic facts or narrative fallback text.
- Treat sensitive prompts, log bodies, and tool arguments as deep evidence; do not copy them into compact records.
- Use top-level cohesive services instead of extending the 1,900-line runtime projection query service or adding partial classes.
- Preserve the existing resumable meaning of runtime `Escalated`.

## Allowed Side Effects

- Processes projection contracts, application orchestration, persistence model/configuration/migration, Processes module integration, project-structure projection, web APIs, affected UI/application consumers, tests, bundle evidence, and the authoritative sibling Processes API skill.

## Source Artifacts

- See `inputs/00-original-request.md` and `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- N001: historical loading is slow and usage is expanding.
- N002: improvement must be architectural rather than only a local micro-optimization.
- N003: terminal records need hard facts for steps, repetitions, actors, duration, usage, and cost.
- N004: manager-agent LLM output must be structured and connected to the hard record.
- N005: IDs/JSON references are preferred to database relations and joins.
- N006: Runs, Graphs, Analytics, manager interaction, LiveProcesses, CRM, and project structure need reusable data.
- N007: sequential versus asynchronous/deep loading must be measured and improved.
- N008: Processes APIs and SharedInfo skill must be updated.
- N009: architecture and modular-refactoring gates are mandatory.

## Dependency And Sequencing Signals

- Baseline characterization defines the no-deep-hydration performance contract.
- Contracts, schema, and migration block finalization, API, and project-node work.
- Deterministic hard facts must commit before asynchronous narrative generation.
- API implementation blocks SharedInfo skill parity.
- Every implementation subbundle blocks final architecture/performance closure.

## Validation Expectations

- Behavioral tests must prove idempotency, dispositions, completeness, narrative state transitions, snapshot-only reads, filters/paging, analytics aggregation, and API contracts.
- EF model/migration and full solution build must succeed.
- Performance review must compare the old and new logical I/O shape; claims of speedup require query/call-count evidence, not stopwatch-only assertions.
- Architecture review must verify dependency direction, canonical-source boundaries, test seams, and absence of policy leakage.

## Evidence Contract

- SB01 Behavioral: focused characterization tests plus recorded Pass 1 and Pass 2 findings.
- SB02 Behavioral critical foundation: model/store tests, migration inspection, project build.
- SB03 Behavioral critical foundation: assembler, finalization, narrative failure/retry, and project-node tests.
- SB04 Behavioral: query/API tests proving snapshot-only behavior and bounded paging.
- SB05 Standard: diff/readback of authoritative API skill against implemented routes.
- SB06 Behavioral: focused suites, solution build, architecture gate, and final validator.

## UI Validation Strategy

- This bundle primarily changes data sources and APIs. Existing visual composition is preserved.
- If a browser-visible component must change to consume records, validate its existing large-screen Runs/Graphs/Analytics surface and scroll owner at 1600x900; no mobile/BaseLib redesign is authorized.

## Browser Validation Analytics

- N/A unless SB04 changes rendered markup. A data-source-only refactor is proven through service/component tests and API requests.

## Working Assumptions

- Existing background projection processing remains the trigger mechanism; terminal record assembly must not be added to synchronous GET catch-up.
- Escalation produces a versioned `Escalated` disposition record, but a later resumed outcome may replace it for the same run ID.
- Retained evidence is not complete enough for a lossless historic backfill; completeness flags are contractual.
- The shared agent catalog is the source for agent display metadata; records keep agent IDs and stable execution-time labels only where needed.

## Primary Risks

- Completion hooks are distributed and may double-trigger; upsert/version/hash idempotency is mandatory.
- Scoped EF stores cannot safely be parallelized with `Task.WhenAll`; batching and projection-shaped reads are required.
- Summary generation can fail or run concurrently on several hosts; lease/attempt state must be explicit.
- JSON schema evolution can break old records; record and payload schema versions are required.
- Sibling skill updates require write permission outside this repository.
