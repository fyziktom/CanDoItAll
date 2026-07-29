# CRM/HR party affiliations, workforce pool, and agent access

Status: Accepted for implementation, 2026-07-29

## Outcome

CRM/HR will represent a person's relationship to an organization with a dedicated
`PartyOrganizationAffiliation` record. This becomes the canonical source for the
organization-scoped facts that are currently missing or duplicated across global
party roles, generic party relationships, and workforce profiles.

The large-screen Workforce page remains an assignment-oriented people pool. It
continues to include outside contacts because project work can legitimately be
assigned to someone who is not our employee or engaged freelancer. Each card must
make the distinction explicit:

- `Employee`
- `Contractor`
- `Freelancer`
- `External contact`
- `Delivery unit`

`External contact` is the persisted business term. “Not ours” is rejected as a
canonical value because it is tenant-relative and unclear in exports, APIs, and
audit history. UI help text explains that an external contact has no staffable
employee, contractor, or freelancer affiliation.

The affiliation badge is shown in the card's upper-left corner. Party lifecycle
remains in the upper-right corner. The visible relationship line shows the current
primary organization and title. Its tooltip lists the other current affiliations.

## Problem and evidence

The existing Workforce population predicate treats every `Person` as workforce.
The legacy unpaged workforce query then gives a person without a profile the
default kind `Employee`. This is why a person related to a customer or vendor
appears equivalent to an internal employee.

The existing candidate sources are not suitable canonical replacements:

- `PartyRoleAssignment` has no organization endpoint, so `Employee` or
  `Freelancer` cannot say *for which company*.
- `PartyRelationship` has endpoints but no affiliation kind, job title, employee
  code, organizational unit, or manager.
- `WorkforceProfile` assumes one resource-planning profile per party and cannot
  represent several simultaneous or historical organizations.
- `CrmAccountConnection` describes account responsibility such as billing contact
  or sponsor. That is separate from the company with which a person is affiliated.
- `PartyRelationship.IsPrimary` is edge-global, although “primary” must be scoped
  to the person whose affiliations are being viewed.

`WorkforceProfile.PartyId` is also queried as one-to-one but currently has only a
non-unique database index. The implementation must align the database constraint
with the existing read invariant.

## Canonical ownership

| Concern | Canonical owner | Notes |
|---|---|---|
| Shared identity and record lifecycle | `Party` | Lifecycle is directory lifecycle, not employment or recruiting state. |
| Person-to-company affiliation | `PartyOrganizationAffiliation` | Organization-scoped kind, title, unit, manager, dates, and primary marker. |
| Staffable resource planning | `WorkforceProfile` | Discipline, seniority, rates, capacity, location, and time zone. |
| Skills and availability | `PartySkill`, `CapacityBlock` | Remain party/resource concerns. |
| Account responsibility | `CrmAccountConnection` | Primary/billing/technical contact, sponsor, account manager, and similar CRM roles. |
| Generic non-affiliation relationships | `PartyRelationship` | Reporting, ownership, support, partner, and other residual graph edges. |
| Recruiting lifecycle | `RecruitmentApplication` | Candidate and hiring state are not party lifecycle. |
| Customer lifecycle | `CrmAccountProfile` | Prospect/customer stage is not party lifecycle. |
| Project and task participation | `ProjectPartyAssignment` | Stores the selected party and optionally the affiliation that explains the assignment context. |
| Search/chat/card data | Projections | Never become authorization or canonical state. |

The product repository owns these domain choices. SharedInfo remains the owner of
cross-repository engineering standards only.

## Common CRM model review

The existing model already separates shared party identity, contact points,
addresses, account profiles, account stakeholders, opportunities and stage
history, interactions, recruitment, skills, capacity, and project assignments.
Adding another generic “contact type” or a tenant-relative “ours/not ours” flag
would duplicate those sources of truth.

This slice therefore adds only the missing organization-scoped affiliation and
the optional project-assignment reference to it. Two broader improvements are
recorded but intentionally not hidden inside the affiliation change:

- party plus affiliation editing currently has explicit retryable partial-success
  behavior; a future atomic save requires extracting the party command core so a
  focused coordinator can use one `AppDbContext`;
- recruiting “hire” conversion should eventually close the application and
  create the affiliation/profile in one transaction.

Neither concern is solved with `TransactionScope` across factory-created
contexts or duplicated persistence logic.

## Affiliation model

```text
PartyOrganizationAffiliation
  Id
  PersonPartyId
  OrganizationPartyId
  AffiliationKind
    Employee
    Contractor
    Freelancer
    ExternalContact
  IsPrimary
  JobTitle
  EmployeeCode
  OrganizationUnitPartyId?
  ManagerPartyId?
  ValidFromUtc?
  ValidToUtc?
  Notes
  LastChangedBy
  CreatedAtUtc
  UpdatedAtUtc
```

The first rollout keeps legacy organization-scoped fields readable on
`WorkforceProfile` for contract compatibility, but all new UI and agent writes use
the affiliation service. Compatibility projection is one-way: legacy profile data
may seed an affiliation; affiliation and global role fields must not remain two
independently editable truths.

## Invariants

1. `PersonPartyId` references a `PartyType.Person`.
2. `OrganizationPartyId` references `PartyType.Organization`.
3. `OrganizationUnitPartyId`, when supplied, references
   `PartyType.OrganizationUnit`.
4. `ManagerPartyId`, when supplied, references `PartyType.Person`.
5. The person, organization, unit, and manager identifiers cannot create a
   self-reference.
6. An end date cannot precede a start date.
7. Duplicate affiliations with the same person, organization, kind, and effective
   interval are rejected.
8. At most one affiliation per person is marked primary. Changing the primary
   affiliation clears the old marker in the same transaction; audit history
   preserves the transition.
9. Historical affiliation rows remain evidence and may be corrected only through
   an explicit audited edit.
10. `ExternalContact` does not implicitly create a `WorkforceProfile`, rates,
    capacity, skills, or an employee/freelancer global role.
11. Employee, contractor, or freelancer affiliation may have a staffable profile,
    but the profile is created through an explicit command.
12. `WorkforceProfile.PartyId` is unique.
13. Outside people remain valid project/task assignees. Project presentation must
    disclose their affiliation; it must not silently treat them as internal.
14. If `ProjectPartyAssignment.PartyOrganizationAffiliationId` is supplied, it
    must belong to the same person and be effective for the assignment interval.
15. Sensitive party details, rates, confidential notes, and private contact points
    are never exposed through default agent projections.

## Migration and compatibility

The PostgreSQL migration will:

1. create the affiliation table and foreign keys;
2. add indexes for person/current-primary lookup, organization lookup, and
   organization-unit lookup;
3. add a partial unique index for the one affiliation marked primary per person;
4. add the optional affiliation foreign key to project-party assignments;
5. make `WorkforceProfile.PartyId` unique; migration fails visibly if invalid
   duplicate data exists rather than deleting records silently;
6. seed an affiliation only when the legacy evidence is unambiguous:
   a person has a workforce profile and its home unit can resolve to an
   organization, or a single compatible person-to-organization membership exists;
7. leave ambiguous role-only or account-connection evidence unconverted.

No account connection is automatically interpreted as employment. No person is
automatically persisted as an external contact merely because the Workforce pool
shows the safe fallback classification.

## Query design and EF Core performance

The current unbounded `ListWorkforceDirectoryAsync` path is not used for the
large-screen catalog. A focused `IWorkforceRecordQueryService` owns a bounded,
source-paged projection.

```text
count filtered candidates
        +
page party identities
        |
        +-- one page-scoped affiliation query
        +-- one page-scoped legacy-profile compatibility query
        +-- one page-scoped relationship fallback query
        |
        +--> materialized WorkforceRecordPage
```

Closure criteria:

- all read queries use `AsNoTracking`;
- page size and search length remain bounded;
- enrichment uses page identifiers and a constant number of queries, never N+1;
- only required columns are projected;
- no `Include`-driven cartesian expansion;
- count and page ordering are stable;
- affiliation and lifecycle filters execute in SQL;
- search uses existing normalized columns where prefix/equality semantics permit;
- PostgreSQL integration tests capture commands and enforce the query budget;
- the legacy unbounded query is either removed from active UI use or clearly
  retained as a compatibility API with no new callers.

## Large-screen user-story map

| Area | User story | Required UI/behavior |
|---|---|---|
| Directory | Find one shared identity without duplicating a person per company. | Server-paged party search and one canonical party editor. |
| Directory | Record several current or historical company affiliations. | Affiliation editor in the Relations tab with kind, company, unit, manager, title, dates, and primary choice. |
| Directory | Understand the main relationship immediately. | Primary affiliation summary in the dialog header; other current affiliations in a tooltip/list. |
| Directory | Avoid contradictory primaries and duplicate intervals. | Inline explanation and service validation; failed saves keep edits open. |
| Workforce | Distinguish internal, engaged, and outside people. | Top-left affiliation badge and affiliation filter for Employee, Contractor, Freelancer, External contact, and Delivery unit. |
| Workforce | Keep directory lifecycle visible independently. | Existing top-right Draft/Active/etc. corner badge. |
| Workforce | See the person's main company and title. | Primary relationship line; tooltip lists all other current affiliations. |
| Workforce | Find outside participants who may receive project work. | External contacts remain in the All and External contact filters. |
| Workforce | Avoid accidental employee creation. | External-contact workspace states “No staffable profile”; creating a profile is an explicit action. |
| Workforce | Plan actual supply. | Employee/contractor/freelancer filters expose skills, rates, capacity, and availability; external contacts do not fabricate those values. |
| CRM account | Separate employer/company membership from account responsibility. | Connected-record UI shows affiliation context beside the independent account role. |
| Opportunity | Spot a contact whose primary company differs from the account. | Non-blocking mismatch warning; no silent rewrite. |
| Projects | Assign an employee, freelancer, contractor, external contact, or AI agent. | Picker and assignment card show affiliation/company context. |
| Projects | Explain a multi-affiliated person's assignment. | Optional affiliation selection stored on CRM/HR-owned assignment. |
| Recruiting | Convert a hired candidate without partial state. | Follow-up transaction boundary creates affiliation/profile and closes recruiting state atomically. |
| Lifecycle | End employment without deleting the identity or history. | Dated affiliation remains visible; party archive is a separate decision. |
| Merge/import | Preserve organization-scoped truth. | Merge and import move or validate affiliations rather than flattening them into global roles. |
| Audit | Trace who changed affiliation and primary status. | Affiliation saves emit CRM/HR audit records with bounded detail. |
| Agents | Let HR find and create CRM people/affiliations safely. | Typed read/create tools with separate capabilities, approval for mutations, redaction, and receipts. |
| Agents | Create a specialist that can work in project structure. | Project access is an explicit typed policy on agent create/update, with separate read, non-task write, task write, project creation, and project scope fields plus safe read-back. |
| Agents | Create reusable skills/MCP setup and a role avatar. | Capability curation and avatar generation remain separate approval-gated stages from agent creation. |
| Future Sales agent | Reuse domain commands without inheriting HR authority. | CRM/HR-owned services are shared; tool providers and capability keys remain identity/record-family specific. |

Only large-screen workflows are in scope for this change. No small- or
medium-screen redesign is planned.

## Component plan

```text
CrmHrWorkforcePage
  WorkforceRecordBrowser
    PagedRecordBrowser
      WorkforceRecordPresentation
  WorkforceRecordHeader
    affiliation badge
    lifecycle badge
    primary affiliation summary
  existing server-rendered detail tabs
    explicit external-contact/no-profile state

CrmHrDirectoryPage
  existing party dialog
    Relations tab
      PartyAffiliationsEditor
        organization picker
        optional unit picker
        optional manager picker
      PartyRelationshipsEditor
      duplicate stewardship

ProjectPartyAssignmentPanel
  affiliation-aware party selection
  optional affiliation selector
  ProjectAssignmentRecordCard
    affiliation/company badge
```

