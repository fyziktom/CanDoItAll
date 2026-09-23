# Reviewed API and domain terminology

107 source-grounded entries. Definitions are reviewed against the pinned sources; the preferred wording is a proposed standard, not a claim that every current comment already follows it. This is a curated vocabulary, not a complete audit of every public schema.

Use the same qualified term for the same concept. Do not rename existing JSON members, route segments, operation IDs, enum tokens or C# symbols merely to match these prose labels. Preserve externally defined protocol vocabulary. Every endpoint still needs a field-level source check.

Evidence IDs resolve through `../evidence/sources.json`. Source symbols are anchors, not an assertion that all are HTTP properties.

## API fundamentals

### API-001 — HTTP operation
One HTTP method and route handled by the web application. It has its own request, response and authorization contract.

**Do not conflate with:** agent tool; application service method.
**Anchors:** `MapGet`, `MapPost`, `MapPut`
**Evidence:** P03, P06, P15.

### API-002 — OpenAPI document
The generated machine-readable description of the HTTP surface of a particular build and configuration.

**Do not conflate with:** Swagger UI; timeless source of runtime truth.
**Anchors:** `/openapi/v1.json`, `/swagger/v1/swagger.json`
**Evidence:** P03, S03.

### API-003 — Swagger UI
The interactive documentation frontend displaying the generated OpenAPI document. It does not generate this product's contract.

**Do not conflate with:** Swagger generator.
**Anchors:** `UseSwaggerUI`, `AddOpenApi`
**Evidence:** P01, P02, P03.

### API-004 — wire DTO
A serialized request or response shape exposed over a transport, including internal C# types reachable from endpoints.

**Do not conflate with:** persistence entity; every public C# type.
**Anchors:** `PartyCreateApiRequest`, `LlmChatDefinitionApiResponse`
**Evidence:** P09, P13.

### API-005 — domain model
An owner-managed representation of business facts and rules; its shape is not automatically an HTTP contract.

**Do not conflate with:** wire DTO; UI view model.
**Anchors:** `Party`, `Opportunity`, `ProjectTaskEstimatePolicy`
**Evidence:** P11, P12, P08.

### API-006 — projection
A representation derived from authoritative owner data for a particular reader or purpose. It is not an alternative writer.

**Do not conflate with:** authoritative record; editable master copy.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02.

### API-007 — required property
A JSON member whose presence the applicable transport contract requires. Presence and acceptance of null are separate decisions.

**Do not conflate with:** non-nullable property; C# public property.
**Anchors:** `[JsonRequired] CurrentCostBasis`
**Evidence:** P05, F04.

### API-008 — nullable property
A member whose contract permits a JSON null value. This does not imply the member may be omitted.

**Do not conflate with:** optional property; clear command in every DTO.
**Anchors:** `CurrentCostBasis`, `ScheduleChange`
**Evidence:** P05, F04.

### API-009 — omitted property
A JSON member that is absent. Its effect is defined by the particular reader, default, update semantics and validation.

**Do not conflate with:** null; empty string; empty collection.
**Anchors:** `JsonIgnoreCondition.WhenWritingNull`, `PageIndex ?? 0`
**Evidence:** P09, P13.

### API-010 — replacement
An operation submitting the complete intended collection for the named owner/target. Omitted existing rows may be removed under the owner contract.

**Do not conflate with:** patch; append.
**Anchors:** `PartyRelationshipsReplaceApiRequest`, `ReplaceCrmHrPartyRelationships`
**Evidence:** P09, P10, S04.
**Rule:** Describe empty-list behavior after verifying the owner; never describe a replacement as an incremental update.

### API-011 — page index
An integer position in a page-based collection. The inspected CRM query defaults to page index zero.

**Do not conflate with:** cursor; one-based page number everywhere.
**Anchors:** `CrmHrPartyPageApiQuery.PageIndex`
**Evidence:** P09.
**Rule:** Specify the origin per endpoint; do not globalize the CRM default.

