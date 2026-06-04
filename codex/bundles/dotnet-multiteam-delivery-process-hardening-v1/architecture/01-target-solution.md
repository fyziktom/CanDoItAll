# Target Solution

## Template Shape

- Keep `software-delivery` as the user-facing multi-team process key, but tune it for .NET delivery.
- Add `dotnet-architecture-design-review` as a child process with:
  - contract/design intake,
  - architecture design,
  - architecture review,
  - design handoff.
- Add `dotnet-runtime-command-writeback` as a child process that creates or reuses a process-run `Run command` parent and writes at least:
  - `Run app`,
  - `Run tests`.
- Add `dotnet-ui-screenshot-writeback` as a child process that:
  - records non-UI applicability for backend/library/worker/API-only work,
  - captures and reviews UI screenshots when UI routes exist,
  - creates or reuses a process-run `Screenshots` parent,
  - stores accepted screenshot image assets under that parent.

## Permission Model

- Classification, architecture, peer review, QA, screenshot capture/review, runtime command writeback, release, and post-release steps are read-only or external-action controlled.
- Product mutation remains limited to implementation or repair subprocesses and implementation/repair child steps.
- Project-structure writeback uses `ExternalActionControlled` plus `ExecuteExternalAction`.

## Process Flow

1. Resolve .NET delivery contract and app type.
2. Run the .NET architecture design/review subprocess.
3. Run the .NET implementation slice subprocess.
4. Run peer review and QA validation.
5. If QA accepts, write runtime command nodes.
6. If QA accepts, run UI screenshot writeback; backend-only apps complete this subprocess with an explicit no-UI applicability artifact.
7. Continue security, release approval, rollout, and learning gates.

## Out Of Scope

- JavaScript process creation.
- Runtime-code changes to branch resolution, project-structure projection, or tool policy.
- Running a live software-delivery process as part of this implementation.
