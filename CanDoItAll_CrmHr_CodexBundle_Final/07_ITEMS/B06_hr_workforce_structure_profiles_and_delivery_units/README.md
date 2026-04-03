# B06 — HR workforce structure, worker profiles, and delivery units

## Purpose

Add workforce profiles for employees, contractors, freelancers, and delivery units, including reporting lines, home units, lifecycle dates, rates, seniority, and structure-aware views.

## Dependencies

B01, B02, B03

## Main stories covered

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

## Main routes

- `/crm-hr/workforce`

## Done when

- A person can have a workforce profile without losing CRM identity continuity.
- A delivery unit can be represented as a party with workforce semantics.
- Workforce detail shows home unit and manager relationships clearly.
- Component and Playwright tests prove profile editing.
