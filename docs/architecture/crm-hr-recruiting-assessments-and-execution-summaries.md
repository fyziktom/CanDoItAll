# CRM-HR recruiting assessments and execution summaries

Status: Accepted architecture; recruiting foundation implemented, summary persistence phased  
Date: 2026-07-26

## Context

CRM-HR and AgentFramework currently own two valid but disconnected recruiting concepts:

- CRM-HR owns parties, applications, human interviews, support/lifecycle work, authoritative HR decisions, and workforce conversion.
- AgentFramework owns append-only AI-candidate evidence for agent execution, workflow, and process targets plus automated evaluation, human review, and readiness projection.

Process run records already provide the strongest historical projection: scalar run/project/participant IDs, indexed event history, usage/cost metrics, and a versioned narrative with provenance. The EF store uses no-tracking keyset reads and server-side analytics. Workflow runs retain a summary and immutable usage observations, but neither run nor usage rows materialize `ProjectId`. Agent chat retains session/run summaries and execution usage in file slices, but project attribution and narrative provenance are not normalized across all three surfaces; current project-cost queries enumerate complete execution state and filter in memory.

The requested feature needs an application-owned assessment journey, query-efficient historical evidence, and a reusable summarization mechanism. It must not make CRM-HR the execution owner or create an inverse Processes dependency.

## Decision

### Canonical ownership

CRM-HR owns a recruitment-assessment aggregate tied to exactly one application and candidate party. It owns training plans, authoritative HR decisions, stage/conversion gates, and a bounded historical assessment projection.

AgentFramework remains the owner of AI-candidate execution evidence and readiness. CRM-HR consumes it through a focused assessment driver after resolving the party's technical-agent binding. Existing evidence is never copied into a mutable second engine.

Agent, workflow, and process applications remain the only owners allowed to launch or cancel their work. Assessment launch adapters return typed correlation/target identities; they do not expose internal stores to CRM-HR.

```text
CRM-HR application and assessment
        |
        +--> human interview/exercise driver
        |
        +--> AI assessment driver --> AgentFramework recruiting evidence
                                      |
                                      +--> agent execution target resolver
                                      +--> workflow target resolver
                                      +--> process target resolver

Execution completion
        |
        +--> source adapter --> shared summary orchestrator
                                  |
                                  +--> policy/instruction profile
                                  +--> provider/model execution
                                  +--> typed validated summary result
        |
        +--> source-owned snapshot + CRM-HR assessment projection
```

### Generic summary system

The shared seam is a small orchestration contract in the lowest already-shared AgentFramework layer used by chat and workflows; Processes may depend on that seam in its existing dependency direction. The seam extracts only the execution mechanics already proven by the process narrative generator: same-source idempotent reservation/reuse, active-run deferral, structured-output execution, sanitized failure, and provenance.

The contract consists of:

- a typed `ExecutionSummaryContext` containing source kind, typed source ID, optional native project ID, bounded facts, canonical provider-usage observation IDs, sensitivity, and completion time;
- a versioned `ExecutionSummaryProfile` selecting instructions, schema, provider/model policy, and maximum source/summary sizes;
- an `IEvidenceSummaryExecutionCoordinator` for the shared execution mechanics;
- one domain driver per chat, workflow, or process source;
- a typed `ExecutionSummaryResult` with narrative, outcome, findings, canonical provider-usage observation IDs, a labelled display-only usage/cost rollup, completeness, provenance, and generated time;
- source-owned persistence projectors.

The coordinator does not know EF, process events, workflow stores, chat sessions, prompts, output contracts, or CRM-HR. Drivers build bounded contexts, own their untrusted-data envelope and schema validation, and project into their owning store. A failed summary is explicit and retryable; it does not replace a previously accepted summary with a heuristic fallback.

Different instructions are configuration, not different services. Profiles are versioned and selected by a strongly typed profile key. Source-specific behavior belongs in adapters and validators.

