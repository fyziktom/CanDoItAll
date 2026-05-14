# QA Prompt

```text
Validate the workflow-basic-routing-maf implementation.
Confirm that the compiler uses MAF routing primitives for executable routes and does not treat ConditionExpression as a predicate.
Run targeted unit, component, and integration tests listed in the relevant subbundle README.
For UI phases, use the workflow canvas in a maximized browser first and then a narrower viewport. Capture screenshots and review for clipped controls, ambiguous branch/default labels, hidden validation errors, and save/load loss.
Fail the closure if invalid routes silently become direct edges or if unsupported artl-v1 routes execute.
```
