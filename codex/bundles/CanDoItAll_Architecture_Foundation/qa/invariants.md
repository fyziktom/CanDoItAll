# Architectural and product invariants

Normative target constraints. Their proof plan is in catalogs/invariant-proof-plan.json; all application proof remains NOT_RUN.

## INV-001

**Title:** One writer per fact

**Rule:** Only its owner changes master data; projections, local enrichment and historical snapshots have distinct meanings.

**Module ids:** MOD-AGENTS; MOD-CRM; BND-WORK

**Status:** NORMATIVE_TARGET

## INV-002

**Title:** No foreign EF backdoor

**Rule:** Owner contracts expose no DbContext, EF entities or IQueryable; runtime models do not pull foreign master data through navigations.

**Module ids:** BND-COMPOSITION; MOD-STRUCTURE

**Status:** NORMATIVE_TARGET

## INV-003

**Title:** UI has no execution authority

**Rule:** Reusable presentation controls neither business transactions nor runtime state and has no runtime or persistence dependency.

**Module ids:** BND-CONVERSATIONS; MOD-AGENTS

**Status:** NORMATIVE_TARGET

## INV-004

**Title:** Native data versus projections

**Rule:** Projection reads and rebuilds neither overwrite nor delete native notes, tasks, links or layout.

**Module ids:** MOD-STRUCTURE

**Status:** NORMATIVE_TARGET

## INV-005

**Title:** SimpleNote is not a file fallback

**Rule:** An inline native body and file-node Notes have different authority; opening a node never changes its master representation.

**Module ids:** MOD-STRUCTURE; BND-STORAGE

**Status:** NORMATIVE_TARGET

## INV-006

**Title:** Stable identity and origin

**Rule:** Renaming or switching views does not change source identity; runs and contributions retain their frozen targets and lineage.

**Module ids:** MOD-STRUCTURE; MOD-PROCESSES; BND-WORKFLOWS

**Status:** NORMATIVE_TARGET

## INV-007

**Title:** Current authority

**Rule:** Mutations, secret access, reads and approvals enforce current actor, scope and purpose; model input is not a trusted grant.

**Module ids:** MOD-SECURITY; MOD-AGENTS

**Status:** NORMATIVE_TARGET

## INV-008

**Title:** Profile and incarnation fence

**Rule:** Data, workers and caches from another profile, generation or history must not be used in the current scope.

**Module ids:** BND-COMPOSITION; MOD-WORKSPACE

**Status:** NORMATIVE_TARGET

## INV-009

**Title:** Optimistic concurrency

**Rule:** Relevant revisions or tokens prevent lost updates with the same semantics across every surface.

**Module ids:** BND-WORK; MOD-STRUCTURE; MOD-CRM

**Status:** NORMATIVE_TARGET

## INV-010

**Title:** Durable idempotency

**Rule:** The same key and payload have one confirmed local effect or receipt; a different payload conflicts.

**Module ids:** MOD-STRUCTURE; MOD-PROCESSES; BND-WORKFLOWS

**Status:** NORMATIVE_TARGET

## INV-011

**Title:** Truthful acceptance

**Rule:** Accepted, Committed, Unconfirmed and Rejected are not interchangeable; UI failure does not erase an admission receipt.

**Module ids:** MOD-AGENTS; BND-API

**Status:** NORMATIVE_TARGET

## INV-012

**Title:** Separate writeback

**Rule:** Execution success, required output publication and task acceptance have separate authorities and states.

**Module ids:** MOD-PROCESSES; BND-WORKFLOWS; MOD-STRUCTURE

**Status:** NORMATIVE_TARGET

## INV-013

**Title:** No appropriation of foreign output

**Rule:** Run manifests and contribution receipts prove origin; a UI set-difference is not the sole authority.

**Module ids:** MOD-STRUCTURE; BND-WORKFLOWS

**Status:** NORMATIVE_TARGET

## INV-014

**Title:** Serial tools and approvals

**Rule:** Preserve tool order and approval barriers; continuation revalidates the material payload.

**Module ids:** MOD-AGENTS; BND-WORKFLOWS

**Status:** NORMATIVE_TARGET

## INV-015

**Title:** Assets have real content

**Rule:** An available file node corresponds to the correct bytes and revision; storage/database intermediate states are truthful.

**Module ids:** BND-STORAGE; MOD-STRUCTURE

**Status:** NORMATIVE_TARGET

## INV-016

**Title:** Deletion provenance

**Rule:** Confirmation does not bypass ownership, liveness or references; shared bytes are not deleted prematurely.

**Module ids:** BND-STORAGE; MOD-PROCESSES

**Status:** NORMATIVE_TARGET

## INV-017

**Title:** Destructive-operation compatibility

**Rule:** Preserve explicit dispositions, legacy defaults and partial outcomes for multiple roots.

**Module ids:** MOD-STRUCTURE; BND-API

**Status:** NORMATIVE_TARGET

## INV-018

