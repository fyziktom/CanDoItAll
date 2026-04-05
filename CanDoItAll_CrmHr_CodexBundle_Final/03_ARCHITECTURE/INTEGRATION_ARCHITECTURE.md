# Integration architecture

## 1. Projects integration

### Goal

Projects must become relationship-aware without turning `Project` into a giant CRM/HR blob.

### Recommended design

- Keep `Project` focused on project identity and delivery metadata.
- Add `ProjectPartyAssignment` as the main relationship bridge.
- Enrich `ProjectSummary` queries with primary related-party labels computed from assignments.
- Update the Projects page to show:
  - primary customer
  - delivery unit
  - project manager or owner
  - linked opportunity state where present

### Example assignment kinds

- Customer
- CustomerContact
- DeliveryUnit
- TeamMember
- Manager
- Partner
- Vendor
- Reviewer
- AiAgent
- BillingContact
- TechnicalContact

## 2. Workbench integration

### Current reality

Workbench already stores participant, meeting, and work-item metadata inside project structure.

### Target integration

- `ProjectParticipantMetadata.PartyId` links a participant node to central directory identity.
- `ProjectParticipantMetadata.IsProjectLocalOnly` allows exceptions.
- Meeting editors use the central party picker and ensure participant projections exist when needed.
- Work-item editors store central assignee linkage and optionally maintain the local participant-node link.
- For generic project-structure nodes that need responsibility, store one or more `ProjectPartyAssignment` records keyed by `ProjectId + NodeKey`.

### Practical sequence

1. User picks a central party in project structure.
2. System checks whether a participant projection node already exists in that project.
3. If not, system creates or offers to create one.
4. Node metadata stores the stable `PartyId`.
5. `ProjectPartyAssignment` is created or updated.
6. HR capacity and CRM visibility can now reference the same actor.

## 3. CRM opportunity conversion to project

### Required outcome

When a won opportunity becomes delivery work, CanDoItAll must not retype data.

### Recommended sequence

```mermaid
sequenceDiagram
    participant User
    participant CRM as CrmService
    participant Projects as ProjectsService
    participant Links as ProjectPartyIntegrationService
    participant Workbench as ProjectWorkbenchService

    User->>CRM: Mark opportunity as Won
    User->>CRM: Convert to project
    CRM->>Projects: Create project shell or attach existing project
    CRM->>Links: Copy customer / partner / delivery-unit / owner links
    Links->>Workbench: Seed optional participant projections
    Links->>CRM: Back-link opportunity to project
```

### Rules

- Conversion may create a new project or attach to an existing one.
- Existing project assignments should not be duplicated.
- Opportunity history remains intact after conversion.

## 4. Workspace / AI-agent integration

### Reuse point

`Workspace` already stores provider profiles and capability flags.

### Integration rule

- `AiAgentProfile` references `ProviderProfileId`
- provider secrets remain in `Workspace` / `Security`
- CRM/HR owns:
  - agent identity
  - agent purpose
  - stewardship / owner
  - validation state
  - assignment visibility

This separation avoids duplicating runtime configuration inside business identity records.

## 5. Resources integration

### Needed enhancement

`ProjectResource` should optionally link to owning or maintaining parties.

Potential additions:

- `OwnerPartyId`
- `MaintainerPartyId`

This enables:

- partner-owned repositories,
- delivery-unit-owned folders,
- AI-agent-owned prompt links,
- clear operational accountability.

## 6. Validation and Test Lab integration

### Validation

Add optional responsible-party fields such as:

- `OwnerPartyId`
- `ReviewerPartyId`
- `ApprovedByPartyId`

### Test Lab

Add optional accountable-party fields such as:

- `OwnerPartyId`
- `ReviewerPartyId`
- `EvidenceCapturedByPartyId`

These do not need to become deeply coupled; they just need stable central-party references.

## 7. Search and activity integration

### Search

Every major CRM/HR entity writes a `SearchDocument`:

- party
- opportunity
- interaction summary
- workforce profile
- recruitment application
- AI agent

### Activity

Every major CRM/HR mutation writes an `ActivityEntry`:

- create/update/archive party
- merge duplicate
- log interaction
- move opportunity stage
- convert opportunity to project
- create allocation
- hire candidate
- complete onboarding/offboarding
- update AI-agent review status

### Privacy rule

Confidential notes and sensitive details do **not** go into broad search payloads.

## 8. Automation integration

The current Automation module is light, but CRM/HR can still reuse it by surfacing jobs for:

- overdue CRM next actions
- onboarding/offboarding tasks due soon
- contract or assignment end dates
- stale opportunities
- stale candidate workflows

The implementation can start with job visibility and reminder generation, not a full workflow engine.

## 9. Integration success conditions

The integration architecture is correct only if all of these are true:

- Projects can show and filter by customer / partner / delivery unit / owner.
- Workbench can assign central parties without breaking local project structure flows.
- AI agents have one shared identity reused across Workspace, Projects, and Prompt/Workbench flows.
- Search and activity know about CRM/HR entities.
- Validation, tests, and resources can point to accountable parties.
- Opportunity conversion to project preserves relationship data.
