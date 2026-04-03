# Current participant and AI-agent state

## Existing project-side participant model

The current project structure already knows about people-like actors through:

- `ProjectObjectType.Participant` in `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `ProjectParticipantMetadata` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- participant creation entries in `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs`

### Current participant kinds

- `Hr`
- `TeamBlock`
- `TeamSection`
- `Freelancer`
- `Partner`
- `AiAgent`

### Current participant metadata fields

- `ParticipantKind`
- `Role`
- `Organization`
- `Email`
- `Phone`
- `ParentParticipantArtifactId`

### Current meeting and work-item references

- `ProjectMeetingMetadata.ParticipantIds` stores participant-node artifact ids
- `ProjectWorkItemMetadata.AssigneeParticipantArtifactId` stores one participant-node artifact id

## Why the current model is not enough

The current participant model is useful for local project structure authoring, but it does **not** provide:

- global reuse across projects
- customer / prospect / partner lifecycle management
- HR workforce ownership and staffing views
- recruitment and onboarding
- AI-agent provider binding
- account or opportunity visibility
- search/activity semantics beyond project-local workbench context

## Existing AI-agent reality

AI agents currently exist in two disconnected places:

1. **Workbench participant kind** (`AiAgent`)  
   Good for project structure nodes and local collaboration context.

2. **Workspace provider profiles** (`ProviderProfile`)  
   Good for runtime configuration, default models, health, and execution capability.

That means the app already distinguishes **AI identity** from **AI runtime configuration**, but does not yet connect them through a shared domain model.

## Migration decision used by this bundle

The bundle keeps the project-side participant concept and upgrades it like this:

1. **Central truth becomes `Party`.**
2. **Participant nodes remain valid project objects.**
3. `ProjectParticipantMetadata` gains a `PartyId` reference to the central directory.
4. Meeting and work-item metadata continue to support node-level references for local project behavior, but also gain stable central-party linkage where needed.
5. Project-side participant flows offer two paths:
   - **Pick existing central party**
   - **Create new central party and immediately project it into the structure**
6. A participant may be marked **project-local only** when the team intentionally does not want a global directory record.

## AI-agent migration decision

- `PartyType = AiAgent` becomes the shared identity layer.
- `AiAgentProfile` stores provider-profile binding, model defaults, capability notes, stewardship, and validation state.
- Workbench and Prompt Factory can both reuse the same AI-agent identity.
- Provider credentials and runtime settings stay in `Workspace`, not duplicated inside CRM/HR.

## Validation consequences

Any implementation is incomplete if it:

- deletes participant nodes outright,
- creates a second AI-agent registry disconnected from `Workspace`,
- breaks meeting participant selection on the project structure side,
- or forces every local project participant to become a global directory record.

The correct implementation is a **projection + linkage model**, not a destructive replacement.
