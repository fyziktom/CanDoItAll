# Specification

## Objective

Create the new CRM/HR module project, full relational schema, seed strategy, service registration, startup wiring, and core DTOs around a unified Party model that can represent persons, organizations, organization units, and AI agents.

## Scope

- Create the new module project and service registration entry point.
- Add all base EF entities, configurations, and schema-initializer logic.
- Wire the module assembly into startup and app model discovery.
- Define shared editor models / summaries for parties, CRM, HR, AI agents, and project assignments.
- Seed default lifecycle and stage options where the UI needs deterministic startup data.

## Services and entities involved

**Services**

- `PartyDirectoryService`
- `CrmService`
- `HrService`
- `AiAgentService`
- `ProjectPartyIntegrationService`

**Entities / concepts**

- `Party`
- `PartyRoleAssignment`
- `PartyContactPoint`
- `PartyAddress`
- `PartyRelationship`
- `InteractionRecord`
- `Opportunity`
- `WorkforceProfile`
- `PartySkill`
- `CapacityBlock`
- `RecruitmentApplication`
- `RecruitmentInterview`
- `OnboardingTask`
- `AiAgentProfile`
- `ProjectPartyAssignment`
- `CrmHrAuditEntry`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **DIR-01** As a business director, I can create one unified party record for a person, organization, organization unit, or AI agent so CRM and HR do not split the same real-world actor across modules.
- **DIR-02** As an operations lead, I can classify one party with multiple roles such as customer, partner, employee, contractor, delivery unit, or AI agent owner so the same record can participate in different contexts.
- **DIR-04** As a people ops manager, I can archive and reactivate parties without deleting historical references so project, CRM, and HR history stays intact.
- **DIR-05** As a sales lead, I can store legal name, display name, preferred name, and external identifiers on a party so the record matches contractual and operational reality.
- **DIR-16** As a portfolio manager, I can model a delivery unit as an organization or organization unit instead of an employee so the system supports company-based delivery.
- **DIR-17** As a solution architect, I can attach freeform notes and structured metadata to a party so edge cases do not force schema hacks.
- **DIR-18** As a compliance lead, I can flag records that contain sensitive data so downstream screens treat them carefully.
- **DIR-19** As a support lead, I can see who last changed a party and when so ownership and accountability are visible.
- **DIR-20** As a module owner, I can extend party records with future custom fields without redesigning the entire schema so the module can evolve safely.
- **X-05** As a platform owner, I can create and seed the CRM/HR schema automatically on startup so local environments remain simple.
- **X-08** As a platform owner, I can seed default opportunity stages, relationship stages, and other lookup values so the module works immediately after startup.
- **X-09** As a data steward, I can use archive and safe-delete rules so historical relationships are not broken by aggressive cleanup.
- **X-14** As an architect, I can extend the module with JSON-backed flex fields where appropriate so future requirements do not force schema explosions.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- Fresh app startup creates the CRM/HR tables without manual intervention.
- The solution builds after module registration changes.
- Integration tests prove schema creation and at least one round-trip save/load for the Party aggregate.
- No existing module startup path is broken by the new module registration.
