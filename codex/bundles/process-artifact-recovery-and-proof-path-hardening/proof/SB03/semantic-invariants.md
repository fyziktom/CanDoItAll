# SB03 Semantic Invariants

- Invariant ID: `SB03-I001`
- Source raw note: User required reusable Blazor delivery, repair/fix, backend feature, frontend feature, and backend+frontend feature processes.
- Expected behavior: Blazor process templates load and project with required contracts for delivery, repair, and feature-addition runs.
- Disallowed shallow implementation: Adding a one-off process definition that only works for the demo app.
- Failing-first test: N/A process-template pack expansion; the proof is a non-production template contract addition validated by projection tests.
- Passing test: `bundle://proof/SB03/transcripts/template-tests.txt`.
- Changed source files: `repo://Templates/Processes/manifest.json`, `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`, `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`.
- Production assertions: Template import/projector sees reusable Blazor definitions with browser proof, repair, revalidation, and writeback steps.
- Red-team negative case: `bundle://proof/SB03/transcripts/anti-stub-audit.txt` verifies no demo-app-specific template or test reference was introduced.
- Downstream dependency check: SB04-SB07 live execution uses these templates without Blazor-specific runtime code.

- Invariant ID: `SB03-I002`
- Source raw note: Process core/code must remain generic while Blazor-specific detail lives in process steps, agents, tools, and project-structure records.
- Expected behavior: Blazor runtime validation is process-owned through template steps and artifacts, not hard-coded in process runtime.
- Disallowed shallow implementation: Adding process runtime branches for Tetris, Blazor, or a specific app mode.
- Failing-first test: N/A process-template behavior; no production runtime behavior was added for this invariant.
- Passing test: `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Changed source files: `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`, `repo://Templates/Processes/processes/blazor-app-repair-fix/definition.json`, `repo://Templates/Processes/processes/blazor-fullstack-feature/definition.json`.
- Production assertions: Template steps require dotnet validation, Playwright/browser proof, screenshot paths, console messages, URL/entrypoint, cleanup, and writeback references.
- Red-team negative case: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`.
- Downstream dependency check: SB07 acceptance relies on process-recorded evidence produced by agents.

- Invariant ID: `SB03-I003`
- Source raw note: Final app testing must capture screenshot evidence and reveal browser/runtime failures.
- Expected behavior: Visible Blazor UI surfaces require browser proof with screenshots, browser state/evaluate output, console messages, URL or entrypoint, and cleanup.
- Disallowed shallow implementation: Accepting chat-only screenshots, detached paths, stale images, or stdout logs as browser proof.
- Failing-first test: N/A process-template contract; the negative case is encoded in template tests as unacceptable screenshot evidence.
- Passing test: `bundle://proof/SB03/transcripts/template-tests.txt`.
- Changed source files: `repo://Templates/Processes/processes/blazor-app-delivery/steps/validate-blazor-runtime.md`, `repo://Templates/Processes/processes/blazor-app-delivery/steps/revalidate-blazor-repair.md`.
- Production assertions: Validation artifacts explicitly name Playwright screenshots and browser console messages.
- Red-team negative case: Template test asserts missing, blank, detached, stale, or chat-only screenshots are not acceptable.
- Downstream dependency check: SB07 final validation reads the screenshot and console evidence produced by this contract.

- Invariant ID: `SB03-I004`
- Source raw note: Results, screenshots, and summaries must be added back to project structure.
- Expected behavior: Results are written back to project structure with a compact run evidence index and self-review summary.
- Disallowed shallow implementation: Leaving evidence only in chat or local workspace files without project-structure references.
- Failing-first test: N/A process-template contract; writeback is enforced by process artifacts and final record steps.
- Passing test: `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Changed source files: `repo://Templates/Processes/processes/blazor-app-delivery/steps/record-blazor-results.md`, `repo://Templates/Processes/processes/blazor-app-delivery/steps/record-blazor-results-after-repair.md`.
- Production assertions: Final record steps require project-structure evidence writeback references.
- Red-team negative case: A final record artifact without writeback references fails the template contract.
- Downstream dependency check: SB07 verifies the validation node and screenshot asset node exist after the live run.

- Invariant ID: `SB03-I005`
- Source raw note: Tetris is only the demo; the reusable process must remain generic for Blazor SSR, WASM, and WASM PWA.
- Expected behavior: Templates remain generic and do not mention the demo app.
- Disallowed shallow implementation: Encoding Tetris, canvas, or game-specific behavior in process definitions or runtime.
- Failing-first test: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`.
- Passing test: `bundle://proof/SB03/transcripts/template-tests.txt`.
- Changed source files: `repo://Templates/Processes/processes/blazor-backend-feature/definition.json`, `repo://Templates/Processes/processes/blazor-frontend-feature/definition.json`, `repo://Templates/Processes/processes/blazor-fullstack-feature/definition.json`.
- Production assertions: Template wording targets Blazor SSR, WASM, and WASM PWA apps generically.
- Red-team negative case: `bundle://proof/SB03/transcripts/anti-stub-audit.txt` exits non-zero for demo-specific matches; captured result found none.
- Downstream dependency check: SB05 imports the reusable template set and SB06 launches from the Blazor app delivery definition.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Blazor process definition key | `repo://Templates/Processes/manifest.json` | Template pack loader and API/template import users | Makes each reusable Blazor process selectable and importable | `bundle://proof/SB03/transcripts/template-tests.txt` |
| Blazor delivery contract artifact | First process step | Architect, implementer, QA, and recorder steps | Carries project id, node ids, Blazor mode, output root, run folder, acceptance criteria, routes/API surfaces, exclusions, and writeback targets | `bundle://proof/SB03/transcripts/source-assertions.txt` |
| Blazor runtime evidence pack | QA validation step | Repair branch, release/record step, and project-structure writeback | Blocks closure unless build/test/runtime/browser proof and screenshots are process-visible | `bundle://proof/SB03/transcripts/template-tests.txt` |
| Run evidence index | Final record step | User, Codex observer, and later selective summarization | Keeps large process runs reviewable without reading every raw artifact | `bundle://proof/SB03/transcripts/source-assertions.txt` |
