# Structured Input

## Objectives

- Continue the dispatcher isolation work from the previous bundle.
- Split the claimed route execution body into module-local route handlers.
- Preserve current runtime behavior and exact route stage order.
- Keep Process Core and production process driver APIs out of scope.
- Record proof for every subbundle row individually.

## Constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not add production driver APIs such as `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, or `IProcessHelperDriver`.
- Do not edit UI, Razor, CSS, JavaScript, TypeScript, image, screenshot, browser, mobile, or proof-screenshot files.
- Do not hide EF writes, transition calls, service-scope calls, finalizer calls, or external agent execution inside classes named `Rules`.

## Required Proof

- Source scans for no Process Core, no production driver API, no UI proof drift, route order, and anti-stub markers.
- Focused unit and integration tests named by `bundle://traceability/02-proof-requirements.md`.
- Full solution build at final closure.
- Critical proof manifests and semantic invariants for every critical gate.
