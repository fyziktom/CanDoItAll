# Architecture Test Plan

## Purpose

This test plan defines what future implementation must prove project by project. It is not a test execution report for v2 because v2 changes architecture documentation only.

## Architecture Dependency Tests

| Target | Required proof |
| --- | --- |
| Core | No EF, Razor, UI module, concrete driver, Git implementation, AgentFramework runtime, or infrastructure references. |
| Runtime | No UI module reference and no concrete domain driver implementation reference. |
| Builder | No UI reference; strategy selection occurs before runtime. |
| UI | No direct EF runtime entity access; consumes application projection contracts. |
| Git wrapper | No Process-specific behavior. |
| Git components | No Process runtime dependency. |

## Domain Vocabulary Leak Tests

Scan generic core/runtime/builder contracts for banned domain vocabulary. Allow examples only in docs, driver tests, and concrete driver projects. The test should fail when a generic contract introduces a domain-specific tool, framework, provider, or UI route concept.

## Project Test Matrix

| Project | Tests |
| --- | --- |
| `CanDoItAll.Processes.Contracts` | DTO serialization, version tolerance, nullability, schema markers, no infrastructure dependencies. |
| `CanDoItAll.Processes.Abstractions` | Strongly typed IDs, capability tag equality, strategy interface result envelopes, domain leakage scan. |
| `CanDoItAll.Processes.Core` | Graph validation, acyclic rules, backward edge budget rules, artifact slot matching, reference scope, state transition tables, loop fingerprints. |
| `CanDoItAll.Processes.Templates` | JSON schema load, component reference resolution, local override patches, three-way merge, conflict records, migration chain, skipped-version safety, projection hash drift. |
| `CanDoItAll.Git` | Status, diff, add, commit, branch, merge conflict listing, path authorization, sanitized logs, failed command handling. |
| `CanDoItAll.Processes.Builder` | Driver stack selection, conflict diagnostics, strategy binding, subprocess recursion, parent/child compatibility, plan hash stability, failure outputs, persistence transaction boundary. |
| `CanDoItAll.Processes.Runtime` | Process run states, step states, dispatch claim lifecycle, cancellation, retry, idempotency, event emission, terminal state immutability, budget consumption. |
| `CanDoItAll.Processes.Persistence` | Event store append, outbox, state concurrency, artifact ledger, projection storage, replay, dead-letter persistence, indexes. |
| `CanDoItAll.Processes.Application` | Use cases, authorization, error mapping, template publish orchestration, run start orchestration, projection queries. |
| `CanDoItAll.Processes.Drivers.Abstractions` | Driver descriptor validation, package compatibility, capability matching contracts, strategy factory contracts. |
| `CanDoItAll.Processes.Drivers.*` | Contract conformance, strategy result envelopes, redaction, negative diagnostics, driver facet projection, no runtime mutation. |
| `CanDoItAll.Components.Git` | Status/diff/conflict rendering, commit form validation, accessibility states, authorization states. |
| `CanDoItAll.Modules.Processes` | Component rendering from projections, live/history behavior, canvas projection rendering, template conflict flow, no runtime internals. |

## Required Scenario Tests

- Missing artifact discovered by a later step triggers manager incident and recovery request.
- Artifact from first step is consumed by final step after intermediate branches.
- Subprocess imports parent artifact, produces child artifact, exports result, and reports completion.
- Subprocess manager escalates to parent manager through durable control message.
- Strategy fault becomes restricted diagnostic plus user-safe incident.
- Automatic recovery is rejected when approval is missing or loop budget is exhausted.
- Branch routes backward, repeats, reaches budget, and escalates.
- Live last-hour view excludes older completed events and includes active runs by explicit active-run rule.
- Template global component update merges cleanly for one usage and creates conflict for another.
- Git audit detects unauthorized file mutation by an agent-backed strategy.

## Gate Tests

| Gate | Required test proof |
| --- | --- |
| G03 | Architecture dependency and vocabulary leak tests. |
| G04 | Template schema/migration/merge tests and Git wrapper tests. |
| G05 | Builder composition and plan hash tests. |
| G06 | Runtime transition, event, claim, and cancellation tests. |
| G07 | Driver stack and strategy binding tests. |
| G08 | Artifact, manager, subprocess, recovery, and loop tests. |
| G09 | Projection replay, snapshot cache, time filter, and UI projection tests. |
| G10 | E2E scenarios and regression suite. |

## v3 Additional Test Obligations

| Area | Required proof |
| --- | --- |
| Project order | Architecture tests prove `Processes.Drivers.Abstractions` exists before Builder dependencies and `Processes.Projections` is the only UI read-model contract source. |
| Persistence/event/outbox | Tests prove runtime command idempotency, event sequence, outbox atomicity, artifact ledger append, projector offsets, dead letters, replay, and upcaster behavior. |
| Branch contract | Tests prove typed outcome routing, backward route budget requirement, loop fingerprint escalation, and rejection of free-text token routing. |
| Manager loop | Tests prove manager work idempotency, policy order, incident lifecycle, recovery lifecycle, subprocess messages, and no direct runtime state mutation. |
| UI projection inventory | Tests prove UI uses projection/application services and does not reference runtime/persistence internals. |
| Execution adapters | Tests prove workflow/agent/agent-group/handoff/scheduler/project/plugin integrations return envelopes and do not leak into core/runtime. |
| Runtime history compatibility | Tests prove selected migration/archive/read-only behavior, action denial for read-only legacy runs, and no active old runtime dependency. |
