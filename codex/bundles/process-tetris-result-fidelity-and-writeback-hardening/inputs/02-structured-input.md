# Structured Input

## Core Objective

- Make the Tetris delivery process rerunnable and trustworthy after the observed failed run: process writeback completes, contract drift is rejected, and the app is proven playable/static/no-backend.

## Success Criteria

- Final project-structure writeback either succeeds or records a valid failed tool receipt that enters an explicit recovery/escalation path.
- Implementation and validation preserve the upstream static/WASM/no-backend contract and selected root.
- Browser proof demonstrates actual gameplay interactivity and local high-score persistence.
- A final rerun reaches terminal success through APIs and writes the final evidence/verdict node under `Main app`.

## Hard Constraints

- The request says `static website` and `no backend`; a Blazor Web App server host is not acceptable unless project structure explicitly changes the requirement.
- The contract selected `WASM`; downstream executors must not silently switch to `Microsoft.NET.Sdk.Web` / Interactive Server.
- The run folder selected by the contract is authoritative; creating `MainApp` beside the contracted `main-app` root is a failure unless the contract is updated.
- A step that claims a required project-structure tool failed must have a failed tool receipt or an explicit platform/tool-error record that the runtime can evaluate.
- Browser proof must fail if the page stays `Loading`, if no .NET interactivity/hydration path is active, or if keyboard/localStorage behavior is not proven.

## Allowed Side Effects

- Process runtime, prompt, policy, seed-instruction, and focused test changes required by the subbundles.
- Fresh process run artifacts and project-structure nodes/assets created during final validation.
- No unrelated UI or architecture refactors.

## Source Artifacts

- `bundle://evidence/run-0cca729a-detail.json`
- `bundle://evidence/01-blazor-delivery-contract.md`
- `bundle://evidence/03-blazor-runtime-evidence-pack.md`
- `bundle://evidence/06-project-structure-result-writeback-summary.md`
- `bundle://evidence/tetris-rerun-independent-snapshot.md`
- `bundle://evidence/tetris-rerun-independent-console.txt`
- `bundle://evidence/tetris-rerun-independent.png`

## Input Coverage Signals

- The words `must`, `static website`, `keyboard controls`, `save highest score locally`, and `no backend` are literal requirements.
- The already-fixed HR launch blocker is a prerequisite, not the closure target of this bundle.
- The previous missing-artifact manager-recovery hardening is related but insufficient; this run moved past missing artifacts and failed on writeback/tool-receipt and app-quality issues.

## Dependency And Sequencing Signals

- SB04 cannot start before SB01-SB03 because the final rerun would otherwise be untrustworthy.
- SB03 depends on SB02 because browser proof must validate the right kind of app, not a server-hosted substitute.

## Validation Expectations

- Focused `dotnet test` coverage for governed writeback behavior.
- Prompt/policy/source assertion proof for contract fidelity.
- Playwright proof for gameplay semantics and localStorage persistence.
- API proof for final process closure.

## Evidence Contract

- Test transcripts for every changed runtime/prompt/policy area.
- Browser artifacts with route, viewport, screenshot, snapshot, console, keyboard actions, assertions, and result.
- API snapshots before and after final rerun.
- Project-structure read proof showing final writeback node.

## UI Validation Strategy

- Use a large desktop viewport first, then a narrower viewport if the final app has responsive layout differences.
- Review screenshot for readable board, controls, score/high score, and no clipped or overlapping content.
- Treat interactive assertions as mandatory; visual proof alone is not enough.

## Browser Validation Analytics

- Record route, viewport, Playwright MCP actions, assertions, screenshot paths, console paths, and pass/fail row in `reviews/01-execution-report.md`.

## Working Assumptions

- The app remains available on `http://localhost:5032` for API-driven process operations.
- The final deliverable can be Blazor WASM or plain static HTML/JS if the process permits it.

## Primary Risks

- Prompt-only fixes will not prevent future contract drift.
- Console-clean/browser-rendered checks will continue passing broken interactivity unless semantic assertions are required.
- Weak writeback recovery could fabricate evidence nodes without actually registering required assets.
