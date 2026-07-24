# CRM-HR HTTP Contract

Source of truth: `src/App/CanDoItAll.Web/Api/CrmHrApi.cs`.

All routes are under `/api/crm-hr`. JSON uses the Web defaults: camel-case property names and numeric body enums. Query-string enums accept their names. Check the running OpenAPI document before operating a different build.

## Body enum encoding

The current Web serializer writes and reads body enums as integers. Use the typed client enum when possible. For shell/JSON clients, use these source-ordered values:

- `PartyType`: `Person=0`, `Organization=1`, `OrganizationUnit=2`, `AiAgent=3`.
- `PartyLifecycleStatus`: `Draft=0`, `Active=1`, `Inactive=2`, `Archived=3`, `Former=4`, `Candidate=5`, `Prospect=6`.
- `PartyRoleKind`: `Customer=0`, `CustomerContact=1`, `Partner=2`, `Vendor=3`, `Employee=4`, `Contractor=5`, `Freelancer=6`, `DeliveryUnit=7`, `Candidate=8`, `AiSteward=9`, `AccountManager=10`, `Recruiter=11`, `Stakeholder=12`.
- `PartyContactType`: `Email=0`, `Phone=1`, `Website=2`, `Messaging=3`, `Social=4`, `Other=5`.
- `PartyRelationshipKind`: `MemberOf=0`, `PartOf=1`, `ReportsTo=2`, `CustomerOf=3`, `PartnerOf=4`, `VendorTo=5`, `Represents=6`, `ManagedBy=7`, `OwnedBy=8`, `Supports=9`.
- `WorkforceKind`: `Employee=0`, `Contractor=1`, `Freelancer=2`, `DeliveryUnit=3`.
- `ProjectResourceRateUnit`: `Hour=0`, `ManDay=1`.
- `SkillProficiencyLevel`: `Basic=0`, `Working=1`, `Strong=2`, `Expert=3`.
- `CapacityBlockKind`: `Leave=0`, `Unavailable=1`, `Reserve=2`, `Tentative=3`.
- `RecruitmentStage`: `Applied=0`, `Screening=1`, `Interviewing=2`, `Offer=3`, `Hired=4`, `Rejected=5`, `Withdrawn=6`.
- `RecruitmentDecision`: `Pending=0`, `Approved=1`, `Rejected=2`, `Withdrawn=3`.
- `RecruitmentInterviewType`: `Screening=0`, `Technical=1`, `Manager=2`, `Panel=3`, `Culture=4`.
- `RecruitmentInterviewOutcome`: `Pending=0`, `StrongYes=1`, `Yes=2`, `Mixed=3`, `No=4`, `StrongNo=5`.
- `LifecycleTaskKind`: `Onboarding=0`, `Offboarding=1`.
- `LifecycleTaskStatus`: `NotStarted=0`, `InProgress=1`, `Completed=2`, `Cancelled=3`.

Do not invent numeric values. Refresh this table whenever the source enum order changes.

## Bounded reads

### Parties

`GET /parties`

Query:

- `search`: name, external code, or summary; maximum 200 characters.
- `tags`: repeat the query parameter for conjunctive tag filters.
- `scope`: flags value from `PartyRecordScope`; default `All`.
- `pageIndex`: zero based.
- `pageSize`: 1 through 100.
- `includeArchived`: default `false`.

Response: `PartyRecordPage` with `items`, `pageIndex`, `pageSize`, `totalCount`, and derived `totalPages`.

`GET /parties/{partyId}`

Returns the safe directory projection for one party or structured 404.

### Workforce

`GET /workforce`

Uses the same bounded query fields as Parties, constrained to workforce population.

`GET /workforce/{partyId}`

Returns the workforce workspace for one party or structured 404.

### Recruiting

`GET /recruiting/applications`

Query:

- `searchText`: candidate, role, source, recruiter, manager, or unit text; maximum 200 characters.
- `scope`: `All`, `Applied`, `Screening`, `Interviewing`, `Offer`, `Hired`, `Rejected`, or `Withdrawn`.
- `pageIndex`: zero based.
- `pageSize`: 1 through 100.

`GET /recruiting/applications/{applicationId}`

Returns the aggregate recruitment workspace or structured 404.

## Party commands

`POST /parties`

The request is the source-backed `PartyCreateApiRequest`. Fields:

- `partyType`
- `lifecycleStatus`
- `displayName`
- `legalName`
- `preferredName`
- `externalCode`
- `summary`
- `tags`
- `region`
- `countryCode`
- `timeZone`
- `isSensitive`
- `roles`
- `publicContacts`
- `addresses`

`displayName` is required. Child collections use typed enums and do not accept child ids, private contacts, confidential notes, normalized values, or audit fields. The API normalizes contact values and sets the actor.

`GET /parties/{partyId}/relationships`

`PUT /parties/{partyId}/relationships`

The PUT body is `{ "relationships": [...] }` and contains the complete intended relationship list. Each row contains `relatedPartyId`, `relationshipKind`, `isOutgoing`, `isPrimary`, optional `startDateUtc`/`endDateUtc`, and `notes`. The server sets the actor and assigns relationship ids.

## Workforce commands

`POST /workforce/profiles`

Body: `WorkforceProfileSaveApiRequest`. It includes `partyId`, profile fields, optional rate fields, and no client-supplied audit actor.

`GET /workforce/skills`

`POST /workforce/skills`

POST body: `SkillDefinitionSaveApiRequest`.

`POST /workforce/party-skills`

Body: `PartySkillSaveApiRequest`.

`POST /workforce/capacity-blocks`

Body: `CapacityBlockSaveApiRequest`.

The relevant enum values are:

- `WorkforceKind`: `Employee`, `Contractor`, `Freelancer`, `DeliveryUnit`.
- `SkillProficiencyLevel`: `Basic`, `Working`, `Strong`, `Expert`.
- `CapacityBlockKind`: `Leave`, `Unavailable`, `Reserve`, `Tentative`.

## Recruiting commands

`POST /recruiting/applications`

Body: `RecruitmentApplicationSaveApiRequest`.

`POST /recruiting/interviews`

Body: `RecruitmentInterviewSaveApiRequest`. Supply `scheduledAtUtc` as an ISO 8601 timestamp with an offset.

`POST /recruiting/lifecycle-tasks`

Body: `LifecycleTaskSaveApiRequest`.

`POST /recruiting/support-assignments`

Body: `RecruitmentSupportAssignmentsSaveApiRequest`.

`POST /recruiting/conversions`

Body: `RecruitmentConversionApiRequest`.

Stage values: `Applied`, `Screening`, `Interviewing`, `Offer`, `Hired`, `Rejected`, `Withdrawn`.

Decision values: `Pending`, `Approved`, `Rejected`, `Withdrawn`.

Interview type values: `Screening`, `Technical`, `Manager`, `Panel`, `Culture`.

Interview outcome values: `Pending`, `StrongYes`, `Yes`, `Mixed`, `No`, `StrongNo`.

Lifecycle task kind values: `Onboarding`, `Offboarding`.

Lifecycle task status values: `NotStarted`, `InProgress`, `Completed`, `Cancelled`.

## Response and error behavior

- Successful save operations return the existing service result value, normally a GUID, or `{ "ok": true }` for acknowledgement commands.
- Missing resources return 404 with the shared `errors` collection.
- Invalid models/references return 400 with the shared `errors` collection.
- Cancellation propagates through the HTTP request to application services.
- Handlers do not write EF entities and do not provide seed-only behavior.
