# 2. Domain glossary

Normative definitions. Existing serialized identifiers remain compatible; conceptual terminology does not mandate renaming types.

## TERM-001

**Term:** Domain / bounded context

**Owner:** Specific domain owner

**Definition:** A boundary with its own vocabulary, authority and invariants.

**Distinction:** Not automatically an assembly, deployment or UI tab.

**Current alias:** 

## TERM-002

**Term:** Module / feature package

**Owner:** Composition and domain

**Definition:** A physical unit of code or feature composition; it may currently contain several logical boundaries.

**Distinction:** The location of a class does not determine its rightful authority.

**Current alias:** 

## TERM-003

**Term:** Canonical fact

**Owner:** Domain owner

**Definition:** A current fact that can be changed only through its owner's operation.

**Distinction:** A read-model copy does not become a second master.

**Current alias:** 

## TERM-004

**Term:** Projection

**Owner:** Projection consumer

**Definition:** A derived view carrying source references, versions and freshness information.

**Distinction:** It has no independent authority to change source values.

**Current alias:** 

## TERM-005

**Term:** Historical snapshot

**Owner:** Decision or evidence owner

**Definition:** An immutable record of the values used at the time of a decision or execution.

**Distinction:** It does not change when today's price list or definition changes.

**Current alias:** 

## TERM-006

**Term:** Local enrichment

**Owner:** Enrichment owner

**Definition:** A new locally owned fact attached to a foreign identity, such as a CRM note.

**Distinction:** It does not overwrite a technical agent field.

**Current alias:** 

## TERM-007

**Term:** EntityReference

**Owner:** Owner of the referenced entity kind

**Definition:** A typed reference containing owner, kind, stable identity and the required source scope.

**Distinction:** Not a UI path, display name or implicit permission.

**Current alias:** 

## TERM-008

**Term:** NodeId / NodeKey

**Owner:** Project Structure

**Definition:** A stable identity for a particular node or occurrence in the graph; preserve the existing key format.

**Distinction:** Not necessarily a GUID, and not the same as AgentId.

**Current alias:** 

## TERM-009

**Term:** Source revision

**Owner:** Source owner

**Definition:** A content or configuration version issued by its owner.

**Distinction:** Not the time of the last render.

**Current alias:** 

## TERM-010

**Term:** Concurrency token

**Owner:** Aggregate owner

**Definition:** An opaque value used to compare the expected state before a mutation.

**Distinction:** A random GUID is not an ordered event sequence.

**Current alias:** 

## TERM-011

**Term:** Profile generation

**Owner:** Control plane

**Definition:** The activation generation of a database profile, used to invalidate preparation and caches.

**Distinction:** Not an entity revision or an authorization grant.

**Current alias:** 

## TERM-012

**Term:** Database incarnation

**Owner:** Control plane

**Definition:** The identity of a particular continuity of stored data; a restore or clone must not be mistaken for the old event stream.

**Distinction:** More than a connection string or profile name.

**Current alias:** 

## TERM-013

**Term:** Workspace (domain)

**Owner:** Workspace

**Definition:** A bounded working environment and its preferences.

**Distinction:** Not an open editor, circuit or filesystem root.

**Current alias:** 

## TERM-014

**Term:** Organization/work scope

**Owner:** Security and domain owner

**Definition:** The business access boundary actually supported by the product.

**Distinction:** Does not automatically introduce a new TenantId.

**Current alias:** 

## TERM-015

**Term:** Workspace root

**Owner:** Storage and runtime host

**Definition:** An authorized root of a file or execution environment.

**Distinction:** Not the domain identity of a project.

**Current alias:** 

## TERM-016

**Term:** Editor session

**Owner:** UI application adapter

**Definition:** One open editor instance and its local changes.

**Distinction:** Must not retain master entities or a long-lived database context.

**Current alias:** 

## TERM-017

**Term:** Invocation context

**Owner:** Agents and owner adapters

**Definition:** Verified immutable context for one invocation, including purpose, scope and provenance.

**Distinction:** Text from a floating window cannot grant itself authority.

**Current alias:** 

## TERM-018

**Term:** Invocation snapshot

**Owner:** Source owner

**Definition:** A bounded contextual copy captured for one invocation with coverage and freshness constraints.

**Distinction:** Neither a durable master nor permission for a later mutation.

**Current alias:** 

