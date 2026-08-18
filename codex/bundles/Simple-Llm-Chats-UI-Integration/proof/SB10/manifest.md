# Proof Manifest — SB10

- Status: `Completed`.
- Proof tier: `Governed`.
- Owned requirements: `SCUI-028`, `SCUI-029`, `SCUI-030`, `SCUI-031`, `SCUI-032`, `SCUI-039`, `SCUI-040`, `SCUI-041`, `SCUI-042`, `SCUI-044`, `SCUI-046`, `SCUI-047`, `SCUI-048`, `SCUI-058`, `SCUI-062`.
- Start commit: `2d6dac63a6350a3bdd538c34d11e68ce364a74d4`.
- Candidate identity: working-tree candidate based on `2d6dac63a6350a3bdd538c34d11e68ce364a74d4`; commit skipped because repository signing remained unavailable and the user explicitly authorized continuing without bundle commits.
- SharedInfo commit/hash: `7b7808e8591d7219f40826cf0e5624e182981d90`.
- Semantic contract: `bundle://proof/SB10/semantic-invariants.md`.
- Architecture decision: `bundle://proof/SB10/architecture-gate.md`.
- Execution report: `bundle://proof/SB10/execution-report.md`.

## Scope

SB10 activates the `/chats` page and `Simple Chats` shell navigation only after the route-inactive workspace passed. It also closes three defects found by the required real-browser scenarios: repeated definition-tag replacement in one EF context, event-following scope isolation, and safe draining of an in-flight provider move during cancellation. Floating integration remains absent until SB11.

## Source and proof identity

- Architecture snapshot: `snap-20260817123145-eec615bc`.
- Snapshot result: no blocking errors or dependency cycles. Collector warnings for duplicated generated attributes and partially interpreted pre-existing factory registrations are non-product diagnostics.
- Static impact selection: a bounded request using the actual production diff, all three declared workspaces, `behaviorIntent=Unknown`, and `maxVisitedMembers=500` remained non-responsive and was terminated. Conservative cross-workspace selectors were run instead.
- Browser viewport: 1600x1000.
- Normal screenshot: `bundle://proof/SB10/screenshots/llm-chats-normal-1600x1000.png`.
- Open-dialog screenshot: `bundle://proof/SB10/screenshots/llm-chats-definition-dialog-1600x1000.png`.

## Validation matrix

- Component selection: 21 passed, 0 failed, 0 skipped.
- Unit streaming/session/architecture selection: 14 passed, 0 failed, 0 skipped. The first broad run exposed a stale pre-activation route assertion; its replacement now permits exactly the page and navigation owners and forbids `/chats` everywhere else in the UI module.
- Persistence Integration selection: 2 passed, 0 failed, 0 skipped.
- Playwright solution build: pass, 0 warnings, 0 errors.
- Real browser: definition edit/activate, short completion, long streaming, explicit cancellation, mid-stream refresh/reconnect, optimistic conflict/reload, and sanitized provider error all pass.
- Browser console after final scenarios: 0 warnings, 0 errors.
- Server logs after clean cancellation and refresh scenarios: no new warning or error entries.
- Static whitespace checks: `git diff --check` and `git diff --cached --check` pass.

## Progression

CP2 passes. `floatingIntegrationAllowed=true`; SB11 is unlocked.

The manifest omits its own self-referential digest. Its integrity is checked by the bundle validator.
