# Verification plan — PRM-F17

## Expected verification outcomes

- A process definition cannot be published without a process owner, primary customer, criticality tier, and value statement.
- Process definitions can declare sponsor, stewarding managers, strategic objective links, and upstream/downstream interface contracts.
- Interface contracts capture sender, receiver, required inputs/outputs, definition of done, and handoff expectation metadata.
- Actor assignments remain separate from org hierarchy; the model does not force the process graph to mirror reporting lines.
- Shared-project or shared-library processes preserve explicit ownership instead of being duplicated as shadow copies.

## Automated tests

- Unit tests for new invariants and validation rules
- Integration tests for persistence and cross-module seams
- Component tests for editor or viewer surfaces where applicable
- Playwright coverage for the main happy path if new end-user flow is introduced

## Manual verification checklist

1. Create a draft process and confirm owner/customer/criticality/interface metadata can be entered.
2. Try publishing without owner or customer and verify validation blocks it.
3. Publish with complete metadata and confirm snapshots remain visible.

## Regression concerns to watch

- Org chart reintroduced as the process graph
- Critical processes published without real owner/customer