### Historical identifiers and time

Internal IDs stay in native types, especially PostgreSQL `uuid`/EF `Guid`. Converting all GUIDs to strings would increase storage and index size and weaken validation. A bounded string is appropriate only for heterogeneous or externally owned identifiers, stored with a typed source kind and indexed as a pair.

Historical projection rows store scalar IDs and no navigation graph. Queries use no tracking. Standard UTC `DateTimeOffset`/`timestamptz` columns are indexed in the order required by real filters. A duplicate Unix timestamp is not added speculatively: PostgreSQL can index and range-scan temporal values directly. Numeric epoch time is justified only by measured partitioning/interoperability requirements.

Temporal semantics are explicit:

- `OccurredAtUtc`: source event time;
- `RecordedAtUtc`: first snapshot insertion time;
- `UpdatedAtUtc`: mutable projection update time.

For CRM-HR assessment history, required indexes are:

- `(ApplicationId, CompletedAtUtc DESC, Id)`;
- `(CandidatePartyId, CompletedAtUtc DESC, Id)`;
- `(ProjectId, CompletedAtUtc DESC, Id)` where project is present;
- `(TargetKind, TargetId, CompletedAtUtc DESC)`;
- `(NextStep, ReviewStatus, CompletedAtUtc DESC)` for the recruiter queue.

### Trust and authority

Summaries, classifications, findings, and proposed next steps are untrusted evidence. They cannot change application stage, reject/hire a candidate, convert a workforce profile, or activate an agent. Human review and existing command authorization remain the authority boundaries.

Snapshot text is bounded, redacted before persistence, and labelled as untrusted when supplied to another model. Provenance includes profile version, provider, model, source evidence version/hash, generated time, and known/unknown cost.

Provider-usage observations are the single accounting source. Every observation has one immutable native observation ID. Process, workflow, chat, and assessment snapshots retain bounded sets of those IDs or a reference to a source-owned observation set. Process, workflow, chat, and assessment summaries may expose labelled rollups for display, but those rollups must not be added together: nested process to workflow to agent execution can otherwise count the same provider call more than once.

The cross-source project-cost read model is built from canonical provider-usage observations plus source/project correlation edges. It deduplicates by observation ID before aggregation. Snapshot totals are display projections and reconciliation checks, never accounting inputs. A source that cannot expose observation identity reports cost completeness as unknown rather than contributing an unverifiable aggregate.

## Current source gaps

| Source | Existing strength | Smallest persistence correction |
| --- | --- | --- |
| Process | Native run/project IDs, hard facts, participant history, metrics, structured narrative provenance, bounded claim/retry lifecycle. | Add `RecordedAtUtc` only if first-ingestion audit semantics are required; existing `EndedAtUtc` and composite indexes already serve historical range queries. |
| Workflow | Typed run snapshot and immutable provider-usage facts with process origin, tokens, cost, and recorded time. | Materialize nullable native `ProjectId` on run and usage rows plus process-origin fields on the run; add project/time composite indexes; add a domain-owned versioned narrative lifecycle. |
| Agent chat/execution | Per-run slices, lightweight chat/run summaries, result summaries, usage and lineage. | Carry native `ProjectId` and direct workflow lineage through invocation/run/usage records; add a rebuildable per-project execution-history projection so queries do not enumerate all run folders. |

## Responsibility inventory

| Responsibility | Owner |
| --- | --- |
| Party, application, assessment, training, HR decision, conversion gate | CRM-HR |
| Technical-agent identity and immutable run evidence | AgentFramework |
| Agent execution launch/run store | AgentFramework application |
| Workflow launch/run store and source snapshot | Workflow application |
| Process launch/run record and narrative | Processes application/projections |
| Generic summary contracts/orchestration | AgentFramework shared core |
| Source context construction and persistence | Source-specific adapters/projectors |
| Cross-source project-cost query | Dedicated read model built from provider-usage observations and source/project correlation edges |
| Recruiting UI orchestration | CRM-HR Blazor components |

