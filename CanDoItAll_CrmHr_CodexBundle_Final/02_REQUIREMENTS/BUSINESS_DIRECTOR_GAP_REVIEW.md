# Senior Business Director gap review

| Enterprise capability | Director verdict | Bundle coverage | Notes |
| --- | --- | --- | --- |
| Shared directory for companies, people, units, and agents | Required | B01-B03, B09 | Critical foundation; without this the rest is fragmented. |
| Customer and partner account handling | Required | B04-B05 | Normal enterprise CRM baseline. |
| Opportunity pipeline and conversion to delivery work | Required | B05, B10 | Needed because CanDoItAll is project-delivery centric. |
| Employee, contractor, and delivery-unit profiles | Required | B06 | Must support company-as-delivery-unit, not only named employees. |
| Capacity, staffing, and bench management | Required | B07, B10 | Essential in delivery businesses. |
| Recruitment, interviews, onboarding, offboarding | Required | B08 | Needed for a serious HR surface. |
| AI agents as operational actors | Required for this product | B09, B10 | This is product-specific but strategically important. |
| Deep project integration | Required | B10 | Without project integration the module misses the user's core examples. |
| Ownership in resources, tests, validation, activity | Required | B11 | Makes the module native to CanDoItAll rather than a silo. |
| Privacy, audit, and safe lifecycle controls | Required | B12 | Needed for real HR data. |
| Full payroll, tax, and benefits administration | Explicitly out of scope | Not planned | Would turn CanDoItAll into an ERP/HCM suite; not needed for current product scope. |
| Marketing automation and campaign orchestration | Out of scope | Not planned | CRM here is relationship and pipeline focused, not marketing-suite focused. |
| Full document management / e-signature workflow | Out of scope for v1 | Indirect via resources | Links and resources are enough for now. |
| Advanced RBAC / SSO / approval matrix | Future-ready seam only | B12 | Current repo uses a local-user model; bundle adds seams but not a full auth rewrite. |


## Additional gaps discovered and added to scope

The original request focused strongly on Projects and Workbench assignment. Reviewing the repository from a senior business-director angle shows that a normal enterprise implementation also needs these capabilities, and they were therefore added explicitly:

- duplicate merge and safe import/export,
- capacity and staffing views,
- candidate lifecycle and onboarding/offboarding,
- account interaction journal and next actions,
- opportunity conversion into project context,
- AI-agent governance and ownership,
- privacy, audit, and archive controls.

## Verdict

For a **project-delivery enterprise platform**, this bundle covers the important CRM/HR capability set that business leadership would expect to exist inside CanDoItAll.

The design is intentionally **not** a payroll suite and **not** a marketing suite. That is a deliberate product boundary, not a missing requirement.
