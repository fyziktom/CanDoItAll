# Proof Manifest — SB12

- Status: `Completed with classified validation debt`.
- Proof tier: `Governed`.
- Owned requirements: `SCUI-005`, `SCUI-017`, `SCUI-027`, `SCUI-029`, `SCUI-031`, `SCUI-043`, `SCUI-048`, `SCUI-056`, `SCUI-057`, `SCUI-058`, `SCUI-059`, `SCUI-060`, `SCUI-061`, `SCUI-062`, `SCUI-063`, `SCUI-064`.
- Start commit: `2d6dac63a6350a3bdd538c34d11e68ce364a74d4`.
- Candidate identity: working tree based on the start commit; commit skipped per user instruction because repository signing was unavailable.
- Candidate fingerprint: 56 changed source/test/solution files, SHA-256 `e34521b776be20599e8d6c223b5f4d77e11398ec45bc3d7e6669443ea6994043`.
- SharedInfo commit: `7b7808e8591d7219f40826cf0e5624e182981d90`.
- Architecture snapshot: `snap-20260817145010-016beac4`.
- Semantic contract: `bundle://proof/SB12/semantic-invariants.md`.
- Architecture decision: `bundle://proof/SB12/architecture-gate.md`.
- Execution report: `bundle://proof/SB12/execution-report.md`.

## Browser artifacts

- Main Simple Chat exact reply: `bundle://proof/SB12/screenshots/main-simple-chat-completed.png`.
- Floating Simple Chat exact reply: `bundle://proof/SB12/screenshots/floating-simple-chat-completed.png`.
- Floating Agent exact reply before lifecycle test: `bundle://proof/SB12/screenshots/floating-agent-chat-completed.png`.
- Floating Agent after keep-active/reopen: `bundle://proof/SB12/screenshots/floating-agent-chat-reopened.png`.

## Validation matrix

- Pre-Stable focused Components/Unit selectors: pass (4 + 6 + 38 + 4 tests).
- Stable solution: ran exactly once; 959 passed and 73 failed in the Components project before the run was dominated by missing neutral-shell registration in feature-module hosts. The failure is retained as evidence.
- Final DI repair: both AgentFramework and LlmChats.Ui register `AddConversationShell()`; 2 registration Unit tests pass.
- Post-repair Components selector: 13 passed, 0 failed.
- Post-repair Integration selector: 3 passed, 0 failed.
- Final Web build: pass, 0 warnings, 0 errors.
- Full Playwright solution: ran exactly once; failed in unrelated existing lanes and was cancelled after it stopped producing output. It was not rerun.
- Named live Playwright MCP proof: main Simple Chat, floating Simple Chat, and floating Agent chat pass; 0 browser console errors at the final scenario checkpoint before intentional runtime shutdown.
- Final architecture, sensitive-content scan, `git diff --check`, SharedInfo identity, and bundle artifact checks: pass.

## Progression

Implementation is complete and the required handoff state is `awaiting-user-simple-chat-ui-verification`. `FINAL Pass` is not claimed because the two authorized broad runs are non-green. The manifest omits its own self-referential digest; the bundle validator checks its integrity.