### API-012 — cursor
A continuation value returned by a cursor-paged API. Send it back through the matching continuation contract rather than treating it as a page number.

**Do not conflate with:** page index; authorization token.
**Anchors:** `LlmChatApiPage<T>.NextCursor`, `NextMessageCursor`
**Evidence:** P13.

### API-013 — error envelope
The endpoint-specific serialized container for failure information. This product has more than one envelope.

**Do not conflate with:** ProblemDetails for all operations; one universal Errors type.
**Anchors:** `ApiErrorResponse.Errors`, `Error.ErrorCode`
**Evidence:** P14, P06, P07.

### API-014 — correlation identifier
A diagnostic/run-correlation reference, not evidence that a mutation was committed and not automatically an idempotency key.

**Do not conflate with:** idempotency key; receipt.
**Anchors:** `ApiErrorResponse.CorrelationId`
**Evidence:** P14, E01.

## Authority and identity

### AUTH-001 — API bearer authorization
HTTP authentication and endpoint policy enforcement when configured. It is not an agent capability grant.

**Do not conflate with:** agent permission; project lease.
**Anchors:** `ApiAuthorizationPolicies`, `Authorization: Bearer`
**Evidence:** P02, P03, P15.

### AUTH-002 — authorization scope
A permission category tested by an authorization policy. State the concrete policy and accepted scopes for the operation.

**Do not conflate with:** query scope; workspace scope.
**Anchors:** `HasScope`, `HasApiOrSpecificScope`
**Evidence:** P02.

### AUTH-003 — query scope
A filter selecting which categories of records a query may return; it is not an access grant.

**Do not conflate with:** authorization scope.
**Anchors:** `PartyRecordScope`, `RecruitmentApplicationScope`
**Evidence:** P09.

### AUTH-004 — workspace scope
The organization/project or other admitted workspace context under which an operation resolves data and files.

**Do not conflate with:** filesystem path; bearer scope.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02, P15, E01.

### AUTH-005 — project write admission
The owner-provided evidence binding a write to the project lifetime that the caller read. The HTTP task update requires the returned admission even though its C# property is nullable.

**Do not conflate with:** lease token; bearer token; client-generated version.
**Anchors:** `ProjectWriteAdmission`, `ExpectedProjectAdmission`
**Evidence:** P05, P06, S02.

### AUTH-006 — project lifetime
A particular incarnation of a project used to fence stale writes and projections; equality of the public project identifier alone is insufficient.

**Do not conflate with:** project identifier; request duration.
**Anchors:** `ExpectedProjectAdmission`, `ProjectLifetimeId`
**Evidence:** S02, P12, P06.

### AUTH-007 — lease
A coordination claim under a defined scope/key and validity period. Do not present it as a substitute for authorization or lifetime admission.

**Do not conflate with:** project write admission; ownership of business facts.
**Anchors:** `ProjectStructureLeaseSnapshot`, `LeaseToken`
**Evidence:** P07, S04.

### AUTH-008 — concurrency token
An owner-returned value representing the state a later mutation expects. Preserve its exact representation and obtain a fresh value by reading the owner.

**Do not conflate with:** revision reason; timestamp in every API; client counter.
**Anchors:** `ExpectedConcurrencyToken`, `ConcurrencyToken`
**Evidence:** P13.

### AUTH-009 — revision
A version in a specified owner stream. Qualify it as definition, transcript, assignment or publication revision; these are not interchangeable.

**Do not conflate with:** one global revision; concurrency token by default.
**Anchors:** `DefinitionRevision`, `TranscriptRevision`, `CurrentDirectAssignmentRevision`
**Evidence:** P13, P05, P16.

### AUTH-010 — external code
A business-supplied reference on a party. The inspected persistence mapping indexes it without declaring it unique.

**Do not conflate with:** party identifier; guaranteed idempotency key.
**Anchors:** `Party.ExternalCode`
**Evidence:** P11.