**Title:** One work plan

**Rule:** Gantt, canvas, CRM and tools change the same WorkItem and assignment; a task is not a ProcessStep.

**Module ids:** BND-WORK; MOD-CRM

**Status:** NORMATIVE_TARGET

## INV-019

**Title:** Reservation is not assignment

**Rule:** Mandatory reservation and assignment retain atomicity or explicit pending state; runtime admission is another layer.

**Module ids:** BND-WORK; MOD-CRM

**Status:** NORMATIVE_TARGET

## INV-020

**Title:** AI prices have an owner

**Rule:** Providers/Agents determines tariffs and quotes; CRM is not a second AI price list.

**Module ids:** MOD-PROVIDERS; MOD-CRM

**Status:** NORMATIVE_TARGET

## INV-021

**Title:** Historical pricing and evidence

**Rule:** New tariffs or versions do not change past budgets, usage or accepted runs.

**Module ids:** MOD-PROVIDERS; MOD-PROMPTS

**Status:** NORMATIVE_TARGET

## INV-022

**Title:** Cost completeness

**Rule:** Distinguish unknown, partial and free; make currencies, units, valuation basis and aggregation deduplication explicit.

**Module ids:** MOD-PROVIDERS; BND-WORK

**Status:** NORMATIVE_TARGET

## INV-023

**Title:** Source runtime authority

**Rule:** Only its owner changes process or workflow state; technical success is not business acceptance.

**Module ids:** MOD-PROCESSES; BND-WORKFLOWS; MOD-TESTLAB

**Status:** NORMATIVE_TARGET

## INV-024

**Title:** Simple Chats remain ordinary

**Rule:** UI sharing introduces no implicit agent tools, context or store; preserve existing turn durability.

**Module ids:** BND-SIMPLECHATS; BND-CONVERSATIONS

**Status:** NORMATIVE_TARGET

## INV-025

**Title:** Source revisions and rebuild

**Rule:** Projection sequences, gaps, tombstones and coverage are explicit; old events do not resurrect a deleted source.

**Module ids:** MOD-STRUCTURE; MOD-MEMORY; MOD-CRM

**Status:** NORMATIVE_TARGET

## INV-026

**Title:** Outbox and inbox atomicity

**Rule:** Publication intent commits with source state; the dedupe marker commits with the consumer effect; a transient signal is not master data.

**Module ids:** BND-COMPOSITION

**Status:** NORMATIVE_TARGET

## INV-027

**Title:** One schema authority

**Rule:** Canonical migration mappings are not duplicated by runtime DDL; subset hosts do not generate production migrations.

**Module ids:** BND-COMPOSITION

**Status:** NORMATIVE_TARGET

## INV-028

**Title:** Transaction guarantees

**Rule:** A cross-context atomic operation has an actual shared connection and transaction, a short scope and coordinated retries.

**Module ids:** BND-COMPOSITION; BND-WORK; MOD-CRM

**Status:** NORMATIVE_TARGET

## INV-029

**Title:** Single-writer cutover

**Rule:** A legacy adapter delegates instead of dual-writing; rollback and restore respect data compatibility and external effects.

**Module ids:** BND-COMPOSITION

**Status:** NORMATIVE_TARGET

## INV-030

**Title:** Unavailable is not success

**Rule:** Missing, denied or unsupported capabilities are explicit, not replaced by production fakes.

**Module ids:** BND-COMPOSITION; MOD-SECURITY

**Status:** NORMATIVE_TARGET

## INV-031

**Title:** Data minimization

**Rule:** Secrets and PII do not leak through projections, quotes, logs or exports; membership and source access are current.

**Module ids:** MOD-SECURITY; MOD-COLLAB; MOD-CRM

**Status:** NORMATIVE_TARGET

## INV-032

**Title:** Headless and desktop parity

**Rule:** Owner operations do not depend on a Razor page; desktop-only host actions remain capability-gated.

**Module ids:** BND-COMPOSITION; BND-STORAGE

**Status:** NORMATIVE_TARGET

## INV-033

**Title:** A snapshot is bounded evidence

**Rule:** Snapshot capture has coverage, expiry and fingerprint, not silent fallback or mutation authority.

**Module ids:** MOD-STRUCTURE; MOD-AGENTS

**Status:** NORMATIVE_TARGET

## INV-034

**Title:** Registration is not a grant

**Rule:** HTTP scope, a plugin grant or a tool descriptor does not replace owner policy.

**Module ids:** BND-API; MOD-PLUGINS; MOD-AGENTS

**Status:** NORMATIVE_TARGET

## INV-035

**Title:** Notifications and UI lifecycle

**Rule:** Post-commit invalidation does not replace durable state; fence late callbacks and dispose subscriptions.

**Module ids:** BND-CONVERSATIONS; BND-COMPOSITION

**Status:** NORMATIVE_TARGET

## INV-036

**Title:** Stored-shape compatibility

