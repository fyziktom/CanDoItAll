# Implementation prompt

Implement **B08 — Recruitment pipeline, interviews, onboarding, and offboarding** for CanDoItAll.

## Bundle goal

Implement candidate handling, interview scheduling, structured feedback, hiring conversion, onboarding and offboarding task management, and lifecycle reminders.

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

- Implement candidate records, recruitment stages, interview scheduling, feedback, and hiring conversion.
- Create onboarding and offboarding tasks with ownership and due dates.
- Add reminder-friendly task state for automation.

## Stories that must be satisfied in this bundle

- **HR-19** As a recruiter, I can create a candidate record in the same unified registry so future employees and contractors do not start in a disconnected tool.
- **HR-20** As a recruiter, I can track candidate stage from sourced to hired or rejected so recruitment progress is visible.
- **HR-21** As a recruiter, I can schedule interviews and record interview dates so hiring coordination is structured.
- **HR-22** As a hiring manager, I can capture interview feedback and recommendation so decision quality is documented.
- **HR-23** As a people ops manager, I can convert a hired candidate into an employee or contractor profile so recruiting handoff is seamless.
- **HR-24** As a people ops manager, I can create onboarding tasks with owner and due date so new joiners do not rely on ad hoc follow-up.
- **HR-25** As a people ops manager, I can create offboarding tasks with owner and due date so exits are controlled.
- **HR-26** As a mentor coordinator, I can assign manager, buddy, or mentor relationships for onboarding so support roles are visible.
- **HR-27** As an IT coordinator, I can track access and equipment checklist items during onboarding or offboarding so delivery readiness is observable.
- **X-15** As an automation owner, I can trigger reminders and onboarding follow-up jobs from CRM/HR data so the module participates in operational automation.

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
