# Screenshot validation protocol

## Goal

Prevent “tests passed but the page is visually wrong” outcomes.

## Minimum evidence package per UI bundle

1. screenshot files
2. short semantic review markdown
3. route and scenario label
4. timestamp or execution context
5. pass/fail statement against the bundle acceptance criteria

## Required screenshot categories

### Layout proof

Shows page shell, header, tabs, filters, and primary call-to-action.

### Data proof

Shows real entities created or edited during the scenario.

### State-change proof

Shows something after save, archive, convert, assign, merge, or other key mutation.

### Persistence proof

Shows the page after reload or navigation return.

### Cross-module proof

Shows the related CanDoItAll surface that now reflects the CRM/HR change.

## Semantic review template

Use a note like this for each screenshot set:

```text
Route: /crm-hr/directory
Scenario: create company + contact + delivery unit relationship
Visible proof:
- party appears in list with correct type and status
- detail pane shows roles, contact methods, and relationship summary
- no clipped labels or overlapping controls
- save persisted after reload
Verdict: pass
```

## Blocking defects

Any of these block acceptance:

- unreadable or clipped important text
- broken save action visibility
- hidden validation messages
- missing project/customer/assignment context where required
- incorrect privacy display of confidential notes
- incorrect route or selection after save
- duplicate entries caused by merge or conversion bugs
