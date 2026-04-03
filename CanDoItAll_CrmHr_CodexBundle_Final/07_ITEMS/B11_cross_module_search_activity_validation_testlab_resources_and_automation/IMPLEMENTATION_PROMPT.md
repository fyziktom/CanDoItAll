# Implementation prompt

Implement **B11 — Cross-module integration with search, activity, resources, validation, test lab, and automation** for CanDoItAll.

## Bundle goal

Finish enterprise integration by indexing CRM/HR artifacts, writing activity events, linking owners to resources, validation, and tests, and wiring reminder-style automation jobs.

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

- Write CRM/HR search documents and activity entries.
- Add owner/responsible-party links to Resources, Validation, and Test Lab where relevant.
- Expose reminder-style automation jobs for stale next actions and lifecycle tasks.
- Prove cross-module visibility.

## Stories that must be satisfied in this bundle

- **DIR-14** As a project manager, I can see a party activity timeline so I understand the latest interactions, assignments, and changes before acting.
- **DIR-15** As an executive assistant, I can open a party directly from global search so the directory behaves as a first-class application surface.
- **CRM-20** As a commercial operations lead, I can receive reminders for overdue next actions so opportunities do not stall silently.
- **PRJ-12** As a quality lead, I can link validation runs and test plans to responsible parties so accountability is clear.
- **PRJ-13** As a resource owner, I can link resources to owning or maintaining parties so operational ownership is visible.
- **X-02** As a platform owner, I can index parties, interactions, opportunities, workforce records, and agent profiles in global search so the module is discoverable.
- **X-03** As a platform owner, I can write activity entries for major CRM/HR changes so the timeline reflects relationship work.
- **X-08** As a platform owner, I can seed default opportunity stages, relationship stages, and other lookup values so the module works immediately after startup.
- **X-15** As an automation owner, I can trigger reminders and onboarding follow-up jobs from CRM/HR data so the module participates in operational automation.

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
