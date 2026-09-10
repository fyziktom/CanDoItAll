# 1. Binding principles and decisions

**Status: shared target architecture, version 1.1.** This is not an execution bundle. API names describe responsibilities, not mandatory new symbols. Retain or narrow an appropriate existing contract rather than rename it to match this document.

## Product architecture

CanDoItAll remains a modular monolith. Do not simultaneously introduce microservices, per-module databases, a new frontend, ORM, or universal event bus. Boundaries establish authority and operation semantics, not a quota of projects. The objective is to change one area safely without rewriting the others, not to prohibit useful collaboration.

One authoritative owner means **one logical writer of a particular fact**, not one server instance or universal table. Multiple owner instances/services may enforce the same invariant. Consumers may maintain derived read models, but must identify provenance/version and must not accept independent master edits.

A historical snapshot describes a past decision, not today's fact. Consumer enrichment is a separate fact: a CRM personnel note is not an agent system prompt; an approved budget baseline is not a second price list; a graph occurrence alias is not a source rename.

## Decision register

| ID | Decision | Rationale and change condition |
|---|---|---|
| DEC-01 | Refactor and selectively replace subsystems, not the whole product. | Preserve real product paths and failure semantics. |
| DEC-02 | Finish the current bounded Agents decoupling checkpoint before new broad UI extractions. | Avoid freezing incorrect data contracts in new UI seams. |
| DEC-03 | Agents owns technical agents; Providers owns AI pricing within the Agents area. | CRM and Structure consume those facts. |
| DEC-04 | Structure is a native graph with projections, not a purely read-only namespace. | Native notes, tasks, links, and placements have their own lifecycle. |
| DEC-05 | Work Management initially remains a subdomain of Structure. | Do not invent another physical module before its separation is useful. |
| DEC-06 | Work Management owns task assignments; CRM owns personnel and capacity facts. | One mutation path for Gantt, canvas, CRM, and tools. |
| DEC-07 | CRM retains commercial/staffing project participation; Projects owns the project. | Classify assignment semantics, not merely nullable NodeId values. |
| DEC-08 | Intermodule queries/commands are typed in-process operations; HTTP is an external adapter. | The monolith need not call itself over HTTP for decoupling. |
| DEC-09 | Provider owns its published API; consumer may own a narrow required port. | An integration adapter prevents foreign domain-model dependency. |
| DEC-10 | One physical database per data profile, bounded runtime contexts, coordinated migrations. | Separate access without rebuilding deployment and schema history simultaneously. |
| DEC-11 | Do not remove FK, concurrency, authorization, or atomic guarantees for lighter hosts. | Sandbox convenience does not justify weaker production integrity. |
| DEC-12 | Persisted projections need recoverable synchronization; not every query needs an outbox/cache. | Small reads may use direct batch queries. |
| DEC-13 | A required contribution/effect is an orchestration obligation, not an authorization override. | Every destination owner may still reject it. |
| DEC-14 | Admission, execution completion, result delivery, and business acceptance are distinct. | A projection failure must not relaunch work or falsely report that it never started. |
| DEC-15 | Simple Chats remains an ordinary, non-agent product. | Presentation reuse and external definition administration do not attach tools or implicit context. |
| DEC-16 | Preserve serial tool execution and approval barriers. | Parallel mutations require a separate approved design. |
| DEC-17 | Preserve identities, public/persisted payload compatibility, and historical meaning. | Extraction must not silently alter URLs, NodeKeys, or retry receipts. |
| DEC-18 | Static design is not runtime parity proof. | Each affected slice must execute its proof plan. |
| DEC-19 | All appropriate modules may expose safe reads and commands to automation. | Structure is one destination, not the sole integration hub. |
| DEC-20 | Runtime adapters invoke canonical owner operations; no generic privileged CRUD gateway. | Agents, workflows, and processes are callers, not owners of their destination data. |
| DEC-21 | Tool, HTTP, workflow-executor, and process-step availability are separate claims. | An API, prompt, preset name, or registration alone does not grant execution. |
| DEC-22 | All package and downstream engineering artifacts use English. | Preserve technical identifiers and actual product localization only where required. |

DEC-06/07 are target ownership decisions, not assertions about today's exact tables. Preserve roles, multiplicity, intervals, rates, and audit during classification. Report ambiguous data rather than use last-writer-wins.

## Prohibited replacements for today's problems

Do not introduce a global foreign-entity `IRepository<T>`, giant `IApplicationGateway`, all-domain Contracts package, singleton holding a context, UI-owned process orchestration, facade-hidden service locator, privileged `Execute(string, object)`, foreign-table event writer, dual master writes, or a production mock returning success. A routing mechanism may dispatch registered typed handlers; it must not define a permissive universal business API.

No quotas for interfaces, partial classes, file sizes, or projects. New abstraction must protect authority, variability, or lifetime. Neutral libraries must not encode product policy merely through tool names/enums while appearing dependency-clean.

Preserve useful existing seams: runtime gateways, source-authority providers, prompt services, contributors, and neutral conversation presentation [SRC-002, SRC-003]. Correct their location or implementation rather than create duplicate equivalents. The audited HR-to-CRM integration is an existing capability to retain, not a missing feature to reimplement [SRC-029].
