# Specification

## Objective

Implement candidate handling, interview scheduling, structured feedback, hiring conversion, onboarding and offboarding task management, and lifecycle reminders.

## Scope

- Implement candidate records, recruitment stages, interview scheduling, feedback, and hiring conversion.
- Create onboarding and offboarding tasks with ownership and due dates.
- Add reminder-friendly task state for automation.

## Services and entities involved

**Services**

- `HrService`
- `PartyDirectoryService`

**Entities / concepts**

- `RecruitmentApplication`
- `RecruitmentInterview`
- `OnboardingTask`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

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

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- Candidates move through recruitment stages with history preserved.
- Interviews and feedback persist.
- Hiring conversion can create workforce identity without duplicating the person.
- Onboarding/offboarding tasks are visible and actionable.