## Project and task

### PROJ-001 — project
The Projects-owned business container and lifecycle record to which project structure and participation are attached.

**Do not conflate with:** Project Structure node; workspace directory.
**Anchors:** `projectId`, `ProjectsService`
**Evidence:** S02, P06.

### PROJ-002 — Project Structure
The Workbench-owned hierarchy and related task, node, link and asset operations within a project.

**Do not conflate with:** Projects lifecycle API; generic filesystem tree.
**Anchors:** `/api/project-structure`
**Evidence:** S02, P06.

### PROJ-003 — node identifier
An opaque structure-node key returned by the owner. It is not universally a GUID.

**Do not conflate with:** projectId; display label.
**Anchors:** `nodeId`, `ProjectStructureNodeSummary.Id`
**Evidence:** P06, P07.

### PROJ-004 — canonical task
A task governed by the typed task application boundary, not an arbitrary WorkItem created through a generic node mutation.

**Do not conflate with:** generic node; recruiting lifecycle task.
**Anchors:** `project_task_update`, `/tasks`, `ProjectWorkItemKind.Task`
**Evidence:** P07, P18.

### PROJ-005 — task identifier
The string node identifier of the canonical task. In task-update HTTP requests the body and route identifiers must match exactly.

**Do not conflate with:** GUID wrapper object; task title.
**Anchors:** `TaskId`, `taskId`
**Evidence:** P05, P06.

### PROJ-006 — current task state
The previously read task values used as preconditions for an edit; not guesses and not copies of the desired new values.

**Do not conflate with:** proposed task state; latest state silently substituted at save time.
**Anchors:** `CurrentTitle`, `CurrentEstimate`, `CurrentExecution`
**Evidence:** P05, P18, P07.

### PROJ-007 — proposed task state
The caller's intended new task values, checked against current owner state and domain rules.

**Do not conflate with:** already committed state.
**Anchors:** `ProposedTitle`, `ProposedEstimate`, `ProposedExecution`
**Evidence:** P05, P18.

### PROJ-008 — current progress percentage
Previously read task progress. The inspected validator accepts the untracked sentinel -1 or a tracked value from 0 through 100.

**Do not conflate with:** always 0..100; execution state.
**Anchors:** `CurrentProgressPercent`
**Evidence:** P18.

### PROJ-009 — proposed progress percentage
Requested tracked progress, accepted only from 0 through 100 by the task-details validator.

**Do not conflate with:** -1 untracked input; probability of winning an opportunity.
**Anchors:** `ProposedProgressPercent`
**Evidence:** P18.

### PROJ-010 — task execution snapshot
The current or proposed explicit execution state and related actual timestamps; it is separate from displayed progress and planned schedule.

**Do not conflate with:** schedule interval; run result.
**Anchors:** `CurrentExecution`, `ProposedExecution`, `ActualStartedAtUtc`, `ActualEndedAtUtc`
**Evidence:** P18.

### PROJ-011 — schedule change
A typed schedule edit identifying affected tasks and their previous/proposed intervals, with a gesture and optional critical-task information.

**Do not conflate with:** actual execution timestamps; only the edited task always moves.
**Anchors:** `ProjectStructureTaskScheduleAgentChange`, `AffectedTasks`
**Evidence:** P05.

### PROJ-012 — schedule interval
The start and end timestamps of a planned task interval. Preserve offsets/UTC semantics and owner constraints; do not silently convert to date-only values.

**Do not conflate with:** effort; calendar-day duration in every case.
**Anchors:** `PreviousStart`, `PreviousEnd`, `ProposedStart`, `ProposedEnd`
**Evidence:** P05, P07.

### PROJ-013 — expected effort hours
Effort stored in hours, even when the chosen input/display unit is ManDays. Conversion belongs to the effort policy.

**Do not conflate with:** raw value in the selected unit; elapsed schedule hours.
**Anchors:** `ProjectTaskEstimate.ExpectedEffortHours`
**Evidence:** P08.