## TERM-019

**Term:** Effective authority

**Owner:** Security and source owner

**Definition:** The intersection of valid actor authority, delegation, capabilities, scope and current policy.

**Distinction:** Not an old screenshot, UI selection or bearer string supplied by a model.

**Current alias:** 

## TERM-020

**Term:** AgentDefinition

**Owner:** Agents

**Definition:** The technical identity and configuration of a particular agent.

**Distinction:** Not a CRM party or a live runtime object.

**Current alias:** 

## TERM-021

**Term:** AgentTemplate

**Owner:** Agents

**Definition:** A template used to create or govern agent definitions.

**Distinction:** Not automatically a schedulable worker.

**Current alias:** 

## TERM-022

**Term:** AgentRun / Execution

**Owner:** Agents runtime

**Definition:** One execution with identity, input, policy and result.

**Distinction:** Not AgentDefinition; its lifetime is not the window lifetime.

**Current alias:** 

## TERM-023

**Term:** Capability

**Owner:** Capability owner and Agents policy

**Definition:** A declared ability and the restrictions on using it.

**Distinction:** Registering a capability does not prove that an executable tool is available.

**Current alias:** 

## TERM-024

**Term:** Runtime tool

**Owner:** Domain adapter

**Definition:** A typed operation actually attached to a particular invocation.

**Distinction:** An HTTP endpoint or prompt list does not create a runtime tool.

**Current alias:** 

## TERM-025

**Term:** Tool receipt

**Owner:** Operation owner and runtime evidence

**Definition:** Evidence that a specific operation was accepted, applied or rejected.

**Distinction:** Not an arbitrary assistant statement that it is done.

**Current alias:** 

## TERM-026

**Term:** ProviderProfile

**Owner:** Agents / Providers

**Definition:** Access, models, transport, purpose, routing and secret-reference configuration.

**Distinction:** Not a resolved secret or a CRM price list.

**Current alias:** 

## TERM-027

**Term:** Provider publication

**Owner:** Agents / Providers

**Definition:** An authorized offer of selected provider capabilities to other instances.

**Distinction:** Not a copy of the source credentials.

**Current alias:** 

## TERM-028

**Term:** Provider import

**Owner:** Agents / Providers

**Definition:** A local profile linked to a foreign publication while retaining its origin identity.

**Distinction:** Must not forge local ownership or ignore upstream revocation.

**Current alias:** 

## TERM-029

**Term:** Tariff

**Owner:** Agents / Providers

**Definition:** A versioned price for a kind of consumption, with unit and currency.

**Distinction:** Not always a per-token price; a model may use other billing units.

**Current alias:** 

## TERM-030

**Term:** CostQuote

**Owner:** Agents / Providers

**Definition:** An estimate for a specific workload, including assumptions, version and uncertainty.

**Distinction:** Not an invoice or permission to spend a budget.

**Current alias:** 

## TERM-031

**Term:** UsageRecord

**Owner:** Execution and Providers evidence

**Definition:** Observed consumption for a particular invocation, with provenance.

**Distinction:** Missing usage is not zero usage.

**Current alias:** 

## TERM-032

**Term:** CostActual

**Owner:** Providers evidence

**Definition:** Valuation of observed usage using a frozen tariff; it may be partial or estimated.

**Distinction:** Not automatically the amount actually invoiced upstream.

**Current alias:** 

## TERM-033

**Term:** Budget baseline

**Owner:** Work Management / Projects

**Definition:** An approved historical cost plan.

**Distinction:** Not a cache of current prices.

**Current alias:** 

## TERM-034

**Term:** Party

**Owner:** CRM

**Definition:** A personnel, organizational or commercial identity.

**Distinction:** A technical agent has its own Agents identity.

**Current alias:** 

## TERM-035

**Term:** AiResourceBinding

**Owner:** CRM

**Definition:** A CRM-owned link from a resource or party to a technical agent identity.

**Distinction:** Does not contain an independent agent definition.

**Current alias:** 

## TERM-036

**Term:** ResourceProfile (staffing)

**Owner:** CRM

**Definition:** Personnel and planning properties of a resource.

**Distinction:** Not a catalog ProjectResource or ProviderProfile.

**Current alias:** 

## TERM-037

**Term:** ProjectParticipation

**Owner:** CRM

**Definition:** A project's commercial or staffing relationship and role, referencing ProjectId.

