# Assumptions And Risks

## Assumptions

- The rollback is intentional and implementation must not reintroduce the reverted behavior.
- Run `b5b2e2df-f952-4fb9-913d-3cb22f9f231e` is useful for symptom analysis but not as a clean current-source repro because it was launched before rollback.
- Generic process runtime must support arbitrary enterprise processes, including non-software and management-only flows.
- Multi-team development can remain a software-delivery process, but .NET-specific rules must be isolated in templates, process drivers, or driver strategies.
- Capability readiness must cover runtime tools, MCP tools, skills, process instruction fragments, allowed operations, suppressions, and required receipts.
- Recovery must fail explicitly when a required capability or artifact is unavailable; it must not silently downgrade proof or mark work completed.

## Critical Path Risks

- If SB01 does not expose typed blocked-result diagnostics, later subbundles will still guess at root causes.
- If SB02 does not define a proper readiness contract, launch matching may keep assigning agents to steps they cannot execute.
- If SB03 implements fallback before typed diagnosis exists, it will hide failures behind generic manager retries.
- If SB04 starts before SB02, .NET isolation may still rely on prompt-only policy instead of enforceable contracts.
- If SB05 edits templates without SB01/SB02/SB03 proof, it may reduce one escalation while creating another.
- If SB06 only replays Calculator or Tetris, the bundle will miss domain leak regressions.

## Validation Risks

- Some blocked provider/tool details are currently not persisted. SB01 must add characterization tests that demonstrate this exact absence before changing behavior.
- E2E process runs are expensive and environment-dependent. The bundle must add narrow unit and integration tests first, then a smaller end-to-end replay.
- Tool/MCP availability may vary by local agent setup. Readiness validation must be deterministic and explain missing external capabilities without requiring live provider calls.
- Process templates are JSON/markdown-heavy. Tests should parse structured definitions instead of relying only on string contains assertions.
- Large classes create false confidence because tests may exercise only through high-level services. Each extracted classifier/resolver/strategy must be unit-testable without constructing the full runtime.

## Reopen Triggers

- A blocked run still reaches `NeedsAttention` without a typed diagnostic category and actionable source reference.
- A process step can be dispatched even though required tools, MCP tools, or skills are absent or explicitly suppressed.
- Generic runtime or dispatcher code gains a `.NET`, Blazor, Calculator, Tetris, screenshot, or Playwright rule.
- Manager fallback retries a step without classifying the failure and recording the chosen recovery policy.
- A management-only step receives development tools or skills in context when the process step explicitly suppresses them.
- A UI/browser proof step lacks Playwright/screenshot readiness, or a non-UI step is forced to require Playwright/screenshot proof.