### PROJ-014 — effort unit
The chosen Hours or ManDays input/display unit. It does not change the stored unit of ExpectedEffortHours.

**Do not conflate with:** currency unit.
**Anchors:** `ProjectWorkItemEffortUnit`, `ExpectedEffortUnit`
**Evidence:** P08.

### PROJ-015 — hours per man-day
The conversion factor used by the effort policy; its default is eight, and overloads accept an explicit positive factor.

**Do not conflate with:** universal working calendar.
**Anchors:** `DefaultHoursPerManDay`
**Evidence:** P08.

### PROJ-016 — expected cost
A nonnegative estimated amount with its associated currency, distinct from historical charges and recognized sales.

**Do not conflate with:** cost rate; recognized sales total.
**Anchors:** `ExpectedCostAmount`, `ExpectedCostCurrencyCode`
**Evidence:** P08, S02.

### PROJ-017 — expected cost basis
The owner-returned basis for an expected task cost, used with the edit precondition. The member can be null but CurrentCostBasis must be present in this JSON input.

**Do not conflate with:** a freely edited price; zero cost when null.
**Anchors:** `ProjectTaskExpectedCostBasis`, `CurrentCostBasis`
**Evidence:** P05, P18.

### PROJ-018 — direct-assignment revision
The nonnegative owner revision for a task's direct assignments, submitted unchanged as an edit precondition.

**Do not conflate with:** number of assignees; task progress; project revision.
**Anchors:** `CurrentDirectAssignmentRevision`
**Evidence:** P05, P18.

### PROJ-019 — direct task assignee
A person or agent selected through the typed task-assignment path. This update does not accept process/workflow selections as direct assignees.

**Do not conflate with:** every task resource kind; project participant.
**Anchors:** `AssigneeChanged`, `ProposedAssignee`
**Evidence:** P18.

### PROJ-020 — task resource
A typed execution/resource selection for a task. The separate resource-attachment endpoint can handle cases beyond direct person/agent assignment.

**Do not conflate with:** storage object; only a person.
**Anchors:** `ProjectStructureTaskResourceSelection`, `/tasks/{taskId}/resource`
**Evidence:** P07, P18.

### PROJ-021 — project participation
CRM/HR-owned involvement and staffing facts in a project, distinct from Workbench-owned task assignments.

**Do not conflate with:** direct task assignment.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02.

### PROJ-022 — asset
A Project Structure content record/placement whose metadata and actual bytes are retrieved through distinct supported operations.

**Do not conflate with:** metadata proves file content; arbitrary local file path.
**Anchors:** `/assets/{nodeId}`, `/assets/{nodeId}/content`
**Evidence:** S04, S02.

### PROJ-023 — canonical-current read
A read through the authoritative current Project Structure service; the explicit HTTP read source.

**Do not conflate with:** invocation snapshot.
**Anchors:** `ProjectStructureReadSource.CanonicalCurrent`
**Evidence:** P15, S04.

### PROJ-024 — invocation snapshot
A bounded snapshot attached to an eligible in-process agent invocation; unavailable as an HTTP read source and never silently replaced by canonical data.

**Do not conflate with:** downloaded OpenAPI snapshot; any cached project.
**Anchors:** `ProjectStructureReadSource.InvocationSnapshot`
**Evidence:** P15, S04.

## CRM and HR

### CRM-001 — party
A CRM/HR business identity used by roles, contacts, relationships and workforce records. Qualify the type; a party is not necessarily a customer or person.

**Do not conflate with:** user account; customer in every case.
**Anchors:** `Party`, `PartyId`, `PartyType`
**Evidence:** P11, P09.

### CRM-002 — person party
A party representing a human; its party ID is distinct from login identity, workforce-profile ID and recruitment-application ID.

**Do not conflate with:** all parties; agent definition.
**Anchors:** `PartyType.Person`, `PartyId`
**Evidence:** P09, P12.

