# Assumptions And Risks

## Working Assumptions

- `Microsoft.Agents.AI.A2A`, `Microsoft.Agents.AI.Hosting.A2A`, and `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` are acceptable preview dependencies only if isolated to the MAF/hosting adapter projects and guarded by explicit configuration.
- `gpt-5.4-mini` is available in the user's OpenAI provider account and should be the default for OpenAI-backed agents. Provider health tests must fail clearly if the account cannot access it.
- Process delivery failures described by the user are caused by a combination of missing cooperation primitives, insufficient role-specific tool grants, and weak handoff evidence, not a single MAF package bug.
- Existing process artifact validation must remain authoritative. New handoff/A2A paths should feed it better evidence, not bypass it.
- UI work, if any, should be minimal: editor fields/panels for A2A endpoints, handoff relationships, and tool profiles rather than a new design surface.

## Critical Path Risks

- MAF 1.3 may contain breaking API changes from the current `1.0.0` integration around agents, sessions, run options, hosted tools, approvals, middleware, or workflow APIs.
- A2A package train is preview while core/workflows are stable `1.3.0`; pulling preview types into public Core/Models contracts would make the repo harder to evolve.
- Handoff workflows can become prompt-only theater if they are not tied to process artifact inputs/outputs and explicit downstream gates.
- Recursive agent calls can create runaway loops or approval deadlocks if `CanAskOtherAgents`, max handoff depth, cancellation, and run correlation are not enforced.
- Broad tool defaults for dev agents can create security and data-loss risk. Tool availability must be role-scoped and approval-aware.
- Context changes can break governed process runs if compaction, session restore, or transcript replay drops required upstream artifact text or tool results.

## Validation Risks

- Package restore/build may fail because this repo targets `net10.0` and current MAF transitive dependencies may pull newer `Microsoft.Extensions.*` versions.
- Unit tests may pass while real process flows still fail if validation relies only on deterministic mock agents. At least one process-runtime integration proof should assert implementation artifacts are read by QA/review steps.
- Browser validation is required only if visible Blazor configuration UI changes are made; otherwise forcing Playwright into package/runtime work adds noise.
- A2A tests should use local mock/stub endpoints or MAF local sample patterns; they must not depend on external internet agents.

## Reopen Triggers

- Any MAF API change that forces public Core contracts to reference preview A2A SDK types must reopen architecture before implementation continues.
- Any architecture review finding that A2A/handoff code crosses UI, Core, Maf, and Processes boundaries without a clear owned contract must add a remediation subbundle before process-flow work.
- Any test proof showing agents complete implementation steps without required artifacts must reopen `05-process-artifact-handoff-enforcement`.
- Any evidence that context limits truncate governed process artifact instructions must reopen `07-context-session-and-compaction-policy`.
