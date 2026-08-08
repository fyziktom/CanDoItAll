# Bundle closure audit — MAF-Refactor

Closed 2026-08-07, session `5c18c0b3-da2a-475b-b209-7b87021fc51f` (Claude Code / Claude Fable 5), continuing session `070b8b42` (2026-08-06). Branch `maf-refactor`, all work uncommitted by policy.

## Prepared work units (the bundle's own requirements)

All 19 subbundles **Completed / Unlocked** with per-subbundle `proof/proof-manifest.json` + `SESSION-HANDOFF.md`. Six blocking checkpoints executed (CP1–CP6); CP6 hard acceptance met and independently re-verified on the final tree: `Architecture guard passed.`, `Cutover guard passed.`, no reference cycles, Release build 0 errors / 0 warnings, full Unit suite at exactly the 6 known baseline failures, full Integration suite with only the documented pre-existing exclusions.

## Operator session instructions (raw inputs), note by note

| Instruction | Status | Evidence |
|---|---|---|
| Follow, implement, and validate the whole bundle e2e via the bundle workflow | **Solved** | Status table in EXECUTION-PROGRESS.md; SB00–SB18 proofs; CP1–CP6 |
| Keep proper closure so the app can run at the end; rebuild + restart the 5032 instance | **Solved** | Fresh Release build 0/0 post-SB18; 5032 running (`Now listening`), root/agents APIs 200, UI structure page renders, floating-chat affordance present |
| Preserve the instance's sample projects/data | **Solved** | All 9 sample projects intact post-closure (API count verified); additionally a pre-repair `pg_dump` backup exists in the session scratchpad. The one DB intervention (migration-squash gap repair) was additive-only, evidence in SB17 proof `environmentRepairs` |
| UI e2e: large screens only, floating agents work, page switching provides correct info, fix UI-connection regressions | **Solved** | SB17 proof `scenarioMatrix.uiE2E`: 1720×960 throughout; floating chat answered from live Gantt observation; page-switch turn captured the new surface with transition awareness; the one UI regression found (circuit death via NuGet Gantt component NRE) contained at the hosting layer and re-proven stable |
| Workflow e2e, at least 5 cases | **Solved (6 cases)** | SB17 proof `scenarioMatrix.workflowE2E`: IF + ELSE routing, two SWITCH branches, two HumanInput round-trips, idempotent launch — live OpenAI through the new `ILlmInvocationPort`, usage analytics captured |
| Processes module reflects all changes, but NO process e2e runs | **Solved** | No process runs executed; module ownership proven by SB13 (42 unit + 14 targeted integration tests), CP4 fresh verification, and full-DI app boot |
| Use the CanDoItAll CodeAnalysis MCP for navigation | **Not solvable this environment** | MCP not connected in either session (verified via tool search both sessions); gap recorded since SB00 and carried in CP4/CP5/CP6 proofs; navigation done with repo tooling instead |
| Optionally use OPENAI_API_KEY (Terra/Luna) to summarize agent-run history and save credits | **Not needed** | Context was managed with delegated agents + durable file anchors; no external summarization was required. The key WAS exercised indirectly: the instance's OpenAI providers powered the live workflow/chat e2e |
| Watch credit consumption; do not run out before the app runs | **Solved** | Closure reached with the app running and all gates green; heavy implementation delegated to subagents with tight briefs to protect the orchestrating context |

## Residual items (explicit, none hidden)

1. **Pre-existing Integration inconsistency** (`ProjectStructureRealMafPromptHarnessTests`: 3 failures — scripted-harness tool script vs. the deliberate post-`3b5477e9d` lease-visibility contract, plus the `ExitSummary` assertion vs. the reduced receipts DTO that never carried the field). Both proven pre-bundle via git archaeology; needs a product decision, not a mechanical fix. Details: SB17 proof `bugs[SB17-PREEXISTING-1]`, SB18 proof `bugs[SB18-BUG-2]`.
2. **Upstream library fix owed**: `CanDoItAll.Components.Gantt` (0.1.18) `GanttTaskDragSource.DisposeRegistrationAsync` NRE on dispose-during-registration; contained in this repo via ErrorBoundary, root fix belongs to the components repository.
3. **Environment-only exclusions**: Components (bUnit) suite banned by operator (hangs); `FileSandboxWorkspaceStoreLockIntegrationTests` + two Playwright-evidence detail tests hang at test-host startup on this machine (hang-dump evidence in SB15 proof).
4. **Named compatibility readers retained with removal criteria** (ADR-012): legacy unversioned session-state reader; `ExecutionInvocationMetadata` process-key storage format.

## Exit condition

Implementation and durable bundle state agree; every applicable gate passed or is honestly blocked with evidence; original inputs are closed above; no required work is disguised as a caveat.
