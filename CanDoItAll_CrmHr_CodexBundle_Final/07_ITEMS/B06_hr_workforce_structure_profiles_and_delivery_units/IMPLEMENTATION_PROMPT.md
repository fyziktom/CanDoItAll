# Implementation prompt

Implement **B06 — HR workforce structure, worker profiles, and delivery units** for CanDoItAll.

## Bundle goal

Add workforce profiles for employees, contractors, freelancers, and delivery units, including reporting lines, home units, lifecycle dates, rates, seniority, and structure-aware views.

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

- Create workforce profiles for employees, contractors, freelancers, and delivery units.
- Store titles, discipline, seniority, rates, capacity baseline, home unit, and manager.
- Show org-structure context in the workforce route.

## Stories that must be satisfied in this bundle

- **HR-01** As a people ops manager, I can create an employee profile from the unified party model so one person can exist in HR, CRM, and projects at the same time.
- **HR-02** As a people ops manager, I can create contractor and freelancer profiles with separate employment metadata so external workforce handling is explicit.
- **HR-03** As a delivery director, I can create delivery units and internal teams as parties so staffing can use organizations as well as people.
- **HR-04** As an HR manager, I can assign manager relationships and home unit membership so org structure is maintained.
- **HR-05** As an HR manager, I can store start date, end date, employment type, and lifecycle state so workforce records reflect reality.
- **HR-06** As an HR manager, I can maintain job title, discipline, seniority, and location so staffing data is useful.
- **HR-16** As a people ops manager, I can mark a primary delivery unit or home team for a worker so reporting lines and staffing ownership are clear.
- **HR-17** As a finance partner, I can store internal cost rate and external billing-rate range for a worker or delivery unit so staffing economics are visible without becoming a payroll system.
- **HR-29** As a vendor manager, I can represent a subcontractor company and the individual subcontractor separately so commercial and operational relationships are not blurred.
- **HR-30** As a delivery director, I can reuse one person as employee, candidate, customer stakeholder, or partner contact when reality requires it so duplication does not grow.
- **HR-34** As an HR manager, I can reactivate former workers or contractors when they return so historical context is preserved.
- **HR-36** As a capability lead, I can group workers by discipline and capability area so capability health is reviewable.
- **DIR-16** As a portfolio manager, I can model a delivery unit as an organization or organization unit instead of an employee so the system supports company-based delivery.

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