### CRM-003 — CRM account
The commercial account identified by AccountPartyId, with a separate CRM account profile. It is not an authentication account.

**Do not conflate with:** user login; account-profile ID.
**Anchors:** `AccountPartyId`, `CrmAccountProfile`
**Evidence:** P12, S02.

### CRM-004 — CRM account profile
Commercial relationship-stage and note fields for an account party; its own Id is not AccountPartyId.

**Do not conflate with:** party identity record.
**Anchors:** `CrmAccountProfile.Id`, `RelationshipStage`
**Evidence:** P12.

### CRM-005 — account connection
A typed link from an account to another party, optionally associated with project links.

**Do not conflate with:** generic party relationship; login connection.
**Anchors:** `CrmAccountConnection`, `RelatedPartyId`
**Evidence:** P12.

### CRM-006 — party relationship
A typed directed source-party/target-party relationship with its own identity and optional dates.

**Do not conflate with:** account connection in every context; affiliation ID.
**Anchors:** `PartyRelationship.SourcePartyId`, `TargetPartyId`
**Evidence:** P11.

### CRM-007 — party role assignment
A business role attached to a party, with its own title, primary flag and optional validity interval.

**Do not conflate with:** authorization role; task assignee.
**Anchors:** `PartyRoleAssignment`
**Evidence:** P11, P09.

### CRM-008 — contact point
A typed contact value attached to a party, with a label and primary/public flags. It is not itself a person identity.

**Do not conflate with:** contact person; party.
**Anchors:** `PartyContactPoint`, `PartyPublicContactCreateApiRequest`
**Evidence:** P11, P09.

### CRM-009 — public contact
A contact created through the inspected PublicContacts input; the mapper marks it IsPublic=true. Do not infer internet-public authorization from this data flag.

**Do not conflate with:** anonymous API access.
**Anchors:** `PublicContacts`, `IsPublic`
**Evidence:** P09.

### CRM-010 — sensitive party
A party marked for restricted handling by its owner. Visibility and redaction still depend on the actual read endpoint and current authority.

**Do not conflate with:** all its data is absent in every response; public flag.
**Anchors:** `Party.IsSensitive`
**Evidence:** P11, S02.

### CRM-011 — confidential note
A separate restricted CRM/HR note record, not Party.Notes or Party.Summary. The inspected party-create API does not accept this collection.

**Do not conflate with:** ordinary note; summary.
**Anchors:** `PartyConfidentialNote`, `ConfidentialNotes`
**Evidence:** P11, P09.

### CRM-012 — opportunity
A prospective commercial engagement associated with an account, owner, stage, optional value and project link.

**Do not conflate with:** project; invoice; CRM account.
**Anchors:** `Opportunity`
**Evidence:** P12.

### CRM-013 — opportunity stage
The commercial lifecycle position of an opportunity; not the account relationship stage or party lifecycle status.

**Do not conflate with:** account relationship stage; workforce status.
**Anchors:** `Opportunity.Stage`, `OpportunityStageHistory`
**Evidence:** P12.

### CRM-014 — probability percentage
The opportunity's ProbabilityPercent value; it describes commercial likelihood, not execution progress. Validate its allowed range at the owner before adding constraints.

**Do not conflate with:** task progress.
**Anchors:** `Opportunity.ProbabilityPercent`
**Evidence:** P12.

### CRM-015 — recognized amount
The monetary value retained on an opportunity-stage history record for recognition, distinct from its current editable opportunity amount.

**Do not conflate with:** invoice payment; current quoted amount.
**Anchors:** `OpportunityStageHistory.RecognizedAmount`, `RecognizedCurrencyCode`
**Evidence:** P12.

### CRM-016 — workforce profile
A workforce-specific record linked to a party, with work classification, capacity, rates and managerial fields. Profile Id and PartyId have different meanings.

