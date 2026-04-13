# Expected proof contract

## Definition of sufficient proof

A phase is only sufficiently proved when:
- its targeted tests passed,
- its build impact is checked,
- its UI changes have browser proof if applicable,
- the subbundle gate decision is recorded,
- the evidence is written into the execution report.

## Proof categories

### Structural proof
- validator pass,
- architecture memo,
- updated traceability.

### Behavioral proof
- focused integration and component tests,
- conflict tests where relevant,
- no-op/identity stability tests where relevant.

### UI proof
- browser route and actions,
- large-screen screenshot,
- narrower-width screenshot,
- explicit screenshot review notes.

### Closure proof
- completed-stage validator,
- final execution report,
- final raw-note closure table.

## Proof anti-patterns

Not sufficient:
- “the code looks correct”,
- “tests should pass”,
- “manual check done” with no record,
- screenshots captured but not reviewed,
- final closure with incomplete gate rows.