## Pattern selection

A ports-and-adapters boundary is selected because there are three real replaceable execution owners and one reusable summarization policy. A new generic repository, base class, event bus, or inheritance hierarchy is rejected. The common behavior is orchestration and validation; source facts and persistence remain composition-based adapters.

The implemented recruiting foundation reuses the existing append-only evidence contract, exposes application-scoped candidate assessment listing/readiness, accepts bound AI-agent parties in CRM-HR, verifies candidate participation for agent/workflow/process targets, adds typed analysis/training/recheck UX, gates AI workforce conversion on application-specific readiness, and fixes the dominant CRM-HR layout defects. Workflow participation is derived from executed node events for the exact run and definition version, rather than configured graph membership. The latest current-configuration attempt controls readiness, so a recheck or later rejection cannot inherit an older approval. Human decisions are not editable in the Blazor page; the API binds reviewer identity and requires the dedicated `agent-recruiting.review` scope.

The current deployment has a single-user/local-operator trust model: an operator who can open Settings can issue API tokens and scopes. The scope therefore protects the mutation boundary and prevents accidental/general API use, but it is not independent separation of duties. Before a multi-user or regulated deployment, a server-side authorization-artifact verifier must resolve a typed artifact ID, verify reviewer permission and application/attempt correlation, and derive the accepted digest. Caller-supplied reference/hash metadata must not be treated as independently verified authorization in that deployment.

The generic evidence API currently validates application/project correlation values as typed non-empty GUIDs but does not query CRM-HR or Projects to prove those cross-domain edges. The in-product CRM-HR flow supplies its selected application and bound technical candidate, but external clients remain inside the trusted-operator boundary. A source-specific correlation validator is required before those generic endpoints are exposed to independently trusted clients.

Application-owned relational assessment snapshots, source launch adapters, cross-domain correlation validation, canonical provenance extraction, automatic summary generation, first-class training-plan lineage, and cross-source project-cost persistence remain phased work behind the contracts above rather than being improvised in Razor.

## Rejected alternatives

- Promote AgentFramework recruiting evidence into the generic CRM-HR aggregate: it is deliberately AI-agent/configuration-version specific.
- Duplicate evidence into CRM-HR mutable tables: creates two readiness authorities.
- Let an LLM update stage or activate an agent: bypasses human authorization and audit.
- Make AgentFramework depend on Processes for result summaries: reverses the established dependency direction and risks a cycle.
- Put one giant summary service in CRM-HR: chat/workflow/process owners would depend on the wrong module.
- Store every GUID as text: larger indexes, weaker validation, and no historical benefit for internally owned IDs.
- Duplicate every UTC value as Unix time: write/index cost without a demonstrated query improvement.
- Add more partials to `RecruitingService` or large Razor pages: preserves the existing responsibility hotspot.
- Add a silent heuristic summary when generation fails: hides missing evidence and corrupts downstream decisions.

## Testability and closure gates

- Summary orchestration is testable with fake source adapters, profile registry, generator, clock, and projector.
- Each source adapter has contract tests for bounds, sensitivity, project attribution, usage completeness, and terminal-state handling.
- Current CRM-HR tests prove application-scoped readiness, AI binding validation, latest-attempt/recheck precedence, conversion gates, and authoritative decision separation.
- The phased relational assessment aggregate must add contract tests for immutable application ownership and first-class training/recheck lineage.
- Dependency analysis proves no new project cycle or inverse Processes reference.
- Query tests prove all primary filters use bounded indexed projections rather than full runtime loads.
- Accounting tests prove nested process, workflow, and agent correlations deduplicate immutable provider-usage observation IDs before aggregation.
- File-projection tests prove project-history delta updates and full rebuild produce the same result.
- Historical native UUID rows remain queryable after their source record is removed.
- Browser tests prove the dominant next action is understandable and that automated readiness never performs conversion or activation.
