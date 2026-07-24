# Follow-up Request

Date: 2026-07-24

## Literal Request

- Improve the CRM-HR UI further.
- Directory and Workforce must use a list/card catalogue like the Agents tab, including paging and proper scrolling for longer result sets.
- Details and editing must open in dialogs rather than permanently occupying the page.
- Re-check `feedback10.docx`, especially the requirement that the reusable browser also serves ordinary searchable lists.
- Add realistic test data that demonstrates hiring and more complex workforce situations.
- Feed that data through CRM-HR HTTP APIs, not through application startup seed code or direct database writes.
- Complete the missing CRM-HR API commands needed for those scenarios and add a reusable CRM-HR API Codex skill.
- Prefix CRM-HR subpage workbench-tab titles with `CRM` so labels such as `Directory` and `Workforce` remain clear outside the module context.
- Update all affected documentation.
- Run proper validation, then rebuild and restart the instance on port `5032`.

## Added-Scope Classification

- The list/dialog and contextual-title corrections reopen the prior `N005`/`R007`, `N010`/`R012`, UI, and closure claims.
- Public CRM-HR HTTP APIs, a reusable API skill, persistent test/demo data, and their documentation are new scope introduced by this follow-up; they are not hidden requirements from `feedback10.docx`.
- Seed data must use normal production command paths through HTTP. It must not fabricate unavailable invoice/purchase financial facts.