**Distinction:** Not assignment of an executor to a particular task.

**Current alias:** 

## TERM-038

**Term:** Human rate

**Owner:** CRM

**Definition:** A person's price or rate with explicit unit and effective period.

**Distinction:** Not an AI provider tariff.

**Current alias:** 

## TERM-039

**Term:** CapacityReservation

**Owner:** CRM / Staffing

**Definition:** A reservation of staffing or planning capacity for a period.

**Distinction:** Not a task assignment or runtime admission lease.

**Current alias:** 

## TERM-040

**Term:** RuntimeAdmission

**Owner:** Agents / Processes / Workflows

**Definition:** Technical acceptance of a particular execution under current limits and permissions.

**Distinction:** A planned reservation does not guarantee a healthy provider.

**Current alias:** 

## TERM-041

**Term:** Project

**Owner:** Projects

**Definition:** A project's portfolio and lifecycle identity, including phases.

**Distinction:** More than a root node in a graph.

**Current alias:** 

## TERM-042

**Term:** ProjectHierarchy

**Owner:** Projects

**Definition:** Project-to-project relationships and their rules.

**Distinction:** A drawn Project Structure edge must not become their alternative master.

**Current alias:** 

## TERM-043

**Term:** Project Structure

**Owner:** Project Structure

**Definition:** A native authoring graph connected to projections from source domains.

**Distinction:** Neither merely a cache nor the owner of every domain.

**Current alias:** 

## TERM-044

**Term:** NativeNode

**Owner:** Project Structure

**Definition:** A node whose own content and lifecycle originate in Project Structure.

**Distinction:** Being created by an agent does not transfer it to Agents ownership.

**Current alias:** 

## TERM-045

**Term:** SimpleNote

**Owner:** Project Structure

**Definition:** A native inline text note whose body is canonical content here.

**Distinction:** Not a file-node description or lazy file storage.

**Current alias:** 

## TERM-046

**Term:** ProjectedNode

**Owner:** Source owner and projection adapter

**Definition:** A read model of a foreign entity or event.

**Distinction:** Not an independently editable copy of the source object.

**Current alias:** 

## TERM-047

**Term:** ReferenceNode / occurrence

**Owner:** Project Structure

**Definition:** A locally owned placement of an EntityReference, optionally with a local alias or annotation.

**Distinction:** Removing an occurrence does not delete the source.

**Current alias:** 

## TERM-048

**Term:** StructuralLink

**Owner:** Project Structure

**Definition:** A locally owned semantic structural relationship between allowed endpoints.

**Distinction:** Not automatically a task dependency or process edge.

**Current alias:** 

## TERM-049

**Term:** Projection edge

**Owner:** Source owner

**Definition:** A display of a relationship that is canonical elsewhere.

**Distinction:** Must not be changed through a generic Link command.

**Current alias:** 

## TERM-050

**Term:** WorkItem / Task

**Owner:** Work Management

**Definition:** Canonical planned work with a goal, constraints and acceptance conditions.

**Distinction:** Not every node, and not a ProcessStep.

**Current alias:** 

## TERM-051

**Term:** WorkAssignment

**Owner:** Work Management

**Definition:** Selection of an executor, role or resource for work, with revision and scope.

**Distinction:** Not a CRM duplicate AssignedTo field or a run start.

**Current alias:** 

## TERM-052

**Term:** TaskDependency

**Owner:** Work Management

**Definition:** A predecessor/successor relationship with planning rules.

**Distinction:** Not an arbitrary visual line.

**Current alias:** 

## TERM-053

**Term:** Schedule (work plan)

**Owner:** Work Management

**Definition:** Planned dates, durations and constraints of work.

**Distinction:** Not a cron SchedulerPlan.

**Current alias:** 

## TERM-054

**Term:** Gantt

**Owner:** UI and Work Management

**Definition:** A view of the same work plan and an adapter for its mutations.

**Distinction:** Not a separate task database.

**Current alias:** 

## TERM-055

**Term:** Milestone

**Owner:** Work Management or Structure, according to kind

**Definition:** A planning point with typed temporal meaning.

**Distinction:** Its date is not merely free-form subtitle text.

**Current alias:** 

## TERM-056

**Term:** Acceptance

**Owner:** Work owner

**Definition:** A decision that a result satisfies the assignment and may close the task.

