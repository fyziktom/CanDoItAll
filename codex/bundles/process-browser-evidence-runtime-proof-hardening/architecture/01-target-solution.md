# Target Solution

## Boundaries

- Process core owns generic proof lifecycle: what evidence categories are required, where artifacts live, how they are validated, and how missing or invalid evidence affects step outcomes.
- Project structure owns domain acceptance hints: for TetrisGame, examples include active game elements visible after representative input and no JavaScript errors during active play proof. These examples must not become hardcoded process-runtime rules.
- Process definitions and step evidence contracts own generic step-level requirements: UI QA steps must capture screenshots, console logs, DOM/snapshot or evaluate output, and representative interaction assertions.
- Agent skills and instructions own tool choice and execution behavior: Playwright MCP, browser_evaluate, launch commands, screenshot review questions, and how to cite artifacts.
- AgentFramework provider-native MCP code owns tool output discovery, durable receipt/enrichment, and safe mirroring into scoped workspace artifacts.

## Proposed Shape

1. Introduce or reuse a typed browser/runtime proof requirement model for process dispatch.
   - Required categories: screenshot, DOM/snapshot or evaluate output, console diagnostics, route/URL, representative interaction, cleanup or stop marker.
   - The implementation may derive this from existing `DispatchArtifactExpectation` and step text, but execution should avoid stringly typed ad hoc checks where a small typed model clarifies behavior.
2. Make provider-native browser outputs durable.
   - Prefer actual tool receipts/results when available.
   - Add a production fallback that reads durable execution logs or provider-native MCP output manifests when chat history is empty.
   - Copy or mirror `.playwright-mcp` files into the scoped process artifact root and create `Processes_ArtifactRecords` linked to the expected artifact or proof category.
   - Record conformance observations when evidence references exist but cannot be imported.
3. Harden proof validation.
   - Required browser artifact paths must exist, be non-empty, and have expected content type or parseability.
   - Screenshots must be valid images and large enough to represent the viewport.
   - Console diagnostics must be tied to an active validation window. Active errors block acceptance; intentional post-stop disconnects are separate warnings or notes.
   - Interactive proof must include a representative assertion from project structure or step contract.
4. Update generic process definitions and agent instructions.
   - Multi-team UI QA steps should declare exact managed artifact paths or typed requirements for screenshot, console, snapshot/DOM, and interaction summary.
   - Agent prompts must say that citing `.playwright-mcp` raw paths is not enough unless the process imports them as managed artifacts.
   - Agent prompts must require screenshot review questions, not just screenshot capture.
5. Add regression proof.
   - Failing-first tests reproduce the DB shape.
   - Passing tests exercise production projection/validation paths, not manually seeded rows.
   - Final demo proof uses a clean development DB and a fresh process run.

## Production Behavior Artifact Matrix

| Production artifact or signal | Producer | Consumer | Lifecycle | Negative proof required |
| --- | --- | --- | --- | --- |
| Browser proof artifact record | Process artifact projection from execution detail and provider-native MCP outputs | Process step validation, release readiness, UI evidence views | Created after browser MCP execution and before QA/release acceptance | Missing screenshot expectation with only markdown mention must fail or repair |
| Browser proof conformance observation | Process validation when required evidence is missing, detached, invalid, or shallow | Process UI, step outcome diagnostics, operator review | Created during dispatch validation or transition before final step acceptance | Completed QA with no image artifact must produce observation |
| Console diagnostic phase classification | Runtime proof validator from browser console log and stop/cleanup markers | QA step outcome, evidence pack validation, release readiness | Captured during active proof and finalized after cleanup | Post-stop disconnects cannot be called active proof errors, active JS errors cannot be ignored |
| Representative interaction assertion | QA agent/browser proof and project structure acceptance hints | Proof validator and evidence report | Captured after navigation and before screenshot/console finalization | Page load or pause-only proof must not satisfy game/canvas/custom-control acceptance when project hints require visible interactive state |

## Validation Strategy

- Unit tests for path extraction, provider-native output matching, console phase classification, and proof requirement derivation.
- Integration tests for artifact projection from provider-native browser outputs when chat history is empty but execution logs and `.playwright-mcp` outputs exist.
- Integration tests that prove process transition rejects or repairs the original DB failure shape.
- Prompt tests that assert generic instructions require exact artifact paths, screenshot review, console diagnostics, and representative interaction proof without hardcoding Tetris.
- Final manual/live validation with a clean development DB and a fresh process run, including Playwright MCP screenshot and console artifacts visible in process artifact records.
