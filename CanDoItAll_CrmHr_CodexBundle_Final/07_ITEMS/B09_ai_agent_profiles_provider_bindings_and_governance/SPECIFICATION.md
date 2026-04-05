# Specification

## Objective

Make AI agents a first-class party type with provider bindings, human ownership, capability records, validation status, and directory visibility.

## Scope

- Create AI-agent profiles tied to Party and Workspace provider profiles.
- Store owner/steward, execution mode, capability notes, and review state.
- Make AI agents discoverable and assignable through CRM/HR pages.

## Services and entities involved

**Services**

- `AiAgentService`
- `WorkspaceService`
- `PartyDirectoryService`

**Entities / concepts**

- `AiAgentProfile`
- `Party`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **AI-01** As an AI operations lead, I can register an AI agent as a first-class party so the app can assign and report on agents like people or companies.
- **AI-02** As an AI operations lead, I can link an AI agent profile to a Workspace provider profile and default model so operational configuration is connected to the directory.
- **AI-03** As a solution architect, I can record agent capabilities, limitations, tool access, and scope so assignments are safe and understandable.
- **AI-04** As a delivery lead, I can assign a human owner or steward to an AI agent so accountability exists.
- **AI-06** As a quality lead, I can capture validation notes and latest review status for an AI agent so risky agents are visible.
- **AI-07** As a workspace administrator, I can distinguish local, remote, and third-party agents so infrastructure and risk posture are explicit.
- **AI-08** As a delivery lead, I can search agents in the same directory and assignment flows as people so blended staffing stays unified.
- **DIR-01** As a business director, I can create one unified party record for a person, organization, organization unit, or AI agent so CRM and HR do not split the same real-world actor across modules.
- **DIR-02** As an operations lead, I can classify one party with multiple roles such as customer, partner, employee, contractor, delivery unit, or AI agent owner so the same record can participate in different contexts.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- An AI agent can be created as a party and linked to a provider profile.
- AI agent detail shows capabilities, owner, execution mode, and review state.
- The same AI agent can later be used by project integration flows.
- No duplicate provider registry is introduced.
