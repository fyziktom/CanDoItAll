# Implementation prompt

Implement **B09 — AI agent profiles, provider bindings, capabilities, and governance** for CanDoItAll.

## Bundle goal

Make AI agents a first-class party type with provider bindings, human ownership, capability records, validation status, and directory visibility.

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

- Create AI-agent profiles tied to Party and Workspace provider profiles.
- Store owner/steward, execution mode, capability notes, and review state.
- Make AI agents discoverable and assignable through CRM/HR pages.

## Stories that must be satisfied in this bundle

- **AI-01** As an AI operations lead, I can register an AI agent as a first-class party so the app can assign and report on agents like people or companies.
- **AI-02** As an AI operations lead, I can link an AI agent profile to a Workspace provider profile and default model so operational configuration is connected to the directory.
- **AI-03** As a solution architect, I can record agent capabilities, limitations, tool access, and scope so assignments are safe and understandable.
- **AI-04** As a delivery lead, I can assign a human owner or steward to an AI agent so accountability exists.
- **AI-06** As a quality lead, I can capture validation notes and latest review status for an AI agent so risky agents are visible.
- **AI-07** As a workspace administrator, I can distinguish local, remote, and third-party agents so infrastructure and risk posture are explicit.
- **AI-08** As a delivery lead, I can search agents in the same directory and assignment flows as people so blended staffing stays unified.
- **DIR-01** As a business director, I can create one unified party record for a person, organization, organization unit, or AI agent so CRM and HR do not split the same real-world actor across modules.
- **DIR-02** As an operations lead, I can classify one party with multiple roles such as customer, partner, employee, contractor, delivery unit, or AI agent owner so the same record can participate in different contexts.

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