**Rule:** NodeKeys, URLs, payload versions, import mappings and history have explicit migration policies.

**Module ids:** MOD-STRUCTURE; MOD-PROJECTS

**Status:** NORMATIVE_TARGET

## INV-037

**Title:** Traceable recovery

**Rule:** Required pending or failed effects and uncertain external effects have receipts, correlation and safe retry or reconciliation.

**Module ids:** BND-CONNECTORS; MOD-PROCESSES

**Status:** NORMATIVE_TARGET

## INV-038

**Title:** Future functions do not expand scope

**Rule:** A gap or estimate does not authorize implementation; new hardening guarantees require scoped proof.

**Module ids:** MOD-TESTLAB; MOD-MEMORY; MOD-SCHEDULER

**Status:** NORMATIVE_TARGET

## INV-039

**Title:** No silent loss of supported capabilities

**Rule:** Rewire a working integration instead of removing it merely to clean up the dependency graph.

**Module ids:** MOD-AGENTS; MOD-STRUCTURE; MOD-PROCESSES

**Status:** NORMATIVE_TARGET

## INV-040

**Title:** Measure actual outcomes

**Rule:** Use actual tests, build graphs and performance evidence rather than quotas, assumed passes or counts of new abstractions.

**Module ids:** BND-COMPOSITION

**Status:** NORMATIVE_TARGET

## INV-041

**Title:** Automation calls owners, not foreign stores

**Rule:** Agents, workflows, and processes use typed destination-owner operations; Structure is not a universal intermediary.

**Module ids:** MOD-AGENTS; MOD-CRM; BND-WORKFLOWS; MOD-PROCESSES

**Status:** NORMATIVE_TARGET

## INV-042

**Title:** Surface availability is explicit

**Rule:** API, descriptor, preset name, or UI existence does not prove an attached authorized tool or executor.

**Module ids:** MOD-AGENTS; MOD-SCHEDULER; MOD-PROCESSES

**Status:** NORMATIVE_TARGET

## INV-043

**Title:** Managed identity is not a display label

**Rule:** Caller authority is resolved and bounded; ordinary automation cannot spoof HR or inherit interactive-only privileges.

**Module ids:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Status:** NORMATIVE_TARGET

## INV-044

**Title:** Simple Chats administration remains external

**Rule:** Definition administration does not add agent dependencies, tools, implicit context, conversation access, or transcript mutation to Simple Chats.

**Module ids:** BND-SIMPLECHATS; MOD-AGENTS; BND-CONVERSATIONS

**Status:** NORMATIVE_TARGET

## INV-045

**Title:** New retry-safe commands commit receipts with effects

**Rule:** An owner effect and its durable dedupe/receipt must share an atomic boundary before automatic retry safety is advertised.

**Module ids:** MOD-CRM; BND-SIMPLECHATS; MOD-SCHEDULER

**Status:** NORMATIVE_TARGET

## INV-046

**Title:** Read and disclosure permissions differ

**Rule:** Source access, model/provider disclosure, and destination publication each require an applicable permission decision.

**Module ids:** MOD-CRM; MOD-RESOURCES; MOD-AGENTS

**Status:** NORMATIVE_TARGET

## INV-047

**Title:** Use-time authority survives no cache shortcut

**Rule:** Current authorization, actor scope, field policy, and required approval are checked at dispatch/resume and receipt disclosure.

**Module ids:** MOD-AGENTS; MOD-CRM; BND-WORKFLOWS

**Status:** NORMATIVE_TARGET

## INV-048

**Title:** Step retry differs from loop iteration

**Rule:** Persist logical step/iteration/effect identity; replay reconciles existing owner effects rather than deriving new IDs from attempts.

**Module ids:** BND-WORKFLOWS; MOD-PROCESSES

**Status:** NORMATIVE_TARGET

## INV-049

**Title:** Safe lookup is not a stale assignment guarantee

**Rule:** Typed identity resolution and current eligibility validation are required before source references drive mutations.

**Module ids:** MOD-CRM; MOD-RESOURCES; BND-WORK

**Status:** NORMATIVE_TARGET

## INV-050

**Title:** Administration preserves separate fact owners

**Rule:** Staffing fields cannot overwrite agent configuration, Provider AI prices, or Work Management assignments.

**Module ids:** MOD-CRM; MOD-AGENTS; MOD-PROVIDERS; BND-WORK

**Status:** NORMATIVE_TARGET

## INV-051

**Title:** Required foreign effects remain visible

**Rule:** Successful execution does not hide blocked required domain effects; compensation never silently deletes pre-existing or later-edited data.

**Module ids:** MOD-PROCESSES; MOD-CRM; BND-WORK

**Status:** NORMATIVE_TARGET

## INV-052

**Title:** Engineering artifacts are English

**Rule:** Instructions, catalogs, implementation bundles, comments, and reports use English; retain required technical identifiers and product-localized strings.

**Module ids:** BND-COMPOSITION

**Status:** NORMATIVE_TARGET
