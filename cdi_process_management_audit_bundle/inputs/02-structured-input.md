# Structured Input

## Core Objective

- Repair the flat audit bundle into a validator-compliant initiative bundle.
- Reconcile the old audit against the live process-management module before implementing anything.
- Implement typed branching so a process can route to different next steps based on an explicit decision outcome and explicit decision-maker role ownership.
- Execute the repaired bundle fully and record real proof.

## Hard Constraints

- Use the bundle workflow and keep the bundle current while executing it.
- Do not silently narrow stale audit scope. Any narrowing must be explicit with a disposition and follow-up path.
- Keep the process definition canonical. Do not introduce alternate runtime or UI-only sources of truth.
- Use strong typing for branch semantics. Do not hide branching behind magic strings or free-text-only logic.
- Do not claim completion without real validator runs, build or test proof, and browser proof for UI changes.

## Source Artifacts

- The user request in `inputs/00-original-request.md`.
- The architect's flat audit pack listed in `inputs/01-source-artifacts.md`.
- The live process-management code and tests listed in `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- The bundle itself is structurally wrong and must be repaired before implementation starts.
- The legacy audit explicitly called out missing branch semantics and a sequence-driven runtime.
- The user explicitly called out branching, multi-outcome switch behavior, and decision-maker role input as missing functionality.
- The user explicitly required true validation and refused a paper-only completion.

## Dependency And Sequencing Signals

- Bundle repair and stale-audit reconciliation must happen before code changes, otherwise the execution scope is untrustworthy.
- The branch-capable definition model and publish validation are the critical foundation for runtime work.
- Runtime orchestration and MCP contracts depend on the definition model being correct.
- Workspace and canvas proof depend on the runtime contract being correct.
- Final closure depends on validator reruns, test proof, and browser analytics being already recorded during earlier subbundles.

## Validation Expectations

- Run the prepared-stage bundle validator after repairing the bundle.
- Run targeted .NET tests for definition, runtime, MCP, and UI changes.
- Run at least one broader confirmation build or test command before closure.
- Run the completed-stage bundle validator only after all subbundle statuses, execution-report rows, and raw-note closure rows are synchronized.

## UI Validation Strategy

- Run a headed Playwright session against the process workspace on a large-screen viewport first.
- Validate authoring of branch outcomes and dependency outcome routing in the definition workspace.
- Validate runtime branch selection and resulting path activation in the run workspace.
- Capture and review desktop-width screenshots, then repeat the relevant flow at a narrower width if layout changes are responsive.

## Browser Validation Analytics

- Record route, viewport, Playwright actions, screenshots, and result for the authoring flow and runtime flow in `reviews/01-execution-report.md`.
- Treat missing Playwright interaction or missing screenshot review as a failed closure gate for the UI subbundle.

## Working Assumptions

- The live unresolved gap that must be reopened from the legacy audit is branching, not the entire original 15-task backlog.
- This pass can keep the current one-predecessor-per-step shape as long as it still supports flexible switch-style routing from one step to one or more next steps.
- Decision-maker role ownership can be modeled as a typed role reference on the branching source step rather than a free-text runtime note.

## Primary Risks

- A weak branching model could still leave runtime behavior sequence-driven under the hood.
- Pending non-selected branch steps could leave runs unable to complete unless skip behavior is explicit.
- UI proof could look plausible while the MCP or runtime contract still cannot express branch selection.
- The stale audit could pressure the work toward a fake "implement everything" posture unless the live-gap reconciliation stays explicit.