**Do not conflate with:** person identity; technical agent definition.
**Anchors:** `WorkforceProfile.Id`, `PartyId`
**Evidence:** P12, P10.

### CRM-017 — internal cost rate
The workforce cost amount expressed per RateUnit and in RateCurrencyCode, not a total task estimate.

**Do not conflate with:** external billing rate; expected cost total.
**Anchors:** `InternalCostRate`, `RateUnit`, `RateCurrencyCode`
**Evidence:** P12.

### CRM-018 — external billing rate
The workforce billing amount per the declared rate unit and currency, distinct from internal cost.

**Do not conflate with:** internal cost rate.
**Anchors:** `ExternalBillingRate`
**Evidence:** P12.

### CRM-019 — capacity block
A dated capacity restriction/reservation fact for a party, with a block kind, percentage and optional project reference. It is not itself a task assignment.

**Do not conflate with:** task; workforce profile.
**Anchors:** `CapacityBlock`
**Evidence:** P12.

### CRM-020 — staffing request
A request for a role/skills and allocation over an interval, optionally bound to a project and its lifetime.

**Do not conflate with:** confirmed assignment; recruitment application.
**Anchors:** `StaffingRequest`
**Evidence:** P12.

### CRM-021 — skill definition
A catalog definition of a workforce skill. This is not an executable agent skill package.

**Do not conflate with:** Codex/API skill; agent capability.
**Anchors:** `SkillDefinition`
**Evidence:** P12, S07.

### CRM-022 — party skill
A party's recorded proficiency/experience in a referenced workforce skill definition.

**Do not conflate with:** skill definition itself.
**Anchors:** `PartySkill`, `SkillId`, `PartyId`
**Evidence:** P12.

### CRM-023 — recruitment application
A candidate party's application for a role, with its own identity, stage and decision. Qualify application to avoid confusion with the software product.

**Do not conflate with:** software application; person party.
**Anchors:** `RecruitmentApplication`, `ApplicationId`
**Evidence:** P12.

### CRM-024 — recruitment interview
An interview belonging to a recruitment application, with scheduling, interviewer and outcome fields.

**Do not conflate with:** application; general interaction record.
**Anchors:** `RecruitmentInterview`
**Evidence:** P12.

### CRM-025 — recruiting lifecycle task
An onboarding/offboarding task associated with a party and optionally a project. Not a canonical Project Structure task.

**Do not conflate with:** canonical task.
**Anchors:** `OnboardingTask`, `LifecycleTaskKind`
**Evidence:** P12.

### CRM-026 — interaction record
A CRM communication/activity record with subject, summary and optional next-action/related opportunity or project information.

**Do not conflate with:** agent chat message; execution run.
**Anchors:** `InteractionRecord`
**Evidence:** P12.

### CRM-027 — next action
A follow-up associated with a CRM interaction, optionally assigned and due-dated; not an implicit canonical task.

**Do not conflate with:** Project Structure task.
**Anchors:** `NextActionText`, `NextActionOwnerPartyId`, `NextActionDueUtc`
**Evidence:** P12.

## Agents and conversations

### AGT-001 — technical agent definition
An Agents-owned executable agent configuration and identity. CRM exposes a projection/binding rather than another authoritative technical definition.

**Do not conflate with:** CRM party; model.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02, P15.

### AGT-002 — agent capability
A declared/assigned capability that participates in runtime eligibility checks. A catalog entry alone does not make a tool executable.

**Do not conflate with:** HTTP permission; automatically attached tool.
**Anchors:** `IAgentRuntimeToolProvider`, `RuntimeToolProviderComposer`
**Evidence:** P15.

### AGT-003 — runtime tool
An executable function attached to an eligible invocation through the runtime provider/composition policy.

**Do not conflate with:** HTTP operation; capability template.
**Anchors:** `AITool`, `project_task_update`
**Evidence:** P15, P05.

### AGT-004 — tool call
One invocation of a runtime function, with arguments and an outcome. Distinguish a call, its retry attempt and the business target.