**Distinction:** An exit code or a Completed run alone is not acceptance.

**Current alias:** 

## TERM-057

**Term:** Asset

**Owner:** Content owner and Structure binding

**Definition:** A content artifact with identity, version, MIME type and provenance that can be attached to the graph.

**Distinction:** Not merely a path or a base64 note without storage identity.

**Current alias:** 

## TERM-058

**Term:** StorageObject

**Owner:** Storage

**Definition:** Managed physical bytes and their logical locator and revision.

**Distinction:** Need not belong exclusively to one Project Structure node.

**Current alias:** 

## TERM-059

**Term:** ProjectResource

**Owner:** Resources

**Definition:** A reusable catalog material or connector reference.

**Distinction:** Not every generated asset must automatically become a catalog resource.

**Current alias:** 

## TERM-060

**Term:** ArtifactManifest

**Owner:** Execution owner

**Definition:** An inventory of one run's outputs, including identities, roles and provenance.

**Distinction:** A UI node set-difference is not reliable evidence of authorship.

**Current alias:** 

## TERM-061

**Term:** AssetBinding

**Owner:** Reference owner

**Definition:** A relationship from a domain object to an asset or content.

**Distinction:** Does not confer ownership of all bytes behind a URL.

**Current alias:** 

## TERM-062

**Term:** Managed output root

**Owner:** Processes/runtime and Storage

**Definition:** A run's output namespace with scope and liveness policy.

**Distinction:** An external target alias or absolute path is not authority.

**Current alias:** 

## TERM-063

**Term:** Contribution

**Owner:** Project Structure API

**Definition:** An authorized request to create, attach or update specific content or a relationship in Project Structure.

**Distinction:** Not a direct foreign-table write or unrestricted command injection; see ModuleOperation for the general cross-module case.

**Current alias:** 

## TERM-064

**Term:** EnsureContribution

**Owner:** Project Structure API

**Definition:** Idempotent creation of a traceable result for a specific producer intent identity.

**Distinction:** Not perpetual recreation of a node the user deleted.

**Current alias:** 

## TERM-065

**Term:** ContributionReceipt

**Owner:** Project Structure

**Definition:** A durable acceptance or application result with created identities and revision.

**Distinction:** Not merely an in-memory TaskCompletionSource.

**Current alias:** 

## TERM-066

**Term:** Required contribution

**Owner:** Orchestration owner

**Definition:** A required completion effect whose rejection or pending state must be visible.

**Distinction:** Required grants no additional permission, quota or priority.

**Current alias:** 

## TERM-067

**Term:** Producer lineage

**Owner:** Runtime and destination receipt

**Definition:** Origin run, step and output identity established by trusted orchestration.

**Distinction:** Not a field a model may rewrite without verification.

**Current alias:** 

## TERM-068

**Term:** ProcessDefinition

**Owner:** Processes

**Definition:** A versioned or otherwise explicitly identified process orchestration definition.

**Distinction:** Not an agent workflow or a task.

**Current alias:** 

## TERM-069

**Term:** ProcessRun

**Owner:** Processes

**Definition:** A particular persisted process instance under runtime authority.

**Distinction:** A Structure status label cannot write its state.

**Current alias:** 

## TERM-070

**Term:** ProcessStep

**Owner:** Processes

**Definition:** A part of process execution and its lifecycle.

**Distinction:** May be projected beside a task, but is not the task's second identity.

**Current alias:** 

## TERM-071

**Term:** ProcessStepAssignment

**Owner:** Processes

**Definition:** Binding of one runtime step to a selected executor.

**Distinction:** Not an editable replacement work plan.

**Current alias:** 

## TERM-072

**Term:** Subprocess

**Owner:** Processes

**Definition:** A separate run with explicit parent/run lineage and allocated authority.

**Distinction:** Not an unrelated process with a similar name.

**Current alias:** 

## TERM-073

**Term:** WorkflowDefinition / Version

**Owner:** Agents / Workflows

**Definition:** A technical AI pipeline definition with a specific version.

**Distinction:** Live editing must not alter a run already accepted.

**Current alias:** 

## TERM-074

**Term:** WorkflowRun

**Owner:** Workflow runtime

**Definition:** Execution of a compiled definition with backend, checkpoints and result.

**Distinction:** Not a single LastRunId in node metadata.

**Current alias:** 

## TERM-075

