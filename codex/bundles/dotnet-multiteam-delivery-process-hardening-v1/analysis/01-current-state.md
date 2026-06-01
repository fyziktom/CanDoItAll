# Current State

## Relevant Process Templates

- `repo://Templates/Processes/processes/software-delivery/definition.json` is the default multi-team delivery process. It currently has one architecture review step and a direct implementation work step.
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json` is a .NET child process with nested setup and feature implementation subprocesses.
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json` creates or validates solution/test scaffolding.
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json` already keeps architecture, QA contract, implementation, validation, and handoff separated for a bounded feature.
- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json` has stronger Blazor delivery instructions and explicit non-mutating validation/writeback behavior.
- `repo://Templates/Processes/processes/app-pages-screenshot-set/definition.json` captures multi-page screenshot sets but writes to a delivery node rather than the requested process-run `Screenshots` node.

## Relevant Agent Templates

- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-solution-architect/settings.json` already has read-only project-structure/process access and an `ArchitectureReview` workspace profile.
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-qa-review-lead/instructions.md` already says QA must not mutate product files unless explicitly assigned repair work.
- `repo://Templates/Agents/teams/visual-automation-templates/members/screenshot-review-storage-agent/settings.json` has project-structure write permission for screenshot asset storage.
- `repo://Templates/Agents/teams/delivery-platform/members/delivery-manager/instructions.md` already covers project-structure writeback for result-recording steps.

## Gaps

- `software-delivery` is too generic for the requested .NET-only path and does not model app-type recognition.
- `software-delivery` implementation is a direct work step rather than a .NET subprocess.
- Architecture design and architecture review are collapsed into one step.
- Runtime command project-structure writeback is not modeled.
- UI screenshot project-structure writeback is not targeted to a `Screenshots` parent under the process run node.
- `dotnet-development-slice` has a QA step with `MutateProductTarget`, which undermines role separation because feature implementation already owns tests.

## Source Assertions Needed During Execution

- Assert `software-delivery` has a .NET contract classification artifact and subprocess-backed implementation.
- Assert all architecture/classification/review/QA/writeback steps are non-mutating.
- Assert runtime command writeback text names `Run command`, `Run app`, and `Run tests`.
- Assert screenshot writeback text names `Screenshots` under the process run node.