Pages retain navigation, load generation, notification, and dialog lifetime.
Leaf components own formatting and local edit interaction. EF queries and
invariants do not move into Razor components.

## Application and dependency boundaries

```text
Projects contracts
        ^
        |
CRM/HR domain + application services
  PartyOrganizationAffiliationService
  WorkforceRecordQueryService
  CrmPartyCommandService
        |
        +--> CRM/HR Razor UI
        +--> CRM/HR HTTP API
        +--> AgentFramework runtime-tool adapters

AgentFramework
  HR provider + HR capability keys
  future Sales provider + Sales capability keys
```

CRM/HR owns the mutation and privacy-safe projection contracts. AgentFramework
adapts them into identity-bound runtime tools. CRM/HR never depends on
AgentFramework. Projects and Workbench receive no HR policy enum.

No new partial class is introduced. The query, policy, and affiliation service
are focused types. The existing party-merge service receives only the required
integrity path: same-type enforcement, workforce-profile deduplication,
affiliation endpoint/deduplication handling, and assignment-reference
repointing. New query and agent behavior remains outside that legacy service.

## Agent capability decision

The HR Agent already has bounded privacy-safe CRM search and summary tools. The
missing CRM capability is mutation, and the current read entitlement is too
coarse for future Sales sharing.

The implementation retains the existing search and summary capabilities and
adds separate capabilities for:

- HR party creation;
- HR party affiliation listing;
- HR affiliation creation/update;

Mutations:

- use typed DTOs only;
- require host approval by default;
- re-run identity, capability, lifecycle, and CRM-scope authorization at
  invocation time;
- accept explicit party/organization identifiers;
- do not accept raw JSON, rates, confidential notes, or private contact data;
- return canonical identifiers and a bounded audit-safe result.

The future Sales agent may reuse the CRM/HR application contracts but must have
different tool names, capability keys, record-family policy, and identity
authorization. Possession of generic CRM memory scope is not mutation authority.

Project-structure runtime tools are intentionally not capability-catalog rows.
Their source of truth is `AgentProjectStructureAccessSettings` in the agent
configuration. The formerly seeded `project-task-create` and
`project-task-update` catalog rows are retired during catalog normalization
because assigning or removing them never controlled the runtime tools. The HR
create/update contracts expose a typed access policy and return its normalized
safe projection during read-back. Nested updates are patches: omitted fields
remain unchanged, an empty ID list clears explicit scope, explicit IDs disable
all-project scope, and enabling all-project scope clears explicit IDs. Canonical
normalization promotes read for any operation or scope and prevents both scope
forms from being persisted together. A request such as Gardener receives only
read, non-task node/asset write, task write, and the explicitly selected project
scope. It does not need or receive a synthetic project-structure capability.

## Testability contract

Unit tests must cover:

- affiliation endpoint types, dates, duplicates, and primary normalization;
- classification precedence and safe external-contact fallback;
- projection of primary and other affiliation text;
- project assignment affiliation compatibility;
- agent command validation and authorization metadata;
- performance-pattern scan findings in changed production files.

Component tests must cover:

- affiliation filters and card badges;
- lifecycle corner status remains independent;
- primary relationship text and other-affiliation tooltip;
- affiliation editor primary selection and failed-save behavior;
- external-contact workspace no-profile state;
- project assignment affiliation disclosure.

PostgreSQL integration tests must cover:

- migration and foreign keys;
- unique workforce profile per party;
- one current primary affiliation per person;
- source paging and a constant query budget;
- create/update audit rows;
- project-assignment affiliation integrity;
- API contracts and privacy redaction.

Playwright tests must use only the source development host and development
database. They must verify:

