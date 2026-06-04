# Structured Input

## Objectives

- Refresh the default multi-team software delivery process for .NET delivery.
- Keep .NET and JavaScript delivery concerns separable; implement only the .NET side now.
- Add app-type recognition for backend-only and Blazor UI application modes.
- Add a dedicated architecture design and review subprocess.
- Add a subprocess that writes .NET runtime command nodes to project structure.
- Add a subprocess that stores UI screenshots in project structure.
- Harden step permissions so non-implementation roles cannot mutate product code.
- Do not execute the delivery process during this implementation.

## Hard Constraints

- Template data and process tests are the source of truth; do not encode .NET-specific branching in process runtime code.
- Product mutation belongs only to implementation or repair steps.
- Project-structure writeback is an external action and must require `ExecuteExternalAction`.
- Screenshot and runtime command writeback must target process-run child nodes, not random delivery blocks.
- The app must be left running for user-led process testing after implementation.

## Expected Validation

- Bundle prepared-stage validator passes.
- Targeted process template tests prove operation contracts and new subprocess wiring.
- Build or targeted test command runs cleanly, or the exact blocker is recorded.
- No live delivery process is started.
