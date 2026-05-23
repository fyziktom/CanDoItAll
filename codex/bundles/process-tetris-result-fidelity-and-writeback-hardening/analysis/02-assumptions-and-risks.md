# Assumptions And Risks

## Assumptions

- API-driven launch/provision/execute remains available on the local development app at `http://localhost:5032`.
- The process definition can be hardened in repo code, prompts, policies, or seeded instructions without manually editing the user's project graph.
- A correct final deliverable can be produced as Blazor WASM or plain static HTML/JS; the key requirement is static-hostable browser execution with local high-score persistence and no server API/backend dependency.

## Risks

- If `project_structure_asset_create` swallows detailed exceptions and only reports `Function failed`, agents cannot recover intelligently. The fix must expose masked but actionable error codes without leaking sensitive paths or payload content.
- If the process only prompts agents to honor contracts, future runs can drift again. Contract fidelity needs an executable validation rule or focused tests.
- If browser proof only verifies static DOM counts, non-interactive Blazor output will continue passing.
- If the final rerun is started before SB01-SB03 are proven, it may waste tokens/runtime and produce another untrustworthy artifact set.

## Critical Path Risks

- SB01 is a critical foundation: final writeback cannot complete until required project-structure tool failures have durable receipts or recoverable diagnostics.
- SB02 is a critical foundation: validation of the final app is meaningless if the process can switch from the contracted static/WASM root to a server-hosted shadow app.
- SB03 is a critical foundation: build/test/console proof is not enough for game delivery; the delivered app must be interacted with through the browser.
- SB04 depends on SB01-SB03; do not rerun the whole process as the closure proof until all three gates pass.

## Validation Risks

- A test that only asserts `project_structure_node_create` appears in a prompt is weak. It must exercise the no-receipt and failed-receipt branches separately.
- A test that only asserts `WASM` appears in the contract is weak. It must reject a `Microsoft.NET.Sdk.Web` / server-host output when the contract says static/WASM/no backend.
- A Playwright check that clicks `New game` but does not observe state change is weak. It must assert post-interaction status, score/board movement or equivalent gameplay state, and localStorage behavior.

## Reopen Triggers

- Any final run that fails with an open escalation, missing required artifact, dead-lettered outbox, or invalid blocked outcome reopens SB01.
- Any final run that creates output outside the contracted root or changes template/mode without updating the contract reopens SB02.
- Any final browser proof where status remains `Loading`, keyboard input has no observable effect, high score is not persisted locally, or the console has runtime errors reopens SB03.
- Any final project-structure graph without a final verdict/evidence index node under the target `Main app` node reopens SB04.
