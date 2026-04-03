# Implementation prompt

Implement **B01 — Foundation: unified party domain, schema, and module skeleton** for CanDoItAll.

## Bundle goal

Create the new CRM/HR module project, full relational schema, seed strategy, service registration, startup wiring, and core DTOs around a unified Party model that can represent persons, organizations, organization units, and AI agents.

## Hard rules

- follow `03_ARCHITECTURE/*` and `02_REQUIREMENTS/SCOPE_AND_NON_FUNCTIONAL_DECISIONS.md`
- keep UI in BaseLib / Razor / HTML only
- do not introduce canvas components
- preserve backward compatibility for existing project/workbench flows where relevant
- add or update tests listed in `FILE_REFERENCES.md`
- add screenshot evidence requirements from `SCREENSHOT_REQUIREMENTS.md`

## Implementation steps

1. Inspect all files in `FILE_REFERENCES.md`.
2. Implement the data model / service changes required for this bundle.
3. Implement the route or UI changes required for this bundle.
4. Wire search/activity/integration seams if this bundle requires them.
5. Add automated tests at the correct level.
6. Execute browser validation and capture screenshots.
7. Write a concise evidence note summarizing code changes, tests, and screenshots.

## Bundle-specific targets

- Create the new module project and service registration entry point.
- Add all base EF entities, configurations, and schema-initializer logic.
- Wire the module assembly into startup and app model discovery.
- Define shared editor models / summaries for parties, CRM, HR, AI agents, and project assignments.
- Seed default lifecycle and stage options where the UI needs deterministic startup data.

## Stories that must be satisfied in this bundle

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

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
