# Assumptions And Risks

## Assumptions

- The new mock provider and agents are allowed to remain in the repository as settings-gated test/tuning infrastructure.
- The deterministic calculator process should be used to tune the process execution core before using real LLM-backed agents.
- The repair bundle should prioritize runtime correctness and deterministic proof over UI polish.
- The process template pack may be changed if needed, but a test-only process definition builder is acceptable when it keeps production templates clean.

## Critical Path Risks

- If the untracked `StartRunAsync` automation kickoff remains, later E2E tests may pass or fail depending on background timing and SQLite locks.
- If template validation tests still do not compile, template-pack changes cannot be trusted.
- If mock role keys remain mismatched with template role keys, launch planning may staff the wrong agent or leave capability gaps.
- If the deterministic calculator process uses generic templates without a repair branch, the QA repair loop will be simulated outside the process engine instead of proving the engine.
- If dispatcher completion rules are relaxed globally to make mocks pass, real process governance will be weakened.

## Validation Risks

- Passing direct mock-agent runtime tests does not prove process service, outbox, dispatcher, role binding, branch routing, or artifact projection.
- Passing process service transition tests does not prove AgentFramework execution or mock-provider integration.
- Passing outbox tests does not prove that `DispatchAsync` can loop through multiple roles to completion.
- Browser UI tests are not the primary validation path for this bundle, but if implementation changes Process Workspace UI, Playwright proof becomes mandatory.

## Reopen Triggers

- Any recurrence of `primary.db` file-lock teardown failures after subbundle 01.
- Any process-template test project that cannot compile.
- Any E2E run that completes only because a test manually transitions process steps instead of using automation dispatch.
- Any mock-agent process run that skips the QA rejection or repair branch.
- Any artifact expectation satisfied only by title coincidence without explicit deterministic mapping in tests.
- Any dispatch completion accepted without a governed outcome marker for governed steps.
- Any real LLM provider contacted while `AgentFramework:ProcessMockAgents:Enabled` is the intended execution path.
