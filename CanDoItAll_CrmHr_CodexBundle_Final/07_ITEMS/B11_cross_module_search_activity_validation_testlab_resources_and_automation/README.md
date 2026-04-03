# B11 — Cross-module integration with search, activity, resources, validation, test lab, and automation

## Purpose

Finish enterprise integration by indexing CRM/HR artifacts, writing activity events, linking owners to resources, validation, and tests, and wiring reminder-style automation jobs.

## Dependencies

B01, B02, B03, B04, B05, B06, B07, B08, B09, B10

## Main stories covered

- **DIR-14** As a project manager, I can see a party activity timeline so I understand the latest interactions, assignments, and changes before acting.
- **DIR-15** As an executive assistant, I can open a party directly from global search so the directory behaves as a first-class application surface.
- **CRM-20** As a commercial operations lead, I can receive reminders for overdue next actions so opportunities do not stall silently.
- **PRJ-12** As a quality lead, I can link validation runs and test plans to responsible parties so accountability is clear.
- **PRJ-13** As a resource owner, I can link resources to owning or maintaining parties so operational ownership is visible.
- **X-02** As a platform owner, I can index parties, interactions, opportunities, workforce records, and agent profiles in global search so the module is discoverable.
- **X-03** As a platform owner, I can write activity entries for major CRM/HR changes so the timeline reflects relationship work.
- **X-08** As a platform owner, I can seed default opportunity stages, relationship stages, and other lookup values so the module works immediately after startup.
- **X-15** As an automation owner, I can trigger reminders and onboarding follow-up jobs from CRM/HR data so the module participates in operational automation.

## Main routes

- `/activity`
- `/resources`
- `/validation`
- `/test-lab`
- `/automation`
- `/crm-hr`

## Done when

- CRM/HR entities appear in global search where safe.
- Major CRM/HR actions appear in Activity.
- Resources/Validation/Test Lab can reference responsible parties.
- Automation workspace can show CRM/HR reminder jobs or equivalent status.
