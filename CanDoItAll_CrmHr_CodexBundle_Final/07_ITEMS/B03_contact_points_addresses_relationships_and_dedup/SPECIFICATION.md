# Specification

## Objective

Finish the party directory by implementing contact methods, addresses, role assignments, relationship editors, import/export flows, and a safe duplicate merge experience.

## Scope

- Implement editor sections for contact methods, addresses, relationships, and tags.
- Add duplicate detection heuristics using normalized contact values and names.
- Provide safe merge confirmation and history consolidation rules.
- Add import/export actions with validation feedback instead of silent failures.
- Show org/relationship summaries in the detail view.

## Services and entities involved

**Services**

- `PartyDirectoryService`

**Entities / concepts**

- `PartyContactPoint`
- `PartyAddress`
- `PartyRelationship`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **DIR-06** As a delivery manager, I can register multiple contact methods for one party so teams can see email, phone, messaging, and web contact points in one place.
- **DIR-07** As an office manager, I can maintain billing, legal, work, and shipping addresses per party so the app can support normal enterprise operations.
- **DIR-08** As a team lead, I can assign tags and classifications such as region, department, capability, and strategic segment so the registry is filterable.
- **DIR-09** As an org designer, I can create parent-child and peer relationships between organizations and units so the module can represent legal entities and delivery structures.
- **DIR-10** As an HR manager, I can define reporting and membership relationships between people and units so org structure and manager chains are explicit.
- **DIR-11** As a CRM administrator, I can detect and merge duplicate parties so email history, opportunities, and assignments converge on one source of truth.
- **DIR-12** As a data steward, I can import parties from CSV without losing validation feedback so bulk onboarding is practical.
- **DIR-13** As a data steward, I can export filtered party lists so business users can share snapshots or audit the directory.
- **HR-29** As a vendor manager, I can represent a subcontractor company and the individual subcontractor separately so commercial and operational relationships are not blurred.
- **HR-30** As a delivery director, I can reuse one person as employee, candidate, customer stakeholder, or partner contact when reality requires it so duplication does not grow.
- **X-12** As a data steward, I can import and export without corrupting duplicate handling or key relationships so bulk operations remain safe.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- A party can hold multiple contact methods and addresses.
- Parent-child and reporting relationships can be created and edited.
- Duplicate merge preserves related history instead of orphaning it.
- Import/export flows are available and validated in browser evidence.
