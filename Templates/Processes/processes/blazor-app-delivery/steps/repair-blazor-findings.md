# Repair Blazor validation findings

Repair only findings produced by the validation step. Do not expand scope beyond project-structure acceptance criteria. If validation found scaffold-only delivery or deferred acceptance criteria, implement the missing requested product behavior in the approved product root instead of adding more evidence around the incomplete scaffold. Rerun the smallest relevant dotnet build/test checks needed to prove the repair before handoff. When runtime startup proof is invalidated by the repair, start the app once with `workspace_dotnet_run` HTTP startup proof and record the startup receipt; leave browser screenshots and console proof to the validation step. Do not mark complete while required behavior, routes, persistence, PWA/static-hosting assets, or tests remain deferred to next actions.

## Evidence

Record commands, files, URLs, screenshots, console messages, errors, assumptions, and project-structure writeback references as applicable.

