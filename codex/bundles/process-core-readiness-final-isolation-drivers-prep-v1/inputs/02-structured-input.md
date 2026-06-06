# Structured Input

## Objectives

- Validate the latest `maf-processes-refactor` state before more extraction work.
- Burn down remaining dispatcher adapters and static wrappers through module-local services.
- Preserve current process dispatch behavior and route order.
- Keep Process Core and production driver APIs out of scope for this bundle.
- Produce a final Core/driver readiness decision and next-bundle cutline.

## Hard Constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce production process-driver APIs.
- Do not remove existing dispatch behavior.
- Do not touch UI unless a compile fix unexpectedly requires it, and stop/document first if that happens.
- Do not create mobile, small, or medium viewport proof artifacts.