**Term:** WorkflowExecutor

**Owner:** Execution capability owner

**Definition:** A typed implementation of a workflow node.

**Distinction:** The generic runtime need not know every product executor.

**Current alias:** 

## TERM-076

**Term:** LaunchIntent

**Owner:** Caller and execution owner

**Definition:** A verified start request, including inputs, versions, origin and idempotency identity.

**Distinction:** Not an acceptance result or proof of a started run.

**Current alias:** 

## TERM-077

**Term:** LaunchReceipt

**Owner:** Execution owner

**Definition:** Confirmation that an existing or new run was accepted, including run identity.

**Distinction:** A Project Structure writeback failure cannot retroactively make it Rejected.

**Current alias:** 

## TERM-078

**Term:** Writeback

**Owner:** Destination owner

**Definition:** Attaching an execution result to its destination domain.

**Distinction:** Not repeating the inference or business action itself.

**Current alias:** 

## TERM-079

**Term:** Approval

**Owner:** Policy and execution owner

**Definition:** A decision about exact scope, action and relevant revision.

**Distinction:** Not a permanent blanket grant or bypass of security checks.

**Current alias:** 

## TERM-080

**Term:** SchedulerPlan

**Owner:** SchedulerPlanner

**Definition:** A temporal rule for invoking a target operation.

**Distinction:** Not a Gantt task plan.

**Current alias:** 

## TERM-081

**Term:** SchedulerFire

**Owner:** SchedulerPlanner

**Definition:** One identified firing with deduplication and dispatch state.

**Distinction:** Redelivery of a trigger must not create another run.

**Current alias:** 

## TERM-082

**Term:** SimpleChat

**Owner:** Simple Chats

**Definition:** An ordinary LLM conversation without agent tool orchestration.

**Distinction:** Not an agent with disabled buttons.

**Current alias:** 

## TERM-083

**Term:** CollaborationThread

**Owner:** Collaboration

**Definition:** A human discussion with its own membership and lifecycle.

**Distinction:** Not a Simple Chat transcript or agent execution journal.

**Current alias:** 

## TERM-084

**Term:** Plugin

**Owner:** Plugins

**Definition:** An installed extension with manifest, activation and grants.

**Distinction:** Not necessarily a connector or runtime tool.

**Current alias:** 

## TERM-085

**Term:** Connector

**Owner:** Connectors or owning integration

**Definition:** An external-system adapter with typed configuration and purpose-specific operations.

**Distinction:** A configuration form or manifest alone is not a working integration.

**Current alias:** 

## TERM-086

**Term:** SecretReference

**Owner:** Security

**Definition:** An opaque secret identity usable only for an authorized purpose.

**Distinction:** Not its resolved value, a password-bearing URL or plaintext.

**Current alias:** 

## TERM-087

**Term:** OperationId

**Owner:** Operation owner

**Definition:** An intent or operation identity stable across safe retries in a defined scope.

**Distinction:** Not a new GUID generated for every retry.

**Current alias:** 

## TERM-088

**Term:** Idempotency key

**Owner:** Owner

**Definition:** A scoped key that returns the original effect or receipt for the same normalized request.

**Distinction:** The same key with different payload is a conflict, not silent success.

**Current alias:** 

## TERM-089

**Term:** Outbox

**Owner:** Source owner

**Definition:** A durable publication intent stored with the source change.

**Distinction:** Not an exactly-once guarantee for an external effect.

**Current alias:** 

## TERM-090

**Term:** Inbox / dedupe ledger

**Owner:** Destination owner

**Definition:** Processing evidence stored atomically with its local effect.

**Distinction:** An in-memory lock cannot replace it across restarts or instances.

**Current alias:** 

## TERM-091

**Term:** Tombstone

**Owner:** Lifecycle owner

**Definition:** A traceable deletion or suppression preventing old messages from resurrecting an identity.

**Distinction:** More than a missing row that an ensure operation can ignore.

**Current alias:** 

## TERM-092

**Term:** Committed / Accepted / Unconfirmed

**Owner:** Result owner and transport

**Definition:** Committed confirms a write; Accepted confirms operation admission; Unconfirmed describes observer uncertainty.

**Distinction:** None automatically means business success.

**Current alias:** 

## TERM-093

**Term:** Projection watermark

**Owner:** Source and projector

**Definition:** A confirmed processing position for a specific source and generation.