1. create an organization and an outside person;
2. add an `ExternalContact` primary affiliation plus another affiliation;
3. see `External contact` in the Workforce card's upper-left corner;
4. see lifecycle in the upper-right corner;
5. see the primary company/title and all others in the tooltip;
6. filter the Workforce pool by affiliation;
7. assign the outside person to a project task and see the affiliation context;
8. ask the development HR Agent to save a reusable garden-planning skill;
9. create a Draft Gardener on Terra with the reusable skill and typed
   project-structure access, then verify normalized settings read-back;
10. generate and assign the Gardener avatar through the configured OpenAI image
    provider;
11. ask the development HR Agent to search and, after approval, create a typed CRM
    party record.

Port `38473` is explicitly excluded. Development uses the managed dotnet-watch
host (`5032`/`7271` or an allocated development port) and
`candoitall_development` or an isolated test database.

## Development verification record — 2026-07-29

The implementation was exercised through the large-screen UI on port `5032`
against `candoitall_development`; port `38473` was not used.

- The Workforce pool displayed development person
  `22f4d321-2f46-45c2-bf96-f4efd587dd30` as `External contact`, independently
  displayed its lifecycle, showed the primary company and title, exposed the
  second current affiliation in the tooltip, and showed `No staffable profile`.
  The classification corner was measured in the browser after the CSS repair:
  its `clientWidth` and `scrollWidth` were both 109 pixels, so the full label was
  visible without clipping.
- Employee classification filtering returned no result for that person;
  `External contact` filtering and a secondary-organization search each returned
  it.
- The development HR Agent saved reusable inline capability
  `garden-planning-inline-skill`
  (`acc72f4e-e68f-4397-bc5d-7c002b5a67fd`), created Draft agent Gardener
  (`41aa5f85-c003-49c2-bd8b-89766c8241ef`) on `gpt-5.6-terra`, assigned the
  skill plus the bounded spreadsheet/file capabilities, persisted normalized
  project access, and generated and assigned an OpenAI image avatar. The agent,
  avatar, model, Draft lifecycle, workload, and seven capabilities survived two
  application restarts.
- Gardener's read-back project policy allows project reads, non-task structure
  writes, and task writes across all projects. It does not allow project or
  subproject creation, and `AllowAllProjects` normalization cleared the explicit
  project ID collection.
- The development HR Agent searched for an exact organization name, requested
  approval only for `hr_crm_party_create`, created Draft Party
  `cdb55eca-0e86-4f08-bd9c-47e2f9dcbd87`, then retrieved its summary and tags.
  A restart followed by a read-only search and summary lookup returned the same
  record and made no changes.
- That CRM exercise exposed a PostgreSQL translation defect caused by composing
  filters over a positional `Candidate` projection. Search filtering, privacy
  checks, ranking, bounding, and summary ID filtering now execute on entity
  queries before projection. A PostgreSQL regression covers Party search with an
  explicit and omitted record kind, sensitive-record exclusion, and direct
  summary retrieval. The final UI replay produced no EF or application warning.
- Focused validation passed: 19 UI tests, 8 core unit tests, 6 affiliation
  PostgreSQL tests, the distinct-primary merge regression, 2 CRM command
  PostgreSQL tests, 9 project-assignment tests, 2 project-presentation tests, the
  fixed-budget Workforce query test, 13 HR-provider tests, 15 HR-policy tests,
  33 project-access unit tests, 5 project-access component tests, the retired
  capability seed integration test, 5 CRM query unit tests, the CRM query
  PostgreSQL regression, and the EF pending-model check. The final Web build
  completed with zero warnings and zero errors.

## Architecture and performance gate

The whole CRM/HR module remains blocked from a clean-architecture verdict because
several very large services/pages mix concerns. This slice improves the boundary
without claiming a whole-module rewrite:

- no new behavior is added to the unbounded workforce query;
- no new business policy is added to Razor;
- no new partial class is created;
- the focused services are directly testable;
- the unavoidable legacy merge change is covered by relational integration
  tests and does not become a new general orchestration surface;
- EF queries are bounded and projected;
- the changed dependency graph remains `Composition -> AgentFramework -> CRM/HR
  -> Projects`, with no reverse edge;
- architecture review, EF command capture, targeted tests, and development UI
  validation are required before completion.