**Do not conflate with:** whole execution run; HTTP operationId.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** P15, E01.

### AGT-005 — agent execution run
A governed agent execution instance, not the entire chat session and not a workflow/process run.

**Do not conflate with:** chat session; workflow run; process run.
**Anchors:** `ExecutionRunId`, `ChatSessionId`
**Evidence:** P14, P15.

### AGT-006 — provider profile
An owner-managed provider configuration selected by a ProviderProfileId. Distinguish it from a provider kind, model name and shared publication.

**Do not conflate with:** model identifier; shared publication ID.
**Anchors:** `ProviderProfileId`, `ProviderKind`
**Evidence:** P13, P16, S02.

### AGT-007 — model identifier
The model-selection value used within a specific provider/transport contract. It is not the provider profile identifier.

**Do not conflate with:** DTO model; ProviderProfileId.
**Anchors:** `Model`, `SharedProviderRoutingModelId`
**Evidence:** P13, P16.

### AGT-008 — shared-provider publication
A published provider capability identified by its publication identity/revision; not the caller's local provider profile.

**Do not conflate with:** local provider profile.
**Anchors:** `SharedProviderPublicationId`, `SharedProviderPublicRevision`
**Evidence:** P16.

### AGT-009 — shared routing model identifier
The routing value accepted by the shared-provider relay as model; do not substitute a private upstream configuration ID.

**Do not conflate with:** local ProviderProfileId.
**Anchors:** `SharedProviderRoutingModelId`
**Evidence:** P16.

### AGT-010 — Simple Chat definition
A reusable ordinary LLM chat configuration with revisions, provider/model settings and optional response format. It is not a governed agent.

**Do not conflate with:** technical agent definition; conversation.
**Anchors:** `LlmChatDefinitionApiResponse`
**Evidence:** P13, P15.

### AGT-011 — Simple Chat conversation
A conversation referring to a definition and definition revision, with transcript revision and optional active operation.

**Do not conflate with:** definition; agent chat session.
**Anchors:** `LlmChatConversationApiResponse`
**Evidence:** P13.

### AGT-012 — conversation turn
The conversational unit identified by TurnId; messages/entries and active operation IDs are separate identities.

**Do not conflate with:** message entry; definition revision.
**Anchors:** `LlmChatMessageApiResponse.TurnId`, `EntryId`, `ActiveOperationId`
**Evidence:** P13.

### AGT-013 — message entry
One recorded conversation message with role, content and its own EntryId, associated with a turn.

**Do not conflate with:** entire turn.
**Anchors:** `LlmChatMessageApiResponse`
**Evidence:** P13.

### AGT-014 — transcript revision
The revision of conversation history used in relevant read/edit preconditions, distinct from definition revision and conversation concurrency token.

**Do not conflate with:** definition revision; token count.
**Anchors:** `ExpectedTranscriptRevision`, `TranscriptRevision`
**Evidence:** P13.

### AGT-015 — token usage
Provider-reported input, output and cached-input usage quantities, not a monetary cost or authorization token.

**Do not conflate with:** API bearer token; price.
**Anchors:** `LlmChatUsageApiResponse`
**Evidence:** P13.

## Execution and effects

### RUN-001 — workflow definition
The reusable workflow configuration; distinguish the definition/catalog identity from a particular workflow execution.

**Do not conflate with:** workflow run; process definition.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02, P15.

### RUN-002 — workflow run
A particular workflow execution, with its own status, external responses and recovery/observation contract.

**Do not conflate with:** agent execution run; workflow definition.
**Anchors:** `WorkflowRunStartApiResponse`, `/api/workflows`
**Evidence:** P15, S03.

### RUN-003 — process run
A particular process orchestration execution; it can contain steps and agent/workflow work without sharing their identifiers.

**Do not conflate with:** workflow run; agent execution run.
**Anchors:** `/api/processes`
**Evidence:** P15, S02.