**Distinction:** There is no automatic global watermark across all modules.

**Current alias:** 

## TERM-094

**Term:** Migration authority

**Owner:** Schema composition

**Definition:** One controlling history and source of schema definition for each table.

**Distinction:** Runtime modules must not independently create the same tables.

**Current alias:** 

## TERM-095

**Term:** Compatibility adapter

**Owner:** Integration owner

**Definition:** A temporary mapping from an old API to the single new writer.

**Distinction:** Not a second independent engine or dual-write path.

**Current alias:** 

## TERM-096

**Term:** CapabilityUnavailable

**Owner:** Owner and host

**Definition:** An explicitly absent or disallowed functional path.

**Distinction:** Not an empty success or a fake production implementation.

**Current alias:** 

## TERM-097

**Term:** Owner operation

**Owner:** Target domain

**Definition:** Typed query or command with owner-defined semantics, scope, validation, and outcome.

**Distinction:** Not arbitrary CRUD or a tool name implying authority.

**Current alias:** 

## TERM-098

**Term:** Runtime tool adapter

**Owner:** Product integration

**Definition:** Registered mapping from governed model invocation to a typed owner operation.

**Distinction:** Not the owner of destination data or an HTTP route.

**Current alias:** 

## TERM-099

**Term:** Workflow owner-operation executor

**Owner:** Workflow integration

**Definition:** Typed executor invoking a destination owner under persisted run/step authority and effect identity.

**Distinction:** Not an impersonated HR interactive tool session.

**Current alias:** 

## TERM-100

**Term:** Process owner-operation step

**Owner:** Process integration

**Definition:** Registered step using owner operations and process-owned checkpoint/recovery.

**Distinction:** Not direct foreign context access.

**Current alias:** 

## TERM-101

**Term:** Managed agent identity

**Owner:** Agents

**Definition:** Technical identity with host-approved purpose and capabilities for a managed preset.

**Distinction:** Display name/system prompt is not identity or privilege.

**Current alias:** 

## TERM-102

**Term:** Definition administration

**Owner:** Definition-owning domain

**Definition:** Validated create/update/status control of a versioned definition.

**Distinction:** Not running the definition, reading transcripts, or rewriting history.

**Current alias:** 

## TERM-103

**Term:** Command intent versus attempt

**Owner:** Originating coordinator and target owner

**Definition:** Logical intended effect survives retries; attempts identify transport/worker executions.

**Distinction:** A new attempt does not authorize a new contact or chat.

**Current alias:** 

## TERM-104

**Term:** Owner operation receipt

**Owner:** Destination owner

**Definition:** Durable evidence of admission/commit and exact effects, recoverable under current access policy.

**Distinction:** Post-hoc logging alone cannot establish atomic idempotency.

**Current alias:** 

## TERM-105

**Term:** Execution disclosure permission

**Owner:** Source/security policy at integration

**Definition:** Permission to send selected data to the actual LLM/provider/tool in the current purpose.

**Distinction:** Not implied by permission to view the same data in UI.

**Current alias:** 

## TERM-106

**Term:** Destination disclosure permission

**Owner:** Source and destination policy

**Definition:** Permission to persist/publish data to a specific audience/location.

**Distinction:** Summarization is not automatic declassification.

**Current alias:** 

## TERM-107

**Term:** Runtime surface observation

**Owner:** Evidence catalog

**Definition:** Pinned code/documentation observation of an actual provider/API/executor surface.

**Distinction:** Not a runtime PASS or an automatic grant.

**Current alias:** 

## TERM-108

**Term:** Staffing resource versus catalog Resource

**Owner:** CRM versus Resources

**Definition:** Staffing resource represents planning capacity/personnel; catalog Resource represents reusable information/materials.

**Distinction:** Neither is synonymous with Storage bytes.

**Current alias:** 

## TERM-109

**Term:** Step effect slot

**Owner:** Execution owner

**Definition:** Stable occurrence/iteration/action/output key persisted before retriable dispatch.

**Distinction:** Not a random tool call identifier or retry counter.

**Current alias:** 

## TERM-110

**Term:** Required domain effect

**Owner:** Business orchestration owner

**Definition:** An explicit obligation whose application, authorized waiver, or safe compensation is needed for business completion.

**Distinction:** Not a privilege override or proof technical execution failed.

**Current alias:**
