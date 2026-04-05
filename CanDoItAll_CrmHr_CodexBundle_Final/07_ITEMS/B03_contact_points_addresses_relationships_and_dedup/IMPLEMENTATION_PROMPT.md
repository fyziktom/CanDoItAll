# Implementation prompt

Implement **B03 — Contact points, addresses, relationships, org structure, import/export, and duplicate merge** for CanDoItAll.

## Bundle goal

Finish the party directory by implementing contact methods, addresses, role assignments, relationship editors, import/export flows, and a safe duplicate merge experience.

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

- Implement editor sections for contact methods, addresses, relationships, and tags.
- Add duplicate detection heuristics using normalized contact values and names.
- Provide safe merge confirmation and history consolidation rules.
- Add import/export actions with validation feedback instead of silent failures.
- Show org/relationship summaries in the detail view.

## Stories that must be satisfied in this bundle

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

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