### RUN-004 — committed effect
An authoritative owner-observed write/effect. A transport success, a generated answer or a completed read is not by itself proof of commitment.

**Do not conflate with:** HTTP 200; no error message.
**Anchors:** `RecordCommitted`, `EffectState`
**Evidence:** P15, E01.

### RUN-005 — known no-effect rejection
A rejection whose owner proves that the relevant effect did not occur. Do not use it for arbitrary failures after dispatch.

**Do not conflate with:** all exceptions; unknown effect.
**Anchors:** `RecordRejectedBeforeEffect`, `None`, `NotCommitted`
**Evidence:** E01, S02.
**Rule:** Do not treat None and NotCommitted enum values as interchangeable; describe their actual owner-specific contract.

### RUN-006 — unknown effect
An outcome for which the system cannot prove whether the effect committed. Reconciliation, not blind repetition, is required.

**Do not conflate with:** definitely failed write; safe-to-retry guarantee.
**Anchors:** `Unknown`
**Evidence:** E01, S02.

### RUN-007 — reconciliation
Owner-backed determination/recovery of an uncertain or partially delivered operation using retained identity/evidence; it is not a new business mutation by default.

**Do not conflate with:** rerun; automatic retry.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02, P15.

### RUN-008 — result disclosure
Permission to expose a previously saved tool result under current authority. It is distinct from permission to execute another mutation.

**Do not conflate with:** tool execution permission.
**Anchors:** `AuthorizeResultDisclosureAsync`
**Evidence:** P15.

### RUN-009 — receipt
Owner-produced evidence identifying a committed effect or coordinated operation. State the exact receipt type and its guarantees.

**Do not conflate with:** correlation identifier; request ID.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02, P15.

## Storage and schemas

### STO-001 — storage object
Content addressed through a storage provider/catalog and locator contract. Do not describe it as an arbitrary filesystem path.

**Do not conflate with:** Project Structure node; workforce resource.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02, S04.

### STO-002 — locator
A storage-provider-specific reference to content; its accepted syntax and visibility are contract-specific.

**Do not conflate with:** URL in every case; absolute local path.
**Anchors:** Owner contract and behavior guidance.
**Evidence:** S02, E01.

### STO-003 — content bytes
The actual stored content returned by a content endpoint/stream, distinct from metadata, filename or a placement record.

**Do not conflate with:** metadata; successful file registration.
**Anchors:** `/assets/{nodeId}/content`
**Evidence:** S04.

### STO-004 — placement
The association/delivery of content into an owner-managed location, potentially with separate recovery and continuation evidence.

**Do not conflate with:** content creation itself; mere file path.
**Anchors:** `/api/storage-placement-recovery`
**Evidence:** S02, S03.

### STO-005 — response-format schema
The schema governing a requested model response format. It is not the OpenAPI schema of the surrounding HTTP DTO.

**Do not conflate with:** OpenAPI document; database schema.
**Anchors:** `LlmChatResponseFormatApiRequest.Schema`
**Evidence:** P13.

### STO-006 — JSON-valued member
A member serialized as JSON data, with its own allowed shape; it is not necessarily a string containing serialized JSON.

**Do not conflate with:** JSON string; free-form metadata by default.
**Anchors:** `JsonElement ModelParameterConfiguration`, `JsonElement Schema`
**Evidence:** P13.

### STO-007 — UTF-8 body byte limit
A request-body limit measured in encoded bytes, distinct from character counts. Verify the limit for each endpoint.

**Do not conflate with:** UTF-16 text length.
**Anchors:** `MaximumBodyBytes`
**Evidence:** P17, P16.

### STO-008 — UTF-16 text length
The text-length unit explicitly used by the inspected shared-provider relay subset; it is not a global API length convention.

**Do not conflate with:** UTF-8 byte length; Unicode grapheme count.
**Anchors:** `MaximumTextCharacters`
**Evidence:** P16.